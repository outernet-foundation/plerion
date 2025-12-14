from io import BytesIO

from core.capture_session_manifest import CameraConfig
from PIL import Image as PILImage
from PIL.Image import Transpose


def transform_image(image_buffer: bytes, camera_config: CameraConfig):
    image = PILImage.open(BytesIO(image_buffer))

    if camera_config.mirroring == "X":
        image = image.transpose(Transpose.FLIP_LEFT_RIGHT)
    elif camera_config.mirroring == "Y":
        image = image.transpose(Transpose.FLIP_TOP_BOTTOM)

    if camera_config.rotation == "90_CCW":
        image = image.transpose(Transpose.ROTATE_270)
    elif camera_config.rotation == "90_CW":
        image = image.transpose(Transpose.ROTATE_90)
    elif camera_config.rotation == "180":
        image = image.transpose(Transpose.ROTATE_180)

    return image.convert("RGB")


def transform_intrinsics(camera: CameraConfig):
    if not camera.model == "PINHOLE":
        raise NotImplementedError("Only PINHOLE camera model is supported for ColmapCamera conversion")

    fx = camera.fx
    fy = camera.fy
    cx = camera.cx
    cy = camera.cy

    if camera.mirroring == "X":
        cx = camera.width - cx
    elif camera.mirroring == "Y":
        cy = camera.height - cy

    if camera.rotation in ["90_CCW", "90_CW"]:
        fx, fy = camera.fy, camera.fx

        if camera.rotation == "90_CCW":
            cx = camera.height - camera.cy
            cy = camera.cx
        else:
            cx = camera.cy
            cy = camera.width - camera.cx

    return [fx, fy, cx, cy]
