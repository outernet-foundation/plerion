from pathlib import Path

from pulumi import Config, Output, StackReference
from pulumi_aws import get_caller_identity, get_region_output
from pulumi_aws.cloudwatch import LogGroup
from pulumi_aws.ecr import Repository, get_authorization_token
from pulumi_aws.ecs import Cluster
from pulumi_aws.efs import FileSystem, MountTarget
from pulumi_aws.lb import Listener, LoadBalancer, TargetGroup
from pulumi_aws.rds import Instance
from pulumi_aws.route53 import Record
from pulumi_awsx.ecs import FargateService
from pulumi_docker import Image

from components.role_policies import create_ecr_policy, create_secrets_manager_policy
from components.secret import Secret
from components.security_group import SecurityGroup
from components.vpc import Vpc


def create_cloudbeaver(
    config: Config,
    core_stack: StackReference,
    vpc: Vpc,
    postgres_security_group: SecurityGroup,
    db: Instance,
    cluster: Cluster,
) -> None:
    # Log groups
    LogGroup("cloudbeaver-log-group", name="/ecs/cloudbeaver", retention_in_days=7)
    LogGroup("cloudbeaver-init-log-group", name="/ecs/cloudbeaver-init", retention_in_days=7)

    # Secrets
    postgres_secret = Secret("postgres-secret", secret_string=config.require_secret("postgres-password"))
    cloudbeaver_secret = Secret("cloudbeaver-secret", secret_string=config.require_secret("cloudbeaver-password"))

    # Image repos
    Repository("alpine-cache-repo", name="dockerhub/library/alpine", force_delete=config.require_bool("devMode"))
    cloudbeaver_image_repo = Repository(
        "cloudbeaver-cache-repo", name="dockerhub/dbeaver/cloudbeaver", force_delete=config.require_bool("devMode")
    )

    # Security Groups
    efs_security_group = SecurityGroup("cloudbeaver-efs-security-group", vpc=vpc)

    cloudbeaver_security_group = SecurityGroup(
        "cloudbeaver-security-group",
        vpc=vpc,
        vpc_endpoints=["ecr.api", "ecr.dkr", "secretsmanager", "logs", "sts", "s3"],
        rules=[
            {"cidr_name": "anywhere", "from_cidr": "0.0.0.0/0", "ports": [8978]},
            {"to_security_group": efs_security_group, "ports": [2049]},
            {"to_security_group": postgres_security_group, "ports": [5432]},
        ],
    )

    load_balancer_security_group = SecurityGroup(
        "load-balancer-security-group",
        vpc=vpc,
        rules=[
            {"cidr_name": "anywhere", "from_cidr": "0.0.0.0/0", "ports": [80, 443]},
            {"to_security_group": cloudbeaver_security_group, "ports": [8978]},
        ],
    )

    # Create a Docker image for CloudBeaver initialization
    init_image_repo = Repository("cloudbeaver-init-repo", force_delete=config.require_bool("devMode"))
    dockerfile = Path(config.require("cloudbeaver-init-dockerfile")).resolve()
    creds = get_authorization_token()
    init_image = Image(
        "cloudbeaver-init-image",
        build={"dockerfile": str(dockerfile), "context": str(dockerfile.parent), "platform": "linux/amd64"},
        image_name=Output.concat(init_image_repo.repository_url, ":", "latest"),
        registry={"server": creds.proxy_endpoint, "username": creds.user_name, "password": creds.password},
    )

    # Create EFS for CloudBeaver workspace
    efs = FileSystem("cloudbeaver-efs")

    # Create mount targets for the EFS in each private subnet of our VPC
    mount_targets = vpc.private_subnet_ids.apply(
        lambda subnet_ids: [
            MountTarget(
                f"efs-mount-{subnet_id[-8:]}",
                file_system_id=efs.id,
                subnet_id=subnet_id,
                security_groups=[efs_security_group.id],
            )
            for subnet_id in subnet_ids
        ]
    )

    # Load balancer
    load_balancer = LoadBalancer(
        "cloudbeaver-lb", security_groups=[load_balancer_security_group.id], subnets=vpc.public_subnet_ids
    )

    load_balancer_target_group = TargetGroup(
        "cloudbeaver-lb-tg",
        port=8978,
        protocol="HTTP",
        target_type="ip",
        vpc_id=load_balancer.vpc_id,
        deregistration_delay=60,  # Wait 60s before deregistering unhealthy instances (down from default of 300, for faster rotation since we only have one instance)
        health_check={
            "path": "/",
            "protocol": "HTTP",
            "interval": 15,  # Check every 15 seconds (down from default of 30)
            "healthy_threshold": 2,  # We are healthy after 30 seconds (down from default of 5*30, for faster deployments)
            "unhealthy_threshold": 10,  # We are unhealthy after 150 seconds (up from default of 3*30, because CloudBeaver can take a while to start up)
        },
    )

    Listener(
        "cloudbeaver-https-listener",
        load_balancer_arn=load_balancer.arn,
        port=443,
        protocol="HTTPS",
        certificate_arn=core_stack.require_output("certificate-arn"),
        default_actions=[{"type": "forward", "target_group_arn": load_balancer_target_group.arn}],
    )

    Listener(
        "cloudbeaver-http-listener",
        load_balancer_arn=load_balancer.arn,
        port=80,
        protocol="HTTP",
        default_actions=[
            {"type": "redirect", "redirect": {"protocol": "HTTPS", "port": "443", "status_code": "HTTP_301"}}
        ],
    )

    # DNS records
    zone_id = core_stack.require_output("zone-id")
    zone_name = core_stack.require_output("zone-name")
    domain = zone_name.apply(lambda zone_name: f"cloudbeaver.{zone_name}")

    Record(
        "cloudbeaver-domain-record",
        zone_id=zone_id,
        name=domain,
        type="A",
        aliases=[{"name": load_balancer.dns_name, "zone_id": load_balancer.zone_id, "evaluate_target_health": True}],
    )

    # Service
    FargateService(
        "cloudbeaver-service",
        name="cloudbeaver-service",
        cluster=cluster.arn,
        desired_count=1,
        network_configuration={"subnets": vpc.private_subnet_ids, "security_groups": [cloudbeaver_security_group.id]},
        task_definition_args={
            "execution_role": {
                "args": {
                    "inline_policies": [
                        {"policy": create_secrets_manager_policy(cloudbeaver_secret.arn)},
                        {"policy": create_secrets_manager_policy(postgres_secret.arn)},
                        {"policy": create_ecr_policy(cloudbeaver_image_repo.arn)},
                    ]
                }
            },
            "volumes": [
                {
                    "name": "efs",
                    "efs_volume_configuration": {
                        "file_system_id": efs.id,
                        "transit_encryption": "ENABLED",
                        "root_directory":
                        # Ensure all mount targets are created before using the EFS
                        mount_targets.apply(lambda mts: Output.all("/", *[mt.id for mt in mts])).apply(lambda _: "/"),
                    },
                }
            ],
            "containers": {
                "cloudbeaver-init": {
                    "name": "cloudbeaver-init",
                    "image": init_image.repo_digest,
                    "essential": False,
                    "log_configuration": {
                        "log_driver": "awslogs",
                        "options": {
                            "awslogs-group": "/ecs/cloudbeaver-init",
                            "awslogs-region": get_region_output().region,
                            "awslogs-stream-prefix": "ecs",
                        },
                    },
                    "mount_points": [{"source_volume": "efs", "container_path": "/opt/cloudbeaver/workspace"}],
                    "environment": [
                        {"name": "POSTGRES_HOST", "value": db.address},
                        {"name": "POSTGRES_PORT", "value": "5432"},
                        {"name": "POSTGRES_DB", "value": "postgres"},
                        {"name": "POSTGRES_USER", "value": config.require("postgres-user")},
                        {"name": "CB_ADMIN_NAME", "value": config.require("cloudbeaver-user")},
                        {
                            "name": "_CB_ADMIN_NAME_VERSION",
                            "value": cloudbeaver_secret.version_id,
                        },  # Force update on secret change
                        {
                            "name": "_POSTGRES_PASSWORD_VERSION",
                            "value": postgres_secret.version_id,
                        },  # Force update on secret change
                    ],
                    "secrets": [
                        {"name": "POSTGRES_PASSWORD", "value_from": postgres_secret.arn},
                        {"name": "CB_ADMIN_PASSWORD", "value_from": cloudbeaver_secret.arn},
                    ],
                },
                "cloudbeaver": {
                    "name": "cloudbeaver",
                    "image": get_region_output().region.apply(
                        lambda r: f"{get_caller_identity().account_id}.dkr.ecr.{r}.amazonaws.com/dockerhub/dbeaver/cloudbeaver:latest"
                    ),
                    "log_configuration": {
                        "log_driver": "awslogs",
                        "options": {
                            "awslogs-group": "/ecs/cloudbeaver",
                            "awslogs-region": get_region_output().region,
                            "awslogs-stream-prefix": "ecs",
                        },
                    },
                    "port_mappings": [
                        {"container_port": 8978, "host_port": 8978, "target_group": load_balancer_target_group}
                    ],
                    "mount_points": [{"source_volume": "efs", "container_path": "/opt/cloudbeaver/workspace"}],
                    "environment": [
                        {"name": "CB_SERVER_NAME", "value": "CloudBeaver"},
                        {"name": "CB_SERVER_URL", "value": domain.apply(lambda d: f"https://{d}")},
                        {"name": "CB_ADMIN_NAME", "value": config.require("cloudbeaver-user")},
                        {"name": "CLOUDBEAVER_DB_DRIVER", "value": "postgres-jdbc"},
                        {
                            "name": "CLOUDBEAVER_DB_URL",
                            "value": Output.all(address=db.address, port=db.port).apply(
                                lambda args: f"jdbc:postgresql://{args['address']}:{args['port']}/postgres"
                            ),
                        },
                        {"name": "CLOUDBEAVER_DB_USER", "value": config.require("postgres-user")},
                        {"name": "CLOUDBEAVER_DB_SCHEMA", "value": "cloudbeaver"},
                        {
                            "name": "CLOUDBEAVER_DB_USER_VERSION",
                            "value": postgres_secret.version_id,
                        },  # Force update on secret change
                        {
                            "name": "CLOUDBEAVER_DB_PASSWORD_VERSION",
                            "value": cloudbeaver_secret.version_id,
                        },  # Force update on secret change
                    ],
                    "secrets": [
                        {"name": "CLOUDBEAVER_DB_PASSWORD", "value_from": postgres_secret.arn},
                        {"name": "CB_ADMIN_PASSWORD", "value_from": cloudbeaver_secret.arn},
                    ],
                    "depends_on": [{"container_name": "cloudbeaver-init", "condition": "SUCCESS"}],
                },
            },
        },
    )
