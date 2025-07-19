import json

import pulumi
import pulumi_aws as aws
import pulumi_awsx as awsx
from pulumi import Config, Output
from pulumi_awsx.ecs import FargateService


def create_minio(
    config: Config,
    s3_bucket: aws.s3.Bucket,
) -> Output[str]:
    """
    Create MinIO S3 gateway service with automatic load balancer.

    Returns the URL to access MinIO.
    """

    # Create task role with S3 access for MinIO
    task_role = aws.iam.Role(
        "minio-task-role",
        assume_role_policy="""{
            "Version": "2012-10-17",
            "Statement": [
                {
                    "Effect": "Allow",
                    "Principal": {"Service": "ecs-tasks.amazonaws.com"},
                    "Action": "sts:AssumeRole"
                }
            ]
        }""",
    )

    # Grant S3 access to MinIO task
    aws.iam.RolePolicy(
        "minio-s3-access",
        role=task_role.id,
        policy=s3_bucket.arn.apply(
            lambda arn: json.dumps(
                {
                    "Version": "2012-10-17",
                    "Statement": [
                        {
                            "Effect": "Allow",
                            "Action": [
                                "s3:GetObject",
                                "s3:PutObject",
                                "s3:DeleteObject",
                                "s3:ListBucket",
                            ],
                            "Resource": [arn, f"{arn}/*"],
                        }
                    ],
                }
            )
        ),
    )

    # Create an Application Load Balancer
    load_balancer = awsx.lb.ApplicationLoadBalancer(
        "minio-lb",
        listeners=[
            {
                "port": 80,  # Listen on port 80 for incoming requests
                "protocol": "HTTP",
            },
            {
                "port": 9001,  # Web console
                "protocol": "HTTP",
            },
        ],
        default_target_group={
            "port": 9000,  # Target MinIO on port 9000
            "protocol": "HTTP",
            "health_check": {
                "enabled": True,
                "protocol": "HTTP",
                "path": "/minio/health/live",
                "interval": 30,
                "timeout": 5,
                "healthy_threshold": 2,
                "unhealthy_threshold": 2,
                "matcher": "200",
                "port": "traffic-port",
            },
        },
    )

    # Create an ECS cluster for MinIO
    cluster = aws.ecs.Cluster("minio-cluster")

    # MinIO Service with automatic load balancer integration
    minio = FargateService(
        "minioTool",
        cluster=cluster.arn,
        assign_public_ip=True,
        task_definition_args={
            "task_role": {"role_arn": task_role.arn},
            "container": {
                "name": "minio",
                "image": "minio/minio:latest",
                "cpu": 256,
                "memory": 512,
                "essential": True,
                "command": [
                    "server",
                    "/data",
                    "--address",
                    ":9000",
                    "--console-address",
                    ":9001",
                ],
                "environment": [
                    {
                        "name": "MINIO_ROOT_USER",
                        "value": config.require("minioAccessKey"),
                    },
                    {
                        "name": "MINIO_ROOT_PASSWORD",
                        "value": config.require_secret("minioSecretKey"),
                    },
                    {"name": "MINIO_DOMAIN", "value": "s3.amazonaws.com"},
                ],
                "port_mappings": [
                    {
                        "container_port": 9000,
                        "host_port": 9000,
                        "target_group": load_balancer.default_target_group,
                    },
                ],
            },
        },
        desired_count=1,
    )

    # Get the load balancer URL
    minio_url = load_balancer.load_balancer.dns_name.apply(lambda dns: f"http://{dns}")

    # Export URLs and service info
    pulumi.export("minioServiceName", minio.service.name)
    pulumi.export("minioUrl", minio_url)

    return minio_url
