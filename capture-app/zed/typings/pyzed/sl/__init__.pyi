# typings/pyzed/sl/__init__.pyi
from __future__ import annotations

from enum import Enum
from typing import Protocol, Tuple

import numpy as np
from numpy.typing import NDArray

class ERROR_CODE(Enum):
    SUCCESS = ...

class RESOLUTION(Enum):
    HD720 = ...

class UNIT(Enum):
    METER = ...

class VIEW(Enum):
    LEFT = ...
    RIGHT = ...

class MEM(Enum):
    CPU = ...

class REFERENCE_FRAME(Enum):
    WORLD = ...

class VIDEO_SETTINGS(Enum):
    AEC_AGC = ...
    GAIN = ...
    EXPOSURE = ...

class ImageSize(Protocol):
    width: int
    height: int

class CameraParameters(Protocol):
    fx: float
    fy: float
    cx: float
    cy: float
    image_size: ImageSize

class Transform(Protocol):
    @property
    def m(self) -> NDArray[np.float64]: ...

class CalibrationParameters(Protocol):
    left_cam: CameraParameters
    right_cam: CameraParameters
    stereo_transform: Transform

class CameraConfiguration(Protocol):
    calibration_parameters: CalibrationParameters

class CameraInformation(Protocol):
    camera_configuration: CameraConfiguration

class InitParameters:
    def __init__(
        self,
        *,
        camera_resolution: RESOLUTION | None = ...,
        coordinate_units: UNIT | None = ...,
        camera_fps: int | None = ...,
    ) -> None: ...

class Translation:
    def get(self) -> Tuple[float, float, float]: ...

class Orientation:
    def get(self) -> Tuple[float, float, float, float]: ...

class Timestamp:
    def get_milliseconds(self) -> int: ...

class Pose:
    timestamp: Timestamp
    def get_translation(self, t: Translation) -> Translation: ...
    def get_orientation(self, o: Orientation) -> Orientation: ...

class RuntimeParameters: ...

class PositionalTrackingParameters:
    enable_imu_fusion: bool
    set_floor_as_origin: bool
    def __init__(self) -> None: ...

class Mat:
    def write(self, path: str) -> bool: ...
    def get_data(self) -> NDArray[np.uint8]: ...

class Camera:
    def open(self, py_init: InitParameters | None = ...) -> ERROR_CODE: ...
    def close(self) -> None: ...
    def get_camera_information(
        self, resizer: None | object = ...
    ) -> CameraInformation: ...
    def enable_positional_tracking(
        self, py_tracking: PositionalTrackingParameters | None = ...
    ) -> ERROR_CODE: ...
    def disable_positional_tracking(self) -> None: ...
    def grab(self, py_runtime: RuntimeParameters | None = ...) -> ERROR_CODE: ...
    def get_position(
        self, py_pose: Pose, reference_frame: REFERENCE_FRAME = ...
    ) -> object: ...
    def retrieve_image(
        self,
        py_mat: Mat,
        view: VIEW = ...,
        mem_type: MEM = ...,
        resolution: object | None = ...,
    ) -> ERROR_CODE: ...
    def set_camera_settings(
        self, setting: VIDEO_SETTINGS, value: float | int
    ) -> ERROR_CODE: ...
