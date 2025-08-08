import json
from typing import Dict

from pulumi import Config, Output
from pulumi_aws.iam import Role


def create_github_actions_role(
    name: str, config: Config, github_oidc_provider_arn: Output[str], policies: Dict[str, Output[str]]
):
    return Role(
        name,
        assume_role_policy=github_oidc_provider_arn.apply(
            lambda arn: json.dumps({
                "Version": "2012-10-17",
                "Statement": [
                    {
                        "Effect": "Allow",
                        "Principal": {"Federated": arn},
                        "Action": "sts:AssumeRoleWithWebIdentity",
                        "Condition": {
                            "StringLike": {
                                "token.actions.githubusercontent.com:sub": (
                                    f"repo:{config.require('github-org')}/{config.require('github-repo')}:ref:refs/heads/main"
                                )
                            },
                            "StringEquals": {"token.actions.githubusercontent.com:aud": "sts.amazonaws.com"},
                        },
                    }
                ],
            })
        ),
        inline_policies=[{"name": policy[0], "policy": policy[1]} for policy in policies.items()],
    )


def create_ecr_policy(repository_arn: Output[str]):
    return repository_arn.apply(
        lambda arn: json.dumps({
            "Version": "2012-10-17",
            "Statement": [
                {"Effect": "Allow", "Action": ["ecr:GetAuthorizationToken"], "Resource": "*"},
                {
                    "Effect": "Allow",
                    "Action": [
                        "ecr:BatchCheckLayerAvailability",
                        "ecr:GetDownloadUrlForLayer",
                        "ecr:BatchGetImage",
                        "ecr:InitiateLayerUpload",
                        "ecr:UploadLayerPart",
                        "ecr:CompleteLayerUpload",
                        "ecr:PutImage",
                    ],
                    "Resource": arn,
                },
            ],
        })
    )


def create_secrets_manager_policy(secret_arn: Output[str]):
    return secret_arn.apply(
        lambda arn: json.dumps({
            "Version": "2012-10-17",
            "Statement": [{"Effect": "Allow", "Action": ["secretsmanager:GetSecretValue"], "Resource": [arn]}],
        })
    )


def create_pullthrough_cache_policy(repository_arn: Output[str]):
    return repository_arn.apply(
        lambda arn: json.dumps({
            "Version": "2012-10-17",
            "Statement": [{"Effect": "Allow", "Action": ["ecr:BatchImportUpstreamImage"], "Resource": [arn]}],
        })
    )
