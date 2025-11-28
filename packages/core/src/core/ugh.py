from __future__ import annotations

from .rig import CameraConfig


def transform_intrinsics(camera: CameraConfig):
    if not camera.model == "PINHOLE":
        raise NotImplementedError("Only PINHOLE camera model is supported for ColmapCamera conversion")

    fx = camera.fx
    fy = camera.fy
    cx = camera.cx
    cy = camera.cy

    if camera.rotation in ["90_CCW", "90_CW"]:
        fx, fy = camera.fy, camera.fx

        if camera.rotation == "90_CCW":
            cx = camera.height - camera.cy
            cy = camera.cx
        else:
            cx = camera.cy
            cy = camera.width - camera.cx

    return [fx, fy, cx, cy]
