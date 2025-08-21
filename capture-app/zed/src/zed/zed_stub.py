import threading
from pathlib import Path


class CaptureThread(threading.Thread):
    def __init__(
        self,
        stop_event: threading.Event,
        output_directory: Path,
        capture_interval: float,
    ):
        self._exception = None

    def exception(self):
        return self._exception
