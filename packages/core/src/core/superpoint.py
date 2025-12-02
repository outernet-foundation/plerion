from lightglue import SuperPoint  # type: ignore
from torch import Tensor, float32, inference_mode, stack  # type: ignore


def superpoint_extract(
    superpoint: SuperPoint, grayscale_image_tensors: dict[str, Tensor], batch_size: int, device: str
):
    num_batches = (len(grayscale_image_tensors) + batch_size - 1) // batch_size
    keypoints: dict[str, Tensor] = {}
    descriptors: dict[str, Tensor] = {}
    image_names = list(grayscale_image_tensors.keys())
    for batch_start in range(0, len(grayscale_image_tensors), batch_size):
        print(f"Superpoint: batch {batch_start // batch_size + 1} of {num_batches}")
        batch = image_names[batch_start : batch_start + batch_size]

        with inference_mode():
            output = superpoint({
                "image": stack([grayscale_image_tensors[name] for name in batch]).to(device=device, dtype=float32)
            })

        for i, image_name in enumerate(batch):
            keypoints[image_name] = output["keypoints"][i]
            descriptors[image_name] = output["descriptors"][i]

    return keypoints, descriptors
