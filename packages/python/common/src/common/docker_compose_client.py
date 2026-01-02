import re
from enum import StrEnum
from http.client import HTTPConnection

from common.run_command import run_command
from docker.models.containers import Container

from docker import from_env

_CONTAINER_ID_RE = re.compile(r"\b[0-9a-f]{12,64}\b")


def create_service(service_name: str, container_name: str, environment: dict[str, str]):
    environment_string = " ".join(f"-e {key}='{value}'" for key, value in environment.items())

    stdout = run_command(
        f"docker compose -f /compose.yml run -d --no-deps --name {container_name} {environment_string} {service_name}",
        log=True,
    )

    return _parse_container_id(stdout)


def destroy_service(container_id: str):
    container: Container | None = None
    try:
        container = from_env().containers.get(container_id)
    except Exception:
        # Already removed
        return

    container.stop(timeout=10)

    # Don't remove the container, we want to keep logs around for debugging
    # container.remove(force=True)


class ServiceStatus(StrEnum):
    UNKNOWN = "unknown"
    CREATED = "created"
    RESTARTING = "restarting"
    RUNNING = "running"
    REMOVING = "removing"
    PAUSED = "paused"
    EXITED = "exited"
    DEAD = "dead"
    READY = "ready"


def get_service_status(container_id: str, session_port: int) -> ServiceStatus:
    container = from_env().containers.get(container_id)
    container.reload()
    assert container.name is not None

    status: str | None = container.attrs.get("State", {}).get("Status")

    match status:
        case None:
            return ServiceStatus.UNKNOWN
        case "created":
            return ServiceStatus.CREATED
        case "restarting":
            return ServiceStatus.RESTARTING
        case "removing":
            return ServiceStatus.REMOVING
        case "paused":
            return ServiceStatus.PAUSED
        case "exited":
            return ServiceStatus.EXITED
        case "dead":
            return ServiceStatus.DEAD
        case "running":
            try:
                conn = HTTPConnection(container.name, session_port, timeout=2.0)
                conn.request("GET", "/health")
                health_status = conn.getresponse().status
                conn.close()
                if health_status < 500:
                    return ServiceStatus.READY
            except Exception:
                pass
            return ServiceStatus.RUNNING

        case _:
            return ServiceStatus.UNKNOWN


def _parse_container_id(stdout: str):
    # docker compose run -d prints the container id (often the only line)
    m = _CONTAINER_ID_RE.search(stdout.strip().splitlines()[-1] if stdout.strip() else "")
    if not m:
        # fall back to searching all output
        m = _CONTAINER_ID_RE.search(stdout)
    if not m:
        raise RuntimeError(f"Failed to parse container id from output:\n{stdout}")
    return m.group(0)
