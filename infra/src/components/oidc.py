import json

from pulumi import Config
from pulumi_aws.iam import OpenIdConnectProvider, Role, RolePolicyAttachment


def create_oidc(config: Config):
    github_oidc = OpenIdConnectProvider(
        "githubOidcProvider",
        url="https://token.actions.githubusercontent.com",
        client_id_lists=["sts.amazonaws.com"],
        thumbprint_lists=["6938fd4d98bab03faadb97b34396831e3780aea1"],
    )

    oidc_role = Role(
        "githubActionsRole",
        assume_role_policy=github_oidc.arn.apply(
            lambda arn: json.dumps({
                "Version": "2012-10-17",
                "Statement": [
                    {
                        "Effect": "Allow",
                        "Principal": {"Federated": arn},
                        "Action": "sts:AssumeRoleWithWebIdentity",
                        "Condition": {
                            "StringLike": {
                                "token.actions.githubusercontent.com:sub": f"repo:{config.require('github-repo')}:ref:refs/heads/{config.require('github-branch')}"
                            }
                        },
                    }
                ],
            })
        ),
    )

    RolePolicyAttachment("pulumiAccess", role=oidc_role.name, policy_arn="arn:aws:iam::aws:policy/PowerUserAccess")
