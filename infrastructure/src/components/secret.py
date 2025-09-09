from typing import overload

import pulumi_aws as aws
from pulumi import ComponentResource, Input, ResourceOptions
from pulumi_aws.secretsmanager import get_secret_output, get_secret_version_output


class Secret(ComponentResource):
    @overload
    def __init__(self, resource_name: str, *, arn: Input[str], opts: ResourceOptions | None = None) -> None: ...

    @overload
    def __init__(
        self,
        resource_name: str,
        *,
        secret_string: Input[str],
        name_prefix: Input[str] | None = None,
        opts: ResourceOptions | None = None,
    ) -> None: ...

    def __init__(
        self,
        resource_name: str,
        *,
        arn: Input[str] | None = None,
        name_prefix: Input[str] | None = None,
        secret_string: Input[str] | None = None,
        opts: ResourceOptions | None = None,
    ) -> None:
        super().__init__("custom:Secret", resource_name, opts=opts)

        self._resource_name = resource_name
        self._child_opts = ResourceOptions.merge(opts, ResourceOptions(parent=self))

        if arn:
            self._secret = get_secret_output(name=arn)
            self._secret_version = get_secret_version_output(secret_id=self._secret.id)
        else:
            self._secret = aws.secretsmanager.Secret(resource_name, name_prefix=name_prefix, opts=self._child_opts)
            self._secret_version = aws.secretsmanager.SecretVersion(
                f"{resource_name}-version",
                secret_id=self._secret.id,
                secret_string=secret_string,
                opts=self._child_opts,
            )

        self.arn = self._secret.arn
        self.version_id = self._secret_version.version_id

        self.register_outputs({"arn": self.arn, "version_id": self.version_id})
