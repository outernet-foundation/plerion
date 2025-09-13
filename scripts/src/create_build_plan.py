from __future__ import annotations

import argparse
import json
import os
import shlex
import tempfile
from pathlib import Path
from typing import Literal, TypedDict, cast

from common.run_command import run_command

workspace_directory = Path("..").resolve()
images_path = workspace_directory / "images.json"
images_lock_path = workspace_directory / "images-lock.json"
infrastructure_directory = workspace_directory / "infra"


class Image(TypedDict):
    stack: str
    first_party: bool
    context: str
    dockerfile: str
    hash_paths: list[str]
    tag: str


class ImageLock(TypedDict):
    digest: str
    tags: list[str]


class FirstPartyPlan(TypedDict):
    name: str
    kind: Literal["first_party"]
    stack: str
    repo: str
    context: str
    dockerfile: str
    tree_sha: str


class ThirdPartyPlan(TypedDict):
    name: str
    kind: Literal["third_party"]
    stack: str
    repo: str


def create_image_build_plan(
    image_name: str, image: Image, image_lock: ImageLock | None
) -> FirstPartyPlan | ThirdPartyPlan | None:
    if image["first_party"] and (not image.get("context") or not image.get("dockerfile")):
        raise ValueError(f"first_party image '{image_name}' requires 'context' and 'dockerfile'")

    stack = image["stack"]

    image_repo = run_command(
        f"pulumi stack output --stack {stack} {image_name}-image-repo-url", cwd=infrastructure_directory
    ).strip()

    if not image["first_party"]:
        if image_lock is not None:
            return

        return {"name": image_name, "kind": "third_party", "stack": stack, "repo": image_repo}

    context = image["context"]

    with tempfile.TemporaryDirectory() as temporary_directory:
        environment = os.environ.copy()
        environment["GIT_INDEX_FILE"] = str(Path(temporary_directory) / "index")
        environment["GIT_OBJECT_DIRECTORY"] = str(Path(temporary_directory) / "objects")
        Path(environment["GIT_OBJECT_DIRECTORY"]).mkdir(parents=True, exist_ok=True)
        quoted_paths_string = " ".join(
            shlex.quote(path) for path in (image["hash_paths"] if "hash_paths" in image else [context])
        )
        run_command(f"git add -A -- {quoted_paths_string}", env=environment, cwd=workspace_directory)
        tree_sha_value = run_command("git write-tree", env=environment, cwd=workspace_directory).strip()

    if image_lock is not None and tree_sha_value == next(
        (tag[5:] for tag in image_lock["tags"] if tag.startswith("tree-")), None
    ):
        return

    return {
        "name": image_name,
        "kind": "first_party",
        "stack": stack,
        "repo": image_repo,
        "context": str((workspace_directory / context).resolve()),
        "dockerfile": str((workspace_directory / context / image["dockerfile"]).resolve()),
        "tree_sha": tree_sha_value,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Create a build plan JSON. Includes only first-party images needing rebuild (missing a lock entry, or tree hash differs) and third-party images missing a lock entry."
    )
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--image", action="append", help="Image name; can be repeated.")
    group.add_argument("--images", nargs="+", help="Space-separated list of image names.")
    group.add_argument("-all", "--all", dest="all_", action="store_true", help="Plan for all images in images.json.")
    arguments = parser.parse_args()

    if not images_path.is_file():
        raise FileNotFoundError(f"Images file not found: {images_path}")

    images = cast(dict[str, Image], json.load(images_path.open("r", encoding="utf-8")))

    if arguments.all_:
        selected_images = list(images.keys())
    elif arguments.image:
        selected_images = list(arguments.image)
    elif arguments.images:
        selected_images = list(arguments.images)
    else:
        raise RuntimeError("Unreachable")

    images_lock = (
        cast(dict[str, ImageLock], json.load(images_lock_path.open("r", encoding="utf-8")))
        if images_lock_path.is_file()
        else {}
    )

    plan: list[FirstPartyPlan | ThirdPartyPlan] = []
    for image_name in selected_images:
        plan_entry = create_image_build_plan(image_name, images[image_name], images_lock.get(image_name))
        if plan_entry is not None:
            plan.append(plan_entry)
