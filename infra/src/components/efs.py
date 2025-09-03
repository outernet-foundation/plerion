from pulumi import ComponentResource, ResourceOptions
from pulumi_aws.efs import FileSystem, MountTarget

from components.security_group import SecurityGroup
from components.vpc import Vpc


class EFS(ComponentResource):
    def __init__(self, resource_name: str, vpc: Vpc, opts: ResourceOptions | None = None):
        super().__init__("custom:EFS", resource_name, opts=opts)

        self._child_opts = ResourceOptions.merge(opts, ResourceOptions(parent=self))
        self._resource_name = resource_name

        self.security_group = SecurityGroup(f"{self._resource_name}-security-group", vpc=vpc, opts=self._child_opts)
        efs = FileSystem(f"{self._resource_name}-filesystem", opts=self._child_opts)
        vpc.private_subnet_ids.apply(
            lambda subnet_ids: [
                MountTarget(
                    f"{self._resource_name}-mount-target-{subnet_id[-8:]}",
                    file_system_id=efs.id,
                    subnet_id=subnet_id,
                    security_groups=[self.security_group.id],
                    opts=self._child_opts,
                )
                for subnet_id in subnet_ids
            ]
        )

        self.id = efs.id

        self.register_outputs({"id": self.id})
