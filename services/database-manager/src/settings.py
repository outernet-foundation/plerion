from __future__ import annotations

from functools import lru_cache

from pydantic import AnyHttpUrl
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env")

    postgres_host: str
    postgres_port: int = 5432
    postgres_admin_user: str
    postgres_admin_password: str

    cloudbeaver_url: AnyHttpUrl  # e.g., http://cloudbeaver.internal:8978/api/gql
    cloudbeaver_admin_user: str
    cloudbeaver_admin_password: str
    cloudbeaver_project_id: str = "g_GlobalConfiguration"


@lru_cache()
def get_settings() -> Settings:
    return Settings()  # type: ignore[call-arg]
