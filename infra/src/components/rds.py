import pulumi_aws as aws
from pulumi import ComponentResource, Config, ResourceOptions

from components.secret import Secret
from components.security_group import SecurityGroup
from components.vpc import Vpc


class RDSInstance(ComponentResource):
    def __init__(self, resource_name: str, config: Config, vpc: Vpc, *, opts: ResourceOptions | None = None):
        super().__init__("custom:RDSInstance", resource_name, opts=opts)

        self._resource_name = resource_name
        self._child_opts = ResourceOptions.merge(opts, ResourceOptions(parent=self))

        db_user: str = config.require("postgres-user")
        db_password_output = config.require_secret("postgres-password")

        subnet_group = aws.rds.SubnetGroup(resource_name="db-subnet-group", subnet_ids=vpc.private_subnet_ids)

        self.security_group = SecurityGroup("postgres-security-group", vpc=vpc)

        self.instance = aws.rds.Instance(
            "postgres",
            db_name="postgres",
            engine="postgres",
            engine_version="15",
            instance_class="db.t3.micro",
            allocated_storage=20,
            db_subnet_group_name=subnet_group.id,
            vpc_security_group_ids=[self.security_group.id],
            username=db_user,
            password=db_password_output,
            skip_final_snapshot=True,
            opts=self._child_opts,
        )

        self.password_secret = Secret("postgres-secret", secret_string=db_password_output, opts=self._child_opts)

        self.address = self.instance.address

        self.register_outputs({"address": self.address})
