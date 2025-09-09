from typing import TYPE_CHECKING, Any, cast

import boto3
from botocore.config import Config

if TYPE_CHECKING:
    from mypy_boto3_batch import BatchClient
    from mypy_boto3_ecs import ECSClient
    from mypy_boto3_lambda import LambdaClient
    from mypy_boto3_s3 import S3Client
    from mypy_boto3_secretsmanager import SecretsManagerClient
else:
    BatchClient = Any
    ECSClient = Any
    S3Client = Any
    SecretsManagerClient = Any
    LambdaClient = Any


def create_batch_client() -> BatchClient:
    return cast(BatchClient, boto3.client("batch", region_name="us-east-1"))  # type: ignore[call-arg]


def create_ecs_client() -> ECSClient:
    return cast(
        ECSClient,
        boto3.client(  # pyright: ignore[reportUnknownMemberType]
            "ecs",
            region_name="us-east-1",
            config=Config(
                connect_timeout=10,
                read_timeout=910,
                tcp_keepalive=True,
                retries={"max_attempts": 3, "mode": "standard"},
            ),
        ),
    )  # type: ignore[call-arg]


def create_lambda_client() -> LambdaClient:
    return cast(
        LambdaClient,
        boto3.client(  # pyright: ignore[reportUnknownMemberType]
            "lambda",
            region_name="us-east-1",
            config=Config(
                connect_timeout=10,
                read_timeout=910,
                tcp_keepalive=True,
                retries={"max_attempts": 3, "mode": "standard"},
            ),
        ),
    )  # type: ignore[call-arg]


def create_s3_client() -> S3Client:
    return cast(S3Client, boto3.client("s3", region_name="us-east-1"))  # type: ignore[call-arg]


def create_secretsmanager_client() -> SecretsManagerClient:
    return cast(SecretsManagerClient, boto3.client("secretsmanager", region_name="us-east-1"))  # type: ignore[call-arg]
