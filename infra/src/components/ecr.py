from pulumi import Output
from pulumi_aws.ecr import Repository, get_image_output


def repo_digest(repo: Repository):
    return Output.concat(
        repo.repository_url, "@", get_image_output(repository_name=repo.name, image_tag="latest").image_digest
    )
