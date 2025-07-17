import os

from piccolo.conf.apps import AppConfig, table_finder

APP_CONFIG = AppConfig(
    app_name="db",
    migrations_folder_path=os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "migrations"
    ),
    table_classes=table_finder(modules=["src.db.tables"]),
    migration_dependencies=[],
    commands=[],
)
