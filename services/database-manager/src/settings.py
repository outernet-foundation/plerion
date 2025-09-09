from __future__ import annotations

from functools import lru_cache
from typing import TYPE_CHECKING, Any

import boto3
from pydantic import AnyHttpUrl, Field, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

if TYPE_CHECKING:
    from mypy_boto3_secretsmanager import SecretsManagerClient
else:
    SecretsManagerClient = Any


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env")

    postgres_host: str = Field()
    postgres_user: str = Field()
    postgres_port: int = Field()
    postgres_password: str = Field(default="")
    postgres_password_arn: str | None = None

    cloudbeaver_url: AnyHttpUrl = Field()
    cloudbeaver_admin_user: str = Field()
    cloudbeaver_admin_password: str = Field(default="")
    cloudbeaver_admin_password_arn: str | None = None

    @model_validator(mode="after")
    def _resolve_secret_passwords(self):
        has_password = (
            self.postgres_password != "" and self.cloudbeaver_admin_password != ""
        )
        has_password_arn = (
            self.postgres_password_arn is not None
            and self.cloudbeaver_admin_password_arn is not None
        )

        if has_password and has_password_arn:
            raise ValueError("Only one of password or password_arn can be set.")
        if not has_password and not has_password_arn:
            raise ValueError("Either password or password_arn must be set.")

        if has_password_arn:
            # I'm surpised this was required, couldn't it have been inferred?
            assert self.postgres_password_arn is not None
            assert self.cloudbeaver_admin_password_arn is not None

            client: SecretsManagerClient = boto3.client("secretsmanager")  # type: ignore[call-arg]
            self.postgres_password = client.get_secret_value(
                SecretId=self.postgres_password_arn
            )["SecretString"]
            self.cloudbeaver_admin_password = client.get_secret_value(
                SecretId=self.cloudbeaver_admin_password_arn
            )["SecretString"]

        return self


@lru_cache()
def get_settings() -> Settings:
    return Settings()  # type: ignore[call-arg]
