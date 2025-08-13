import json
from pathlib import Path
from typing import Optional

from pulumi import Output
from pulumi_aws import get_caller_identity, get_region_output
from pulumi_aws.ecr import Repository


def repo_digest(
    repo: Repository, lock_path: str = "infra/image-lock.json", fallback_tag: str = ":latest"
) -> Output[str]:
    def pick(url: str, name: str) -> str:
        try:
            data = json.loads(Path(lock_path).read_text())
            digest: Optional[str] = data.get(name) or data.get("repositories", {}).get(name, {}).get("digest")
            if digest:
                return f"{url}@{digest}"
        except FileNotFoundError:
            # first run: CI/CD hasn't written the lock yet
            pass
        # safe fallback — will not be pulled if desired_count == 0
        return f"{url}{fallback_tag}"

    return Output.all(repo.repository_url, repo.name).apply(lambda t: pick(*t))


def pullthrough_repo_digest(repo: Repository):
    return Output.concat(
        get_caller_identity().account_id,
        ".dkr.ecr.",
        get_region_output().region,
        ".amazonaws.com/",
        repo.name,
        ":latest",
    )
