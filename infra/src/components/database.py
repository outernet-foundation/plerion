import pulumi
import pulumi_aws as aws

from utils import get_default_subnet_ids

from .network import create_postgres_security_group


def create_database(
    config: pulumi.Config,
) -> tuple[aws.rds.Instance, pulumi.Output[str]]:
    instance_class: str = config.require("rdsInstanceClass")
    db_user: str = config.require("dbUsername")
    db_password_output = config.require_secret("dbPassword")

    # Security group permitting 5432 from inside VPC.
    postgres_sg = create_postgres_security_group()

    # Subnet group over default VPC subnets (sync lookups OK in small stacks).
    subnet_group = aws.rds.SubnetGroup(
        resource_name="db-subnet-group",
        subnet_ids=get_default_subnet_ids(),
    )

    db_instance = aws.rds.Instance(
        resource_name="postgres-instance",
        engine="postgres",
        engine_version="15.7",
        instance_class=instance_class,
        allocated_storage=20,
        db_subnet_group_name=subnet_group.id,
        vpc_security_group_ids=[postgres_sg.id],
        username=db_user,
        password=db_password_output,  # accepts Input[str]; we pass secret Output[str]
        skip_final_snapshot=True,
    )

    # Export a connection string as an Output[str]. Note we DO NOT stringify the secret
    # at plan time; Pulumi will handle secret propagation.
    connection_string = pulumi.Output.concat(
        "postgresql://",
        db_user,
        ":",
        db_password_output,
        "@",
        db_instance.address,
        ":5432/postgres",
    ).apply(lambda s: s)  # ensures Output[str], not Output[Any]

    pulumi.export("postgresConnectionString", connection_string)

    return db_instance, connection_string
