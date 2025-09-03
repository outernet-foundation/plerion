# components/ecr.py
from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Dict, List, TypedDict, cast

from pulumi import ComponentResource, Input, ResourceOptions
from pulumi_aws import ecr
from pulumi_aws.ecr import get_repository_output


class Repository(ComponentResource):
    def __init__(
        self,
        resource_name: str,
        name: Input[str],
        *,
        adopt: bool = False,
        force_delete: bool | None = None,
        opts: ResourceOptions | None = None,
    ):
        super().__init__("custom:Repository", resource_name, opts=opts)

        self._resource_name = resource_name
        self._child_opts = ResourceOptions.merge(opts, ResourceOptions(parent=self))

        if adopt:
            self._repo = get_repository_output(name=name)
        else:
            self._repo = ecr.Repository(resource_name, name=name, force_delete=force_delete, opts=self._child_opts)

        self.resource_name = resource_name

        self.name = name
        self.arn = self._repo.arn
        self.url = self._repo.repository_url

        self.register_outputs({"arn": self.arn, "url": self.url})

    class RepoEntry(TypedDict, total=False):
        digest: str
        tags: List[str]  # optional metadata; not used for resolution

    def locked_digest(self, lock_path: str = "image-lock.json"):
        project_root = Path(os.getcwd())
        while project_root != project_root.parent:
            if (project_root / "Pulumi.yaml").exists():
                break
            project_root = project_root.parent

        lock_file = project_root / lock_path

        if not lock_file.exists():
            raise FileNotFoundError(f"Lock file not found: {lock_file}")

        data_raw: object = json.loads(lock_file.read_text())
        data_map: Dict[str, object] = cast(Dict[str, object], data_raw)
        container_raw: object = data_map.get("repositories", data_map)
        container: Dict[str, object] = cast(Dict[str, object], container_raw)
        entry_raw: object = container.get(self._name)

        if not isinstance(entry_raw, dict):
            raise ValueError(f"Invalid entry in lock file for {self._name}: {entry_raw}")

        entry_map: Dict[str, object] = cast(Dict[str, object], entry_raw)
        digest: object = entry_map.get("digest")

        return self._repo.repository_url.apply(lambda url: f"{url}@{digest}")
