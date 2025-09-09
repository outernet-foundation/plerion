# main.py
from __future__ import annotations

import json
import time
from pathlib import Path
from typing import Any, Dict, Optional

from common.boto_clients import create_ecs_client, create_secretsmanager_client
from common.database_utils import postgres_cursor
from psycopg.sql import SQL, Identifier, Literal
from typer import Exit, Option, echo, run

from .settings import Settings, get_settings

workspace_directory = Path("/mnt/efs")
data_sources_path = workspace_directory / "GlobalConfiguration/.dbeaver/data-sources.json"


def main(event: Dict[str, Any], _context: Any):
    op = event.get("op")
    name = event.get("name")

    if op not in {"create", "update", "delete"}:
        raise ValueError(f"Invalid op: {op}")
    if not isinstance(name, str) or not name:
        raise ValueError("name is required")

    settings = get_settings()

    if op in {"create", "update"}:
        password_plaintext = event.get("password")
        password_secret_arn = event.get("password_secret_arn")

        if password_plaintext is not None and password_secret_arn is not None:
            raise ValueError("Only one of password or password_secret_arn may be provided")
        if password_plaintext is None and password_secret_arn is None:
            raise ValueError("One of password or password_secret_arn must be provided")

        if password_plaintext is not None:
            if not isinstance(password_plaintext, str):
                raise ValueError("password must be a non-empty string")
            password = password_plaintext
        else:
            assert password_secret_arn is not None
            if not isinstance(password_secret_arn, str):
                raise ValueError("password_secret_arn must be a non-empty string")

            password = create_secretsmanager_client().get_secret_value(SecretId=password_secret_arn)["SecretString"]

        if op == "create":
            _create_database(name=name, password=password, settings=settings)
        else:
            assert op == "update"
            _update_database(name=name, password=password, settings=settings)
    else:
        assert op == "delete"
        _delete_database(name=name, settings=settings)


def _create_database(*, name: str, password: str, settings: Settings) -> None:
    with postgres_cursor(
        host=settings.postgres_host, user=settings.postgres_admin_user, password=settings.postgres_admin_password
    ) as cursor:
        # Ensure database doesn't already exist
        cursor.execute("SELECT 1 FROM pg_database WHERE datname = %s", (name,))
        if cursor.fetchone() is not None:
            if settings.backend == "docker":
                print(f"Database {name} already exists, skipping creation.")
                # still update DS and bounce to ensure DS state correct
            else:
                pass
                # raise RuntimeError(f"Database {name} already exists")
        else:
            print(f"Creating database {name}")
            # Create database and role
            cursor.execute(SQL("CREATE ROLE {} LOGIN PASSWORD {}").format(Identifier(name), Literal(password)))
            cursor.execute(
                SQL("CREATE DATABASE {} OWNER {}").format(Identifier(name), Identifier(settings.postgres_admin_user))
            )

            # Connect to new DB to set permissions for role
            with postgres_cursor(
                host=settings.postgres_host,
                user=settings.postgres_admin_user,
                password=settings.postgres_admin_password,
                database_name=name,
            ) as cursor:
                cursor.execute(SQL('CREATE EXTENSION IF NOT EXISTS "uuid-ossp"'))
                cursor.execute(SQL("REVOKE ALL ON DATABASE {} FROM PUBLIC").format(Identifier(name)))
                cursor.execute(
                    SQL("GRANT CONNECT, TEMPORARY ON DATABASE {} TO {}").format(Identifier(name), Identifier(name))
                )
                cursor.execute(SQL("REVOKE CREATE ON SCHEMA public FROM PUBLIC"))
                cursor.execute(SQL("GRANT USAGE, CREATE ON SCHEMA public TO {}").format(Identifier(name)))

    _update_cloudbeaver(name=name, password=password, settings=settings)


def _update_database(*, name: str, password: str, settings: Settings) -> None:
    with postgres_cursor(
        host=settings.postgres_host, user=settings.postgres_admin_user, password=settings.postgres_admin_password
    ) as cursor:
        # Ensure database exists
        cursor.execute("SELECT 1 FROM pg_database WHERE datname = %s", (name,))
        if cursor.fetchone() is None:
            raise RuntimeError(f"Database {name} does not exist")

        # Update role password
        cursor.execute(SQL("ALTER ROLE {} WITH PASSWORD {}").format(Identifier(name), Literal(password)))

    _update_cloudbeaver(name=name, password=password, settings=settings)

    # Terminate old sessions for role so new password is enforced immediately
    with postgres_cursor(
        host=settings.postgres_host, user=settings.postgres_admin_user, password=settings.postgres_admin_password
    ) as cursor:
        cursor.execute(
            """
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE usename = %s AND pid <> pg_backend_pid()
            """,
            (name,),
        )


def _delete_database(*, name: str, settings: Settings) -> None:
    _update_cloudbeaver(name=name, password=None, settings=settings)

    # Drop database and role
    with postgres_cursor(
        host=settings.postgres_host, user=settings.postgres_admin_user, password=settings.postgres_admin_password
    ) as cursor:
        cursor.execute(SQL("DROP DATABASE {} WITH (FORCE)").format(Identifier(name)))
        cursor.execute(SQL("DROP ROLE {}").format(Identifier(name)))


def _update_cloudbeaver(*, name: str, password: str | None, settings: Settings) -> None:
    # Load current state
    with data_sources_path.open("r", encoding="utf-8") as fh:
        current_data = json.load(fh)

    new_data = json.loads(json.dumps(current_data))
    new_data.setdefault("connections", {})

    if password is None:
        new_data["connections"].pop(name, None)
    else:
        new_data["connections"][name] = {
            "provider": "postgresql",
            "driver": "postgres-jdbc",
            "name": name,
            "save-password": True,
            "configuration": {
                "host": settings.postgres_host,
                "port": "5432",
                "database": name,
                "user": name,
                "password": password,
                "url": f"jdbc:postgresql://{settings.postgres_host}:5432/{name}",
            },
        }

    if new_data == current_data:
        print("No changes to CloudBeaver data sources, skipping update.")
        return

    # Persist and restart CloudBeaver
    with data_sources_path.open("w", encoding="utf-8") as fh:
        json.dump(new_data, fh, indent=2)
        fh.write("\n")

    print("Updated CloudBeaver data sources, restarting service")

    if settings.backend == "docker":
        import docker  # local import

        client = docker.from_env()
        container = client.containers.get(settings.cloudbeaver_service_id)
        container.restart()  # type: ignore[union-attr]

        print("Waiting for CloudBeaver service to restart")
        deadline = time.time() + 120
        while time.time() < deadline:
            container.reload()
            if getattr(container, "status", "") == "running":
                return
            time.sleep(2)
        raise RuntimeError("Timeout waiting for CloudBeaver service to restart")

    else:
        assert settings.backend == "aws"
        assert settings.ecs_cluster_arn is not None
        ecs = create_ecs_client()
        ecs.update_service(
            cluster=settings.ecs_cluster_arn, service=settings.cloudbeaver_service_id, forceNewDeployment=True
        )

        print("Waiting for CloudBeaver service to restart")
        waiter = ecs.get_waiter("services_stable")
        waiter.wait(
            cluster=settings.ecs_cluster_arn,
            services=[settings.cloudbeaver_service_id],
            WaiterConfig={"Delay": 5, "MaxAttempts": 120},
        )

    print("Done")


def cli(
    op: str = Option(..., "--op", help="create | update | delete", case_sensitive=False),
    name: Optional[str] = Option(..., "--name", help="Database name"),
    password: Optional[str] = Option(None, "--password", help="DB user password"),
):
    if op.lower() != "create":
        echo(f"op='{op}' not implemented; no-op.")
        raise Exit(code=0)

    if not password:
        echo("Missing --password (or DB_PASSWORD).", err=True)
        raise Exit(code=2)

    main({"op": "create", "name": name, "password": password}, None)


if __name__ == "__main__":
    run(cli)
