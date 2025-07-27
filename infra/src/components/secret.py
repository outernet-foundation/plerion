import pulumi_aws as aws
from pulumi import ComponentResource, Input, Output


class Secret(ComponentResource):
    def __init__(self, resource_name: str, name: str | None = None, secret_string: Input[str] | None = None) -> None:
        if name is None:
            name = resource_name

        super().__init__("infra:components:Secret", name, {})

        self._secret = aws.secretsmanager.Secret(resource_name, name=name)

        self._secret_version = aws.secretsmanager.SecretVersion(
            f"{resource_name}-version", secret_id=self._secret.id, secret_string=secret_string
        )

        self.arn = Output.all(self._secret.arn, self._secret_version.version_id).apply(lambda args: args[0])

        self.register_outputs({"arn": self.arn})
