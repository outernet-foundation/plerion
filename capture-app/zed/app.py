import pathlib, shutil, datetime
import threading
from typing import List, Optional
from litestar import Litestar, post, get, Response
from litestar.response import Stream
from litestar.exceptions import HTTPException
from anyio.to_thread import run_sync
from capture import CaptureThread

CAPTURES_DIRECTORY = pathlib.Path.home() / "captures"
DEFAULT_CAPTURE_INTERVAL = 0.5
CHUNK_SIZE = 64 * 1024

stop_event = threading.Event()
capture_thread: Optional[CaptureThread] = None

@post("/start")
async def start_capture(
    capture_name: str | None = None, 
    capture_interval: float | None = None
) -> dict:
    global capture_thread, stop_event

    print(CAPTURES_DIRECTORY)
    
    if capture_thread and capture_thread.is_alive():
        raise HTTPException(detail="capture already running", status_code=409)
    
    CAPTURES_DIRECTORY.mkdir(parents=True, exist_ok=True)
    capture_name = capture_name or datetime.datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
    capture_directory = CAPTURES_DIRECTORY / capture_name

    if capture_directory.exists():
        raise HTTPException(f"capture named {capture_name} already exists", status_code=409)

    stop_event = threading.Event()
    capture_thread = CaptureThread(
        stop_event, 
        CAPTURES_DIRECTORY / capture_name, 
        capture_interval or DEFAULT_CAPTURE_INTERVAL
    ) 
    capture_thread.start()

    return {
        "status": "started",
        "capture_name": capture_name
    }

@post("/stop")
async def stop_capture() -> dict:
    global capture_thread, stop_event

    if not (capture_thread and capture_thread.is_alive()):
        return {"status":"not_running"}
    
    stop_event.set()
    await run_sync(capture_thread.join)

    if capture_thread.exception():
        raise HTTPException(f"capture error: {capture_thread.exception()}", status_code=500)
    
    capture_thread = None

    return {"status":"stopped"}

@get("/captures")
async def list_captures() -> List[str]:
    return sorted([capture.name for capture in CAPTURES_DIRECTORY.glob("*") if capture.is_dir()])

@get("/capture/{capture_name:str}")
async def get_capture(capture_name: str) -> Response:
    folder = CAPTURES_DIRECTORY / capture_name
    if not folder.exists():
        raise HTTPException("capture not found", status_code=404)

    zip_path = folder.with_suffix(".zip")
    shutil.make_archive(str(folder), "zip", folder)

    async def stream():
        with open(zip_path, "rb") as f:
            while chunk := f.read(CHUNK_SIZE):
                yield chunk
        zip_path.unlink(missing_ok=True)

    return Stream(
        stream(), 
        media_type = "application/zip", 
        headers = {
            "Content-Disposition": f'attachment; filename="{zip_path.name}"'
        }
    )

app = Litestar(
    route_handlers=[
        start_capture, 
        stop_capture, 
        list_captures, 
        get_capture
    ]
)

