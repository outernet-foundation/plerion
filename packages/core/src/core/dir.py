from typing import Any

from neural_networks.models import DIR
from torch import Tensor, float32, inference_mode, stack  # type: ignore


def dir_extract(dir: DIR, rgb_image_tensors: dict[str, Tensor], batch_size: int, device: str):
    num_batches = (len(rgb_image_tensors) + batch_size - 1) // batch_size
    global_descriptors: dict[str, Any] = {}
    image_names = list(rgb_image_tensors.keys())
    for batch_start in range(0, len(rgb_image_tensors), batch_size):
        print(f"DIR: batch {batch_start // batch_size + 1} of {num_batches}")
        batch = image_names[batch_start : batch_start + batch_size]

        with inference_mode():
            output = dir({"image": stack([rgb_image_tensors[name] for name in batch]).to(device=device, dtype=float32)})

        for i, image_name in enumerate(batch):
            global_descriptors[image_name] = output["global_descriptor"][i]

    return global_descriptors
