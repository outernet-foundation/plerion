from __future__ import annotations

from os import environ
from pathlib import Path
from uuid import UUID

from common.fastapi import create_fastapi_app
from core.axis_convention import AxisConvention
from core.camera_config import CameraConfig, PinholeCameraConfig
from core.transform import Transform
from fastapi import File, UploadFile

from .localize import load_models
from .map_manager import Localization, localize

RECONSTRUCTIONS_DIR = Path("/tmp/reconstructions")
MAX_KEYPOINTS = 2500

if not environ.get("CODEGEN"):
    load_models(MAX_KEYPOINTS)


app = create_fastapi_app(title="Localize")


@app.get("/health")
async def health_check() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/localization")
async def localize_image(
    map_ids: list[UUID],
    prior: Transform,
    camera: CameraConfig,
    axis_convention: AxisConvention,
    image: UploadFile = File(...),
) -> list[Localization]:
    if not isinstance(camera, PinholeCameraConfig):
        raise ValueError("Only Pinhole camera config is supported")

    return localize(
        map_ids=map_ids, prior=prior, camera=camera, axis_convention=axis_convention, image=await image.read()
    )
