from typing import Annotated, Literal, Union

from core.axis_convention import AxisConvention
from pydantic import BaseModel, Discriminator

from .classes import Quaternion, Vector3

# See "Orientation" property here: https://exiv2.org/tags-xmp-tiff.html
ImageOrientation = Literal[
    "TOP_LEFT", "TOP_RIGHT", "BOTTOM_RIGHT", "BOTTOM_LEFT", "LEFT_TOP", "RIGHT_TOP", "RIGHT_BOTTOM", "LEFT_BOTTOM"
]


class CameraConfigBase(BaseModel):
    width: int
    height: int
    orientation: ImageOrientation


class PinholeCameraConfig(CameraConfigBase):
    model: Literal["PINHOLE"]
    fx: float
    fy: float
    cx: float
    cy: float


class OpenCVCameraConfig(CameraConfigBase):
    model: Literal["OPENCV"]
    fx: float
    fy: float
    cx: float
    cy: float
    k1: float
    k2: float
    p1: float
    p2: float
    k3: float


class FullOpenCVCameraConfig(CameraConfigBase):
    model: Literal["FULL_OPENCV"]
    fx: float
    fy: float
    cx: float
    cy: float
    k1: float
    k2: float
    p1: float
    p2: float
    k3: float
    k4: float
    k5: float
    k6: float


CameraConfig = Annotated[Union[PinholeCameraConfig, OpenCVCameraConfig, FullOpenCVCameraConfig], Discriminator("model")]


class RigCameraConfig(BaseModel):
    id: str
    ref_sensor: bool | None
    rotation: Quaternion
    translation: Vector3
    camera_config: CameraConfig


class RigConfig(BaseModel):
    id: str
    cameras: list[RigCameraConfig]


class CaptureSessionManifest(BaseModel):
    axis_convention: AxisConvention
    rigs: list[RigConfig]
