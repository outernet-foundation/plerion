from typing import Sequence

from pulumi import ComponentResource, Config, Input, Output, ResourceOptions, export
from pulumi_aws.cloudwatch import LogGroup
from pulumi_aws.ecs import Cluster
from pulumi_aws.route53 import Record
from pulumi_awsx.ecs import FargateService
from pulumi_awsx.ecs._inputs import TaskDefinitionKeyValuePairArgsDict

from components.load_balancer import LoadBalancer
from components.log import log_configuration
from components.repository import Repository
from components.role import Role
from components.roles import ecs_execution_role, ecs_role
from components.secret import Secret
from components.security_group import SecurityGroup
from components.vpc import Vpc


class Oauth(ComponentResource):
    def __init__(
        self,
        resource_name: str,
        config: Config,
        zone_id: Input[str],
        zone_name: Input[str],
        certificate_arn: Input[str],
        vpc: Vpc,
        cluster: Cluster,
        prepare_deploy_role: Role,
        deploy_role: Role,
        *,
        opts: ResourceOptions | None = None,
    ):
        super().__init__("custom:Oauth", resource_name, opts=opts)

        self._resource_name = resource_name
        self._child_opts = ResourceOptions.merge(opts, ResourceOptions(parent=self))

        # Logs groups
        oauth_log_group = LogGroup(
            "oauth-log-group", name="/ecs/oauth2-proxy", retention_in_days=7, opts=self._child_opts
        )

        # Secrets
        github_client_id_secret = Secret(
            "oauth-client-id", secret_string=config.require_secret("oauth-client-id"), opts=self._child_opts
        )
        github_client_secret_secret = Secret(
            "oauth-client-secret", secret_string=config.require_secret("oauth-client-secret"), opts=self._child_opts
        )
        # python -c 'import os,base64; print(base64.urlsafe_b64encode(os.urandom(32)).decode())'
        cookie_secret_secret = Secret(
            "oauth-cookie-secret", secret_string=config.require_secret("oauth-cookie-secret"), opts=self._child_opts
        )

        # Image repo
        oauth_image_repo = Repository(
            "oauth2-proxy-repo",
            "quay/oauth2-proxy/oauth2-proxy",
            opts=ResourceOptions.merge(
                self._child_opts,
                ResourceOptions(
                    retain_on_delete=True
                    # import_="quay.io/oauth2-proxy/oauth2-proxy",
                ),
            ),
        )
        prepare_deploy_role.allow_image_repo_actions([oauth_image_repo])
        export("oauth2-proxy-image-repo-url", oauth_image_repo.url)

        # --------------------
        # Security Groups
        # --------------------
        oauth_security_group = SecurityGroup(
            "oauth-security-group",
            vpc=vpc,
            vpc_endpoints=["ecr.api", "ecr.dkr", "secretsmanager", "logs", "sts", "s3"],
            opts=self._child_opts,
        )

        load_balancer_security_group = SecurityGroup(
            "oauth-load-balancer-security-group",
            vpc=vpc,
            rules=[
                {"cidr_name": "anywhere", "from_cidr": "0.0.0.0/0", "ports": [80, 443]},
                {"to_security_group": oauth_security_group, "ports": [4180]},
            ],
            opts=self._child_opts,
        )

        # Load Balancer
        load_balancer = LoadBalancer(
            "oauth-loadbalancer",
            "oauth",
            vpc=vpc,
            securityGroup=load_balancer_security_group,
            certificate_arn=certificate_arn,
            port=4180,
            health_check={
                "path": "/ping",
                "protocol": "HTTP",
                "interval": 15,
                "healthy_threshold": 2,
                "unhealthy_threshold": 10,
            },
            opts=self._child_opts,
        )

        # DNS Record
        domain = Output.concat("auth.", zone_name)
        Record(
            "oauth-domain-record",
            zone_id=zone_id,
            name=domain,
            type="A",
            aliases=[
                {"name": load_balancer.dns_name, "zone_id": load_balancer.zone_id, "evaluate_target_health": True}
            ],
            opts=self._child_opts,
        )

        # Execution role
        execution_role = ecs_execution_role("oauth-execution-role", opts=self._child_opts)
        execution_role.allow_secret_get([github_client_id_secret, github_client_secret_secret, cookie_secret_secret])

        # Task Role
        task_role = ecs_role("oauth-task-role", opts=self._child_opts)

        # Environment
        environment: Sequence[Input[TaskDefinitionKeyValuePairArgsDict]] = [
            {"name": "OAUTH2_PROXY_PROVIDER", "value": "github"},
            {
                "name": "OAUTH2_PROXY_REDIRECT_URL",
                "value": domain.apply(lambda domain: f"https://{domain}/oauth2/callback"),
            },
            {"name": "OAUTH2_PROXY_UPSTREAMS", "value": "static://200"},
            {"name": "OAUTH2_PROXY_COOKIE_SECURE", "value": "true"},
            {"name": "OAUTH2_PROXY_COOKIE_SAMESITE", "value": "lax"},
            {"name": "OAUTH2_PROXY_REVERSE_PROXY", "value": "true"},
            {"name": "OAUTH2_PROXY_SET_XAUTHREQUEST", "value": "true"},
            {"name": "OAUTH2_PROXY_PASS_AUTHORIZATION_HEADER", "value": "true"},
            {"name": "OAUTH2_PROXY_COOKIE_DOMAINS", "value": Output.concat(".", zone_name)},
            {"name": "OAUTH2_PROXY_WHITELIST_DOMAIN", "value": Output.concat(".", zone_name)},
        ]

        # Allow-lists
        allowed_users = config.get("oauth-allowed-users")
        if allowed_users:
            environment.append({"name": "OAUTH2_PROXY_GITHUB_USER", "value": allowed_users})

        github_org = config.get("oauth-org")
        if github_org:
            environment.append({"name": "OAUTH2_PROXY_GITHUB_ORG", "value": github_org})

        github_team = config.get("oauth-team")
        if github_team:
            environment.append({"name": "OAUTH2_PROXY_GITHUB_TEAM", "value": github_team})

        # Service
        service = FargateService(
            "oauth-service",
            name="oauth-service",
            cluster=cluster.arn,
            desired_count=1,
            network_configuration={"subnets": vpc.private_subnet_ids, "security_groups": [oauth_security_group.id]},
            task_definition_args={
                "execution_role": {"role_arn": execution_role.arn},
                "task_role": {"role_arn": task_role.arn},
                "containers": {
                    "oauth2-proxy": {
                        "name": "oauth2-proxy",
                        "image": oauth_image_repo.locked_digest(),
                        "log_configuration": log_configuration(oauth_log_group),
                        "port_mappings": [
                            {"container_port": 4180, "host_port": 4180, "target_group": load_balancer.target_group}
                        ],
                        "environment": environment,
                        "secrets": [
                            {"name": "OAUTH2_PROXY_CLIENT_ID", "value_from": github_client_id_secret.arn},
                            {"name": "OAUTH2_PROXY_CLIENT_SECRET", "value_from": github_client_secret_secret.arn},
                            {"name": "OAUTH2_PROXY_COOKIE_SECRET", "value_from": cookie_secret_secret.arn},
                        ],
                    }
                },
            },
            opts=self._child_opts,
        )

        # Allow service deployment
        deploy_role.allow_service_deployment("oauth", passroles=[execution_role, task_role], services=[service.service])
