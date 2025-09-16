from __future__ import annotations

import json
from typing import Any, Dict, Optional

from common.boto_clients import create_lambda_client
from pulumi import Input, ResourceOptions
from pulumi.dynamic import CreateResult, DiffResult, Resource, ResourceProvider, UpdateResult

from components.secret import Secret


class _DatabaseProvider(ResourceProvider):
    def create(self, props: Dict[str, Any]):
        create_lambda_client().invoke(
            FunctionName=props["function_arn"],
            InvocationType="RequestResponse",
            Payload=json.dumps({
                "op": "create",
                "name": props["name"],
                "password_secret_arn": props["password_secret_arn"],
            }).encode("utf-8"),
        )

        return CreateResult(
            id_=props["name"],
            outs={
                "function_arn": props["function_arn"],
                "name": props["name"],
                "password_secret_arn": props["password_secret_arn"],
            },
        )

    def diff(self, _id: str, olds: Dict[str, Any], news: Dict[str, Any]) -> DiffResult:
        replaces = [k for k in ("function_arn", "name") if olds.get(k) != news.get(k)]
        changes = bool(
            replaces
            or (olds.get("password_secret_arn") != news.get("password_secret_arn"))
            or (olds.get("password_secret_version_id") != news.get("password_secret_version_id"))
        )
        return DiffResult(changes=changes, replaces=replaces, delete_before_replace=False)

    def update(self, _id: str, olds: Dict[str, Any], news: Dict[str, Any]) -> UpdateResult:
        if olds.get("password_secret_arn") != news.get("password_secret_arn"):
            create_lambda_client().invoke(
                FunctionName=news["function_arn"],
                InvocationType="RequestResponse",
                Payload=json.dumps({
                    "op": "update",
                    "name": news["name"],
                    "password_secret_arn": news["password_secret_arn"],
                }).encode("utf-8"),
            )
        return UpdateResult(outs=news)

    def delete(self, _id: str, props: Dict[str, Any]):
        create_lambda_client().invoke(
            FunctionName=props["function_arn"],
            InvocationType="RequestResponse",
            Payload=json.dumps({"op": "delete", "name": props["name"]}).encode("utf-8"),
        )


class Database(Resource):
    def __init__(
        self,
        resource_name: str,
        database_manager_function_arn: Input[str],
        name: Input[str],
        password_secret: Secret,
        *,
        opts: Optional[ResourceOptions] = None,
    ):
        super().__init__(
            _DatabaseProvider(),
            resource_name,
            {
                "function_arn": database_manager_function_arn,
                "name": name,
                "password_secret_arn": password_secret.arn,
                "password_secret_version_id": password_secret.version_id,
            },
            opts,
        )
