from __future__ import annotations

import json
from typing import Any, Dict, Optional

from common.boto_clients import create_lambda_client
from pulumi import Input, ResourceOptions
from pulumi.dynamic import CreateResult, DiffResult, Resource, ResourceProvider, UpdateResult

from components.secret import Secret


class _DatabaseProvider(ResourceProvider):
    def _invoke_lambda(self, function_arn: str, payload: Dict[str, Any]) -> None:
        response = create_lambda_client().invoke(
            FunctionName=function_arn, InvocationType="RequestResponse", Payload=json.dumps(payload).encode("utf-8")
        )
        if response.get("FunctionError"):
            error_payload = json.loads(response["Payload"].read().decode("utf-8"))
            raise RuntimeError(f"Lambda invocation failed: {error_payload}")

    def create(self, props: Dict[str, Any]):
        self._invoke_lambda(
            function_arn=props["function_arn"],
            payload={"op": "create", "name": props["name"], "password_secret_arn": props["password_secret_arn"]},
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
            self._invoke_lambda(
                function_arn=news["function_arn"],
                payload={"op": "update", "name": news["name"], "password_secret_arn": news["password_secret_arn"]},
            )
        return UpdateResult(outs=news)

    def delete(self, _id: str, props: Dict[str, Any]):
        self._invoke_lambda(function_arn=props["function_arn"], payload={"op": "delete", "name": props["name"]})


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
