from io import BytesIO
from typing import Any, Literal

from cv2 import COLOR_BGR2GRAY, COLOR_BGR2RGB, IMREAD_COLOR, cvtColor, imdecode
from lightglue import SuperPoint  # type: ignore
from numpy import frombuffer, uint8
from PIL import Image as PILImage
from PIL.Image import Transpose
from torch import float32, from_numpy, inference_mode  # type: ignore

from .dir import DIR


class Image:
    def __init__(self, global_descriptor: Any, keypoints: Any, descriptors: Any, size: tuple[int, int]):
        self.global_descriptor = global_descriptor
        self.keypoints = keypoints
        self.descriptors = descriptors
        self.size = size

    @classmethod
    def from_buffer(
        cls,
        image_buffer: bytes,
        camera_rotation: Literal["None", "90_CW", "180", "90_CCW"],
        superpoint: SuperPoint,
        dir: DIR,
        device: str,
    ):
        image = PILImage.open(BytesIO(image_buffer))
        if camera_rotation == "90_CCW":
            image = image.transpose(Transpose.ROTATE_270)
        elif camera_rotation == "90_CW":
            image = image.transpose(Transpose.ROTATE_90)
        elif camera_rotation == "180":
            image = image.transpose(Transpose.ROTATE_180)

        buffered = BytesIO()
        image.save(buffered, format="JPEG", quality=95)
        buffer = buffered.getvalue()

        bgr_image = imdecode(frombuffer(buffer, uint8), IMREAD_COLOR)

        rgb_image = cvtColor(bgr_image, COLOR_BGR2RGB)
        rbg_image_tensor = (
            from_numpy(rgb_image).permute(2, 0, 1).unsqueeze(0).div(255.0).to(device=device, dtype=float32)
        )

        grayscale_image = cvtColor(bgr_image, COLOR_BGR2GRAY)
        grayscale_image_tensor = (
            from_numpy(grayscale_image).unsqueeze(0).unsqueeze(0).div(255.0).to(device=device, dtype=float32)
        )

        with inference_mode():
            return cls(
                global_descriptor=dir({"image": rbg_image_tensor})["global_descriptor"][0],
                keypoints=superpoint({"image": grayscale_image_tensor})["keypoints"][0],
                descriptors=superpoint({"image": grayscale_image_tensor})["descriptors"][0],
                size=bgr_image.shape[:2],
            )
