import pulumi_aws as aws
from pulumi import ComponentResource, Output, ResourceOptions


class Secret(ComponentResource):
    def __init__(
        self,
        resource_name: str,
        name_prefix: str | None = None,
        secret_string: Output[str] | None = None,
        opts: ResourceOptions | None = None,
    ) -> None:
        super().__init__("custom:Secret", resource_name, opts=opts)

        self._child_opts = ResourceOptions.merge(opts, ResourceOptions(parent=self))
        self._secret = aws.secretsmanager.Secret(resource_name, name_prefix=name_prefix, opts=self._child_opts)
        self._secret_version = aws.secretsmanager.SecretVersion(
            f"{resource_name}-version", secret_id=self._secret.id, secret_string=secret_string, opts=self._child_opts
        )

        self.arn = Output.all(self._secret.arn, self._secret_version.version_id).apply(lambda args: args[0])

        self.register_outputs({"arn": self.arn})
