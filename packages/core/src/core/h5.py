from pathlib import Path
from typing import Any, Dict, Mapping, cast

from h5py import Dataset, File, Group
from numpy import asarray, float32, stack, uint8
from numpy.typing import NDArray
from pycolmap import Reconstruction

GLOBAL_DESCRIPTORS_DATASET_NAME = "global_descriptor"
KEYPOINTS_DATASET_NAME = "keypoints"
PQ_CODES_DATASET_NAME = "pq_codes"
GLOBAL_DESCRIPTORS_FILE = "global_descriptors.h5"
FEATURES_FILE = "features.h5"


def write_global_descriptors(root_path: Path, global_descriptors: Mapping[str, NDArray[float32]]):
    path = root_path / GLOBAL_DESCRIPTORS_FILE
    with File(str(path), "w") as file:
        for name, global_descriptor in global_descriptors.items():
            group = file.create_group(name)
            _create_dataset(group, GLOBAL_DESCRIPTORS_DATASET_NAME, global_descriptor)

    return GLOBAL_DESCRIPTORS_FILE, path.read_bytes()


def write_features(root_path: Path, keypoints: Mapping[str, NDArray[float32]], pq_codes: Mapping[str, NDArray[uint8]]):
    path = root_path / FEATURES_FILE
    with File(str(path), "w") as file:
        for name, image_keypoints in keypoints.items():
            group = file.create_group(name)
            _create_dataset(group, KEYPOINTS_DATASET_NAME, image_keypoints)
            _create_dataset(group, PQ_CODES_DATASET_NAME, pq_codes[name])

    return FEATURES_FILE, path.read_bytes()


def _create_dataset(group: Group, name: str, data: Any):
    group.create_dataset(name, data=data, compression="gzip", compression_opts=9, shuffle=True, chunks=True)


def read_h5_features_for_reconstruction(reconstruction: Reconstruction, root_path: Path, device: str):
    images_mapping = cast(Mapping[int, Any], reconstruction.images)

    # Canonical order: sort by image id for determinism
    image_items = sorted(images_mapping.items(), key=lambda item: int(item[0]))
    image_ids_in_order = [int(image_id) for image_id, _ in image_items]
    image_names = [str(img.name) for _, img in image_items]

    # --- global descriptors ---
    with File(str(root_path / GLOBAL_DESCRIPTORS_FILE), "r") as file:
        global_rows = [
            asarray(cast(Group | File, cast(Group, file[name])[GLOBAL_DESCRIPTORS_DATASET_NAME])[()], dtype=float32)
            for name in image_names
        ]

    global_matrix = stack(global_rows, axis=0)

    keypoints: Dict[int, NDArray[float32]] = {}
    pq_codes: Dict[int, NDArray[uint8]] = {}

    with File(str(root_path / FEATURES_FILE), "r") as file:
        for image_id, name in zip(image_ids_in_order, image_names):
            group = cast(Group, file[name])
            keypoints[image_id] = asarray(cast(Dataset, group[KEYPOINTS_DATASET_NAME])[()], dtype=float32)
            pq_codes[image_id] = asarray(cast(Dataset, group[PQ_CODES_DATASET_NAME])[()], dtype=uint8)

    return image_names, image_ids_in_order, global_matrix, keypoints, pq_codes
