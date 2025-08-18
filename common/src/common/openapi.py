import json
import re
import subprocess
from pathlib import Path
from re import Pattern

from fastapi import FastAPI


def dump_openapi(app: FastAPI, spec_path: Path):
    spec = app.openapi()
    spec_path.write_text(json.dumps(spec, indent=2), encoding="utf-8")
    print(f"OpenAPI spec written to {spec_path}")


def generate_client(config_path: Path, spec_uri: Path, out_dir: Path):
    out_dir.mkdir(parents=True, exist_ok=True)
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
    config_path = dir / "config.json"
    spec_path = dir / "openapi.json"
    output_dir = dir / "unity"
    dump_openapi(app, spec_path)
    generate_client(config_path, spec_path, output_dir)
    patch_commas(output_dir)
    print("Client generation complete.")
