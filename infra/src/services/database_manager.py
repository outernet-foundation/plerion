from __future__ import annotations

from pulumi import ComponentResource, Config, Input, Output, ResourceOptions, export
from pulumi_aws import iam, lb
from pulumi_aws.cloudwatch import LogGroup
from pulumi_aws.lambda_ import Function as LambdaFunction
from pulumi_aws.lambda_ import Permission as LambdaPermission
from pulumi_aws.lb import Listener, TargetGroup, TargetGroupAttachment
from pulumi_aws.route53 import Record

from components.rds import RDSInstance
from components.repository import Repository
from components.role import Role
from components.security_group import SecurityGroup
from components.vpc import Vpc
from services.cloudbeaver import Cloudbeaver


class DatabaseManager(ComponentResource):
    def __init__(
        self,
        resource_name: str,
        config: Config,
        zone_name: Input[str],
        zone_id: Input[str],
        certificate_arn: Input[str],
        vpc: Vpc,
        rds: RDSInstance,
        cloudbeaver: Cloudbeaver,
        *,
        opts: ResourceOptions | None = None,
    ):
        super().__init__("custom:DatabaseManager", resource_name, opts=opts)

        self._resource_name = resource_name
        self._child_opts = ResourceOptions.merge(opts, ResourceOptions(parent=self))

        # Image repos
        database_manager_image_repo = Repository(
            "database-manager-repo",
            name="database-manager",
            opts=ResourceOptions.merge(
                self._child_opts,
                ResourceOptions(
                    retain_on_delete=True
                    # import_="database-manager"
                ),
            ),
        )

        # Security groups
        lambda_sg = SecurityGroup(
            "database-manager-security-group",
            vpc=vpc,
            vpc_endpoints=[],
            rules=[
                {"to_security_group": rds.security_group, "ports": [5432]},
                {"to_security_group": cloudbeaver.load_balancer_security_group, "ports": [443]},
            ],
            opts=self._child_opts,
        )

        # ---------------------------------------------------------------------
        # IAM role for Lambda (inline policies per your conventions)
        # ---------------------------------------------------------------------
        assume_role_policy = iam.get_policy_document_output(
            statements=[
                iam.GetPolicyDocumentStatementArgs(
                    actions=["sts:AssumeRole"],
                    principals=[
                        iam.GetPolicyDocumentStatementPrincipalArgs(
                            type="Service", identifiers=["lambda.amazonaws.com"]
                        )
                    ],
                )
            ]
        ).json

        lambda_role = Role(f"{resource_name}-lambda-role", assume_role_policy=assume_role_policy, opts=self._child_opts)

        lambda_role.attach_policy(
            "basic-exec",
            Output.json_dumps({
                "Version": "2012-10-17",
                "Statement": [
                    {
                        "Effect": "Allow",
                        "Action": ["logs:CreateLogGroup", "logs:CreateLogStream", "logs:PutLogEvents"],
                        "Resource": "*",
                    }
                ],
            }),
        )
        lambda_role.attach_policy(
            "vpc-access",
            Output.json_dumps({
                "Version": "2012-10-17",
                "Statement": [
                    {
                        "Effect": "Allow",
                        "Action": [
                            "ec2:CreateNetworkInterface",
                            "ec2:DescribeNetworkInterfaces",
                            "ec2:DeleteNetworkInterface",
                            "ec2:AssignPrivateIpAddresses",
                            "ec2:UnassignPrivateIpAddresses",
                        ],
                        "Resource": "*",
                    }
                ],
            }),
        )

        if config.require_bool("deploy-database-manager"):
            # ---------------------------------------------------------------------
            # Lambda function (container image from ECR; hard-coded arch/mem/timeout)
            # ---------------------------------------------------------------------
            lambda_function = LambdaFunction(
                "database-manager-lambda-function",
                name="database-manager-lambda-function",
                role=lambda_role.arn,
                package_type="Image",
                image_uri=database_manager_image_repo.locked_digest(),  # <repo>@sha256:...
                architectures=["x86_64"],  # hardcoded
                memory_size=1024,  # hardcoded
                timeout=60,  # hardcoded
                vpc_config={"subnet_ids": vpc.private_subnet_ids, "security_group_ids": [lambda_sg.id]},
                environment={
                    "variables": {
                        "POSTGRES_HOST": rds.address,
                        "POSTGRES_USER": config.require("postgres-user"),
                        "CLOUDBEAVER_URL": cloudbeaver.load_balancer.dns_name,
                        "CLOUDBEAVER_ADMIN_USER": config.require("cloudbeaver-user"),
                        "POSTGRES_PASSWORD_ARN": rds.password_secret.arn,
                        "CLOUDBEAVER_ADMIN_PASSWORD": cloudbeaver.password_secret.arn,
                        "_POSTGRES_PASSWORD_VERSION": rds.password_secret.version_id,
                        "_CLOUDBEAVER_ADMIN_PASSWORD_VERSION": cloudbeaver.password_secret.version_id,
                    }
                },
                opts=self._child_opts,
            )

            # Log gropu
            LogGroup(
                f"{resource_name}-logs",
                name=Output.concat("/aws/lambda/", lambda_function.name),
                retention_in_days=7,
                opts=self._child_opts,
            )

            # ---------------------------------------------------------------------
            # Internal ALB (private subnets) + Lambda target group + HTTPS listener
            # ---------------------------------------------------------------------
            # ALB SG: allow HTTPS from within the VPC CIDR
            alb_sg = SecurityGroup(
                f"{resource_name}-alb-sg",
                vpc=vpc,
                rules=[{"from_cidr": vpc.cidr_block, "cidr_name": "vpc", "ports": [443]}],
                opts=self._child_opts,
            )

            application_lb = lb.LoadBalancer(
                f"{resource_name}-alb",
                internal=True,
                security_groups=[alb_sg.id],
                subnets=vpc.private_subnet_ids,
                load_balancer_type="application",
                opts=self._child_opts,
            )

            lambda_tg = TargetGroup(f"{resource_name}-tg", target_type="lambda", opts=self._child_opts)

            TargetGroupAttachment(
                f"{resource_name}-tga",
                target_group_arn=lambda_tg.arn,
                target_id=lambda_function.arn,
                opts=self._child_opts,
            )

            # Allow ALB to invoke Lambda
            LambdaPermission(
                f"{resource_name}-invoke",
                action="lambda:InvokeFunction",
                function=lambda_function.name,
                principal="elasticloadbalancing.amazonaws.com",
                source_arn=application_lb.arn,
                opts=self._child_opts,
            )

            Listener(
                f"{resource_name}-https",
                load_balancer_arn=application_lb.arn,
                port=443,  # hardcoded port
                protocol="HTTPS",
                certificate_arn=certificate_arn,
                default_actions=[{"type": "forward", "target_group_arn": lambda_tg.arn}],
                opts=self._child_opts,
            )

            # Optional private DNS record (if you pass your private hosted zone ID)
            if zone_name and zone_id:
                Record(
                    f"{resource_name}-dns",
                    name=zone_name,  # hardcoded host
                    type="A",
                    zone_id=zone_id,
                    aliases=[
                        {
                            "name": application_lb.dns_name,
                            "zone_id": application_lb.zone_id,
                            "evaluate_target_health": False,
                        }
                    ],
                    opts=self._child_opts,
                )
                self.url = Output.concat("https://", zone_name)
            else:
                self.url = Output.concat("https://", application_lb.dns_name)

            export("database-manager-url", self.url)

        self.register_outputs({
            # "alb_dns_name": application_lb.dns_name,
            # "alb_zone_id": application_lb.zone_id,
            # "lambda_name": lambda_function.name,
            # "url": self.url,
        })
