from __future__ import annotations

import json
import secrets
from contextlib import contextmanager
from functools import lru_cache
from http.cookiejar import CookieJar
from typing import TypedDict
from urllib.request import HTTPCookieProcessor, OpenerDirector, Request, build_opener

from fastapi import Body, FastAPI, HTTPException
from mangum import Mangum
from psycopg import connect, sql
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


class CBConnection(TypedDict):
    id: str
    name: str
    projectId: str


class CBProject(TypedDict):
    connections: list[CBConnection]


class CBData(TypedDict):
    project: CBProject


class CBResponse(TypedDict):
    data: CBData


app = FastAPI(title="DB Registrar API")
handler = Mangum(app)


@app.post("/create_database")
def create_database(
    database_name: str = Body(..., embed=True),
    database_user: str = Body(..., embed=True),
):
    with postgres_cursor() as cursor:
        # Database must not exist
        cursor.execute("SELECT 1 FROM pg_database WHERE datname = %s", (database_name,))
        if cursor.fetchone() is not None:
            raise HTTPException(409, f"Database already exists: {database_name}")

        generated_password: str = secrets.token_urlsafe(32)

        # Create role and database
        cursor.execute(
            sql.SQL("CREATE ROLE {} LOGIN PASSWORD %s").format(
                sql.Identifier(database_user)
            ),
            (generated_password,),
        )
        cursor.execute(
            sql.SQL("CREATE DATABASE {} OWNER {}").format(
                sql.Identifier(database_name),
                sql.Identifier(database_user),
            )
        )

    return {"ok": True, "password": generated_password}


@app.delete("/destroy_database")
def destroy_database(
    database_name: str = Body(..., embed=True),
    database_user: str = Body(..., embed=True),
):
    # Database must exist
    with postgres_cursor() as cursor:
        cursor.execute("SELECT 1 FROM pg_database WHERE datname = %s", (database_name,))
        if cursor.fetchone() is None:
            raise HTTPException(404, f"Database not found: {database_name}")

    # CloudBeaver connection must not exist
    cloudbeaver_api_url, opener = cloudbeaver_login()
    connections_list = cloudbeaver_list_connections(opener, cloudbeaver_api_url)
    if any(connection["name"] == database_name for connection in connections_list):
        raise HTTPException(
            422, f"CloudBeaver connection exists for database name: {database_name}"
        )

    # Drop database and role
    with postgres_cursor() as cursor2:
        cursor2.execute(
            """
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = %s AND pid <> pg_backend_pid()
            """,
            (database_name,),
        )
        cursor2.execute(
            sql.SQL("DROP DATABASE {}").format(sql.Identifier(database_name))
        )
        cursor2.execute(sql.SQL("DROP ROLE {}").format(sql.Identifier(database_user)))

    return {"ok": True}


@app.post("/connect_cloudbeaver")
def connect_cloudbeaver(
    database_name: str = Body(..., embed=True),
    database_user: str = Body(..., embed=True),
    database_password: str = Body(..., embed=True),
):
    settings = get_settings()

    # Database must exist
    with postgres_cursor() as cursor:
        cursor.execute("SELECT 1 FROM pg_database WHERE datname = %s", (database_name,))
        if cursor.fetchone() is None:
            raise HTTPException(422, f"Database does not exist: {database_name}")

    # CloudBeaver connection must not exist
    cloudbeaver_api_url, opener = cloudbeaver_login()
    connections_list = cloudbeaver_list_connections(opener, cloudbeaver_api_url)
    if any(connection["name"] == database_name for connection in connections_list):
        raise HTTPException(409, f"CloudBeaver connection exists: {database_name}")

    # Create CloudBeaver connection
    with opener.open(
        Request(
            cloudbeaver_api_url,
            data=json.dumps(
                {
                    "query": (
                        "mutation C($pid:ID!,$cfg:ConnectionConfig!){"
                        "connection:createConnection(projectId:$pid,config:$cfg){id name}}"
                    ),
                    "variables": {
                        "pid": settings.cloudbeaver_project_id,
                        "cfg": {
                            "configurationType": "MANUAL",
                            "driverId": "postgresql",
                            "name": database_name,
                            "host": settings.postgres_host,
                            "port": str(settings.postgres_port),
                            "databaseName": database_name,
                            "authModelId": "native",
                            "credentials": {
                                "userName": database_user,
                                "userPassword": database_password,
                            },
                            "properties": {},
                        },
                    },
                }
            ).encode("utf-8"),
            headers={"Content-Type": "application/json"},
        ),
        timeout=30,
    ) as create_response:
        _ = create_response.read()

    return {"ok": True}


@app.delete("/disconnect_cloudbeaver")
def disconnect_cloudbeaver(
    database_name: str = Body(..., embed=True),
):
    settings = get_settings()

    # CloudBeaver connection must exist
    cloudbeaver_api_url, opener = cloudbeaver_login()
    connections_list = cloudbeaver_list_connections(opener, cloudbeaver_api_url)
    connection_id: str | None = next(
        (c["id"] for c in connections_list if c["name"] == database_name),
        None,
    )
    if connection_id is None:
        raise HTTPException(404, f"CloudBeaver connection not found: {database_name}")

    # Delete CloudBeaver connection
    with opener.open(
        Request(
            cloudbeaver_api_url,
            data=json.dumps(
                {
                    "query": "mutation D($pid:ID!,$id:ID!){deleteConnection(projectId:$pid,connectionId:$id)}",
                    "variables": {
                        "pid": settings.cloudbeaver_project_id,
                        "id": connection_id,
                    },
                }
            ).encode("utf-8"),
            headers={"Content-Type": "application/json"},
        ),
        timeout=30,
    ) as delete_response:
        _ = delete_response.read()

    return {"ok": True}


@contextmanager
def postgres_cursor(database_name: str = "postgres"):
    settings = get_settings()
    with connect(
        host=settings.postgres_host,
        port=settings.postgres_port,
        dbname=database_name,
        user=settings.postgres_admin_user,
        password=settings.postgres_admin_password,
        connect_timeout=5,
    ) as connection:
        connection.autocommit = True
        with connection.cursor() as cursor:
            yield cursor


def cloudbeaver_login():
    settings = get_settings()
    cloudbeaver_api_url: str = str(settings.cloudbeaver_url).rstrip("/")
    opener: OpenerDirector = build_opener(HTTPCookieProcessor(CookieJar()))

    with opener.open(
        Request(
            cloudbeaver_api_url,
            data=json.dumps(
                {
                    "query": (
                        "mutation L($p:String!,$c:ObjectScalar!){"
                        "authLogin(provider:$p,credentials:$c){authStatus}}"
                    ),
                    "variables": {
                        "p": "local",
                        "c": {
                            "user": settings.cloudbeaver_admin_user,
                            "password": settings.cloudbeaver_admin_password,
                        },
                    },
                }
            ).encode("utf-8"),
            headers={"Content-Type": "application/json"},
        ),
        timeout=15,
    ) as login_response:
        _ = login_response.read()

    return cloudbeaver_api_url, opener


def cloudbeaver_list_connections(opener: OpenerDirector, cloudbeaver_api_url: str):
    settings = get_settings()

    with opener.open(
        Request(
            cloudbeaver_api_url,
            data=json.dumps(
                {
                    "query": "query Q($pid:ID!){project(projectId:$pid){connections{id name projectId}}}",
                    "variables": {"pid": settings.cloudbeaver_project_id},
                }
            ).encode("utf-8"),
            headers={"Content-Type": "application/json"},
        ),
        timeout=30,
    ) as list_response:
        list_obj: CBResponse = json.loads(list_response.read().decode("utf-8"))

    return list_obj["data"]["project"]["connections"]
