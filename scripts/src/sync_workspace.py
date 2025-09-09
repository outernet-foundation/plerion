#!/usr/bin/env python3
from __future__ import annotations

import threading
import time
from pathlib import Path
from typing import Any, List, Optional, Sequence, Set, cast

from common.run_command import run_command
from watchdog.events import FileSystemEvent, PatternMatchingEventHandler
from watchdog.observers import Observer

log = False
root = Path.cwd().resolve().parent
solution_name = root.name.capitalize()
solution = f"{solution_name}.sln"
solution_path = root / solution

IGNORE_DIRNAMES = {".git", ".venv", "__pycache__", "bin", "obj", "openapi_generated"}


# This is not working, and I have no idea why. If the openapi generator every generates
# python files in a subdirectory, this will need to be fixed.
def make_watch_ignore_patterns() -> list[str]:
    pats: list[str] = []
    for name in IGNORE_DIRNAMES:
        pats.append(f"*/{name}/*")  # POSIX-style
        pats.append(f"*\\{name}\\*")  # Windows-style
    return pats


WATCH_IGNORE_PATTERNS = make_watch_ignore_patterns()


def main() -> None:
    # Sync all pyproject.toml directories
    print("Syncing python projects")
    for path in root.rglob("pyproject.toml"):
        if ignored(path):
            continue
        sync_uv(path.parent)

    # Run code generation for all openapi projects
    print("Syncing OpenAPI clients")
    openapi_project_paths = [path.parent.parent for path in root.rglob("generate_openapi.py")]
    for path in openapi_project_paths:
        generate_openapi(path)

    # Sync C# solution
    print("Syncing C# solution")

    # Create C# solution if it doesn't exist
    if not solution_path.exists():
        run_command(f"dotnet new sln -n {solution_name}", cwd=root, log=log)
        print(f"Created solution {solution_path}")

    # Get existing projects in the solution
    out = run_command(f"dotnet sln {solution} list", cwd=root, log=log)
    existing_projects: Set[Path] = set()
    for line in out.splitlines():
        line = line.strip()
        if not line or ".csproj" not in line:
            continue
        if set(line) <= {"-", " "}:
            continue
        existing_projects.add((root / line).resolve())

    # Find all .csproj files in the directory
    desired_projects: List[Path] = []
    for path in root.rglob("*.csproj"):
        if ignored(path):
            continue
        desired_projects.append(path.resolve())
    desired = set(desired_projects)

    # Apply changes to the solution
    apply_project_changes(sorted(desired - existing_projects), sorted(existing_projects - desired))

    # Start file watchers
    print("Workspace synchronized, watching for changes...")
    observer = Observer()
    observer.schedule(CsprojHandler(), str(root), recursive=True)
    observer.schedule(PyprojectHandler(), str(root), recursive=True)
    for path in openapi_project_paths:
        observer.schedule(OpenApiHandler(path), str(path), recursive=True)
    observer.daemon = True
    observer.start()

    # Keep the main thread alive
    try:
        while True:
            time.sleep(1.0)
    except KeyboardInterrupt:
        observer.stop()
    observer.join()


class DebounceHandler(PatternMatchingEventHandler):
    def __init__(
        self, project_root: Path, pattern: List[str], ignore_patterns: List[str], debounce_seconds: float = 0.4
    ) -> None:
        super().__init__(
            patterns=pattern, ignore_patterns=ignore_patterns, ignore_directories=True, case_sensitive=False
        )
        self.project_root = project_root
        self.debounce_seconds = debounce_seconds
        self.lock = threading.Lock()
        self._timer: Optional[threading.Timer] = None

    def _on_created(self, event: FileSystemEvent):
        self._schedule(f"Created: {event.src_path}", cast(str, event.src_path))

    def _on_modified(self, event: FileSystemEvent):
        self._schedule(f"Modified: {event.src_path}", cast(str, event.src_path))

    def _on_deleted(self, event: FileSystemEvent):
        self._schedule(f"Deleted: {event.src_path}", cast(str, event.src_path))

    def _on_moved(self, event: FileSystemEvent):
        self._schedule(f"Moved: {event.src_path} -> {cast(Any, event).dest_path}", cast(Any, event).dest_path)

    def _schedule(self, message: str, path: str):
        if self._timer is not None:
            self._timer.cancel()
        self._timer = threading.Timer(self.debounce_seconds, self._run_internal, args=(message, path))
        self._timer.daemon = True
        self._timer.start()

    def _run_internal(self, message: str, path: str):
        with self.lock:
            print(message)
            self._run(message, path)
            print("Workspace synchronized, watching for changes...")

    def _run(self, message: str, path: str):
        pass  # To be implemented by subclasses


class PyprojectHandler(DebounceHandler):
    def __init__(self) -> None:
        super().__init__(project_root=root, pattern=["**/pyproject.toml"], ignore_patterns=WATCH_IGNORE_PATTERNS)

    def _run(self, message: str, path: str):
        sync_uv(Path(path).parent)

    def on_created(self, event: FileSystemEvent):
        self._on_created(event)

    def on_modified(self, event: FileSystemEvent):
        self._on_modified(event)


class OpenApiHandler(DebounceHandler):
    def __init__(self, project_root: Path) -> None:
        super().__init__(project_root=project_root, pattern=["**/*.py"], ignore_patterns=WATCH_IGNORE_PATTERNS)

    def _run(self, message: str, path: str):
        generate_openapi(self.project_root)

    def on_created(self, event: FileSystemEvent):
        self._on_created(event)

    def on_modified(self, event: FileSystemEvent):
        self._on_modified(event)

    def on_deleted(self, event: FileSystemEvent):
        self._on_deleted(event)

    def on_moved(self, event: FileSystemEvent):
        self._on_moved(event)


class CsprojHandler(DebounceHandler):
    def __init__(self) -> None:
        super().__init__(project_root=root, pattern=["*.csproj"], ignore_patterns=WATCH_IGNORE_PATTERNS)

    def _run(self, message: str, path: str):
        update_solution(None, path)

    def on_created(self, event: FileSystemEvent):
        self._on_created(event)

    def on_deleted(self, event: FileSystemEvent):
        self._on_deleted(event)

    def on_moved(self, event: FileSystemEvent):
        self._on_moved(event)


def sync_uv(path: Path):
    print(f"Syncing: {path}")
    run_command("uv sync", cwd=path, log=log)


def generate_openapi(path: Path):
    print(f"Syncing: {path}")
    run_command("uv run -m src.generate_openapi", cwd=path, log=log)


def ignored(path: Path):
    parts = {p.lower() for p in path.parts}
    return any(name.lower() in parts for name in IGNORE_DIRNAMES)


def update_solution(src: Optional[str], dest: Optional[str]):
    apply_project_changes(
        [Path(dest)] if dest and dest.endswith(".csproj") else [],
        [Path(src)] if src and src.endswith(".csproj") else [],
    )


def apply_project_changes(adds: Sequence[Path], removes: Sequence[Path]):
    if adds:
        print(f"Adding: {', '.join(map(str, adds))}")
        apply_project_change("add", adds)
    if removes:
        print(f"Removing: {', '.join(map(str, removes))}")
        apply_project_change("remove", removes)


def apply_project_change(action: str, paths: Sequence[Path]):
    resolved_paths: List[str] = []
    for path in [path.resolve() for path in paths]:
        try:
            resolved_paths.append(str(path.relative_to(root)))
        except Exception:
            resolved_paths.append(str(path))

    run_command(f"dotnet sln {solution} {action} " + " ".join(f'"{p}"' for p in resolved_paths), cwd=root, log=log)


if __name__ == "__main__":
    main()
