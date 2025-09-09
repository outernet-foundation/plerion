import json
import re
import subprocess
from pathlib import Path
from re import Pattern
from shutil import rmtree

from fastapi import FastAPI


def dump_openapi(app: FastAPI, spec_path: Path):
    with spec_path.open("r", encoding="utf-8") as f:
        old_spec = f.read()

    new_spec = json.dumps(app.openapi(), indent=2)
    if new_spec == old_spec:
        return False

    spec_path.write_text(new_spec, encoding="utf-8")
    print(f"OpenAPI spec written to {spec_path}")

    return True


def generate_client(config_path: Path, spec_uri: Path, out_dir: Path):
    out_dir.mkdir(parents=True, exist_ok=True)

    # Remove existing Api, Models, and Client folders
    config = json.loads(config_path.read_text(encoding="utf-8"))
    if config["generatorName"] == "csharp":
        packageName = config["additionalProperties"]["packageName"]
        for subdir in ["Api", "Model", "Client"]:
            rmtree(Path(out_dir / "src" / packageName / subdir))

    subprocess.run(
        [
            "uv",
            "run",
            "openapi-generator-cli",
            "generate",
            "-i",
            spec_uri.resolve().as_posix(),
            "-o",
            out_dir.resolve().as_posix(),
            "--config",
            str(config_path.resolve()),
        ],
        check=True,
    )


def patch_commas(output_dir: Path) -> None:
    """
    Remove the stray trailing comma in class declarations like:
        public|internal [abstract|sealed] [partial] class Name : Base[, Interfaces...], {  // same line
        public|internal [abstract|sealed] [partial] class Name : Base[, Interfaces...],   // next line
        {
    Only touches lines that start a class declaration.
    """

    # Brace on the NEXT line:
    #   … class Name : Base, \n   {
    pat_next: Pattern[str] = re.compile(
        r"(?m)^(\s*(?:public|internal)(?:\s+(?:abstract|sealed))?(?:\s+partial)?\s+class\s+\w+\s*:[^{\r\n]+),\s*\r?\n(\s*)\{"
    )

    # Brace on the SAME line:
    #   … class Name : Base, {
    pat_same: Pattern[str] = re.compile(
        r"(?m)^(\s*(?:public|internal)(?:\s+(?:abstract|sealed))?(?:\s+partial)?\s+class\s+\w+\s*:[^{\r\n]+),\s*\{"
    )

    for cs_file in output_dir.rglob("*.cs"):
        text: str = cs_file.read_text(encoding="utf-8")
        fixed: str = pat_next.sub(r"\1\n\2{", text)
        fixed = pat_same.sub(r"\1 {", fixed)

        if fixed != text:
            cs_file.write_text(fixed, encoding="utf-8")
            print(f"Patched stray comma in {cs_file}")


def generate(app: FastAPI, dir: Path):
    spec_path = dir / "openapi.json"
    changed = dump_openapi(app, spec_path)

    if not changed:
        print("OpenAPI spec unchanged, skipping client generation")
        return

    for config in dir.glob("config_*.json"):
        client_name = config.stem[len("config_") :]
        output_dir = dir / client_name
        print(f"Generating client in {output_dir} using config {config}")

        generate_client(config, spec_path, output_dir)
        patch_commas(output_dir)
        print(f"Generated {client_name} client in {output_dir}")
