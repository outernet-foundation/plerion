from piccolo.conf.apps import AppRegistry

from src.db.piccolo_shims import PostgresEngine
from src.settings import get_database_settings

settings = get_database_settings()

DB = PostgresEngine(
    config={
        "host": settings.postgres_host,
        "port": 5432,
        "user": settings.database_name,
        "password": settings.database_password,
        "database": settings.database_name,
    },
    extensions=("uuid-ossp",),
)

APP_REGISTRY = AppRegistry(apps=["src.db.app"])
