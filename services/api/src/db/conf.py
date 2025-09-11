from piccolo.conf.apps import AppRegistry

from src.db.piccolo_shims import PostgresEngine
from src.settings import get_database_settings

settings = get_database_settings()

DB = PostgresEngine(
    config={
        "dsn": f"postgresql://{settings.database_name}:{settings.database_password}@{settings.postgres_host}:5432/{settings.database_name}"
    },
    extensions=("uuid-ossp",),
)

APP_REGISTRY = AppRegistry(apps=["src.db.app"])
