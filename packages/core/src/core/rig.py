from typing import Annotated, Literal, Union

from numpy import ndarray
from pydantic import BaseModel, Discriminator

from .transform import Quaternion, Vector3


class CameraConfigBase(BaseModel):
    width: int
    height: int
    mirroring: Literal["None", "X", "Y"]
    rotation: Literal["None", "90_CW", "180", "90_CCW"]


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


class Config(BaseModel):
    rigs: list[RigConfig]


class Transform:
    def __init__(self, rotation: ndarray, translation: ndarray):
        self.rotation = rotation
        self.translation = translation
