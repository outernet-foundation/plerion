from io import BytesIO
from pathlib import Path
from typing import Literal

from numpy import asarray, float32
from numpy.typing import NDArray
from PIL import Image as PILImage
from PIL.Image import Transpose
from torch import from_numpy  # type: ignore


def create_tensors_from_path(image_path: Path, camera_rotation: Literal["None", "90_CW", "180", "90_CCW"]):
    print(f"Creating tensors for image: {image_path}")
    with open(image_path, "rb") as img_file:
        return create_tensors_from_buffer(img_file.read(), camera_rotation)


def create_tensors_from_buffer(image_buffer: bytes, camera_rotation: Literal["None", "90_CW", "180", "90_CCW"]):
    image = PILImage.open(BytesIO(image_buffer))

    if camera_rotation == "90_CCW":
        image = image.transpose(Transpose.ROTATE_270)
    elif camera_rotation == "90_CW":
        image = image.transpose(Transpose.ROTATE_90)
    elif camera_rotation == "180":
        image = image.transpose(Transpose.ROTATE_180)

    image = image.convert("RGB")

    return (
        # RGB (permuted because DIR expects channel-first)
        from_numpy(asarray(image, dtype=float32)).permute(2, 0, 1).div(255.0),
        # Grayscale
        from_numpy(asarray(image.convert("L"), dtype=float32)).unsqueeze(0).div(255.0),
        # Lightglue expects (height, width)
        (image.height, image.width),
    )


class Image:
    def __init__(
        self,
        global_descriptor: NDArray[float32],
        keypoints: NDArray[float32],
        descriptors: NDArray[float32],
        size: tuple[int, int],
    ):
        self.global_descriptor = global_descriptor
        self.keypoints = keypoints
        self.descriptors = descriptors
        self.size = size
