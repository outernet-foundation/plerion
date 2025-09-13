import subprocess
from pathlib import Path
from shlex import split


def run_command(command: str, cwd: Path | None = None, env: dict[str, str] | None = None):
    print(f"Running command: {command}")

    try:
        process = subprocess.run(
            split(command, posix=True), cwd=cwd, env=env, check=True, text=True, capture_output=True
        )
        if process.returncode != 0:
            raise subprocess.CalledProcessError(
                process.returncode, command, output=process.stdout, stderr=process.stderr
            )
        if process.stdout:
            print(process.stdout)
        if process.stderr:
            print(process.stderr)
        return process.stdout
    except subprocess.CalledProcessError as e:
        print(f"Command failed with exit code {e.returncode}")
        if e.output:
            print(e.output)
        if e.stderr:
            print(e.stderr)
        raise
