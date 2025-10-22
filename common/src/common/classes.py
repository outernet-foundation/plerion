from __future__ import annotations

from dataclasses import dataclass
from typing import Annotated, Literal, TypedDict, Union

from fastapi import File, Form, UploadFile
from pydantic import Discriminator, Json


class Point3D(TypedDict):
    x: float
    y: float
    z: float


class Color(TypedDict):
    r: int
    g: int
    b: int


class PinholeIntrinsics(TypedDict):
    model: Literal["PINHOLE"]
    width: int
    height: int
    fx: float
    fy: float
    cx: float
    cy: float


class OpenCVRadTanIntrinsics(TypedDict):
    model: Literal["OPENCV"]
    width: int
    height: int
    fx: float
    fy: float
    cx: float
    cy: float
    k1: float
    k2: float
    p1: float
    p2: float
    k3: float | None


class GenericParamsIntrinsics(TypedDict):
    model: Literal["GENERIC"]
    width: int
    height: int
    params: list[float]


CameraIntrinsics = Annotated[
    Union[PinholeIntrinsics, OpenCVRadTanIntrinsics, GenericParamsIntrinsics], Discriminator("model")
]


class Vector3(TypedDict):
    x: float
    y: float
    z: float


class Quaternion(TypedDict):
    w: float
    x: float
    y: float
    z: float


class Transform(TypedDict):
    position: Vector3
    rotation: Quaternion


class PointCloudPoint(TypedDict):
    position: Vector3  # uses transform.position (rotation ignored for points)
    color: Color


@dataclass
class LocalizationRequest:
    camera: Annotated[Json[CameraIntrinsics], Form()]  # This parses JSON automatically!
    image: UploadFile = File(...)
