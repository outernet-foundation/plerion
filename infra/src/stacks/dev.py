from typing import cast

from pulumi import Config, Output, StackReference
from pulumi_aws.ecs import Cluster

from components.rds import create_database
from components.role import Role
from components.s3 import create_storage
from components.vpc import Vpc, VpcInfo
from services.api import create_api
from services.cloudbeaver import create_cloudbeaver
from services.github_runner import create_github_runner


def create_dev_stack(config: Config):
    core_stack = StackReference("tyler-s-hatch/plerion_infra/core")
    main_prepare_deploy_role = Role(
        "main-prepare-deploy-role",
        name=core_stack.require_output("main-prepare-deploy-role-name"),
        arn=core_stack.require_output("main-prepare-deploy-role-arn"),
    )
    main_deploy_role = Role(
        "main-deploy-role",
        name=core_stack.require_output("main-deploy-role-name"),
        arn=core_stack.require_output("main-deploy-role-arn"),
    )

    vpc = Vpc(name="main-vpc", vpc_info=cast(Output[VpcInfo], core_stack.require_output("vpc-info")))

    captures_bucket = create_storage(core_stack)

    postgres_instance, postgres_security_group, postgres_dsn_secret = create_database(
        config, vpc, deploy_role=main_deploy_role
    )

    cluster = Cluster("main-cluster")

    create_github_runner(
        vpc=vpc,
        config=config,
        cluster=cluster,
        postgres_security_group=postgres_security_group,
        prepare_deploy_role=main_prepare_deploy_role,
        deploy_role=main_deploy_role,
    )

    create_cloudbeaver(
        config,
        core_stack,
        vpc=vpc,
        postgres_security_group=postgres_security_group,
        db=postgres_instance,
        cluster=cluster,
        prepare_deploy_role=main_prepare_deploy_role,
        deploy_role=main_deploy_role,
    )

    create_api(
        config,
        core_stack,
        cluster=cluster,
        s3_bucket=captures_bucket,
        vpc=vpc,
        postgres_security_group=postgres_security_group,
        postgres_dsn_secret=postgres_dsn_secret,
        prepare_deploy_role=main_prepare_deploy_role,
        deploy_role=main_deploy_role,
    )
