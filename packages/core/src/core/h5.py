from pathlib import Path
from typing import Any, Dict, Mapping, cast

from faiss import (  # type: ignore
    OPQMatrix,
    ProductQuantizer,
    read_ProductQuantizer,  # type: ignore
    read_VectorTransform,  # type: ignore
    write_ProductQuantizer,  # type: ignore
    write_VectorTransform,  # type: ignore
)
from h5py import Dataset, File, Group
from numpy import asarray, float32, stack, uint8
from numpy.typing import NDArray
from pycolmap import Reconstruction
from torch import Tensor, from_numpy  # type: ignore

from .utility import to_f32

GLOBAL_DS_NAME = "global_descriptor"
KEYPOINTS_DS_NAME = "keypoints"
PQ_CODES_DS_NAME = "pq_codes"


def write_pairs(root_path: Path, pairs: list[tuple[str, ...]]):
    (root_path / "pairs.txt").write_text("\n".join([" ".join(pair) for pair in pairs]))
    return root_path / "pairs.txt"


def write_opq(opq_matrix: OPQMatrix, pq_quantizer: ProductQuantizer, root_path: Path):
    write_VectorTransform(opq_matrix, str(root_path / "opq_matrix.tf"))
    write_ProductQuantizer(pq_quantizer, str(root_path / "pq_quantizer.pq"))
    return root_path / "opq_matrix.tf", root_path / "pq_quantizer.pq"


def read_opq(root_path: Path) -> tuple[OPQMatrix, ProductQuantizer]:
    return (
        cast(OPQMatrix, read_VectorTransform(str(root_path / "opq_matrix.tf"))),
        cast(ProductQuantizer, read_ProductQuantizer(str(root_path / "pq_quantizer.pq"))),
    )


def write_h5_features(
    root_path: Path,
    global_descriptors: Mapping[str, Tensor],
    keypoints: Mapping[str, NDArray[float32]],
    pq_codes: Mapping[str, NDArray[uint8]],
):
    with File(str(root_path / "global_descriptors.h5"), "w") as file:
        for name, global_descriptor in global_descriptors.items():
            group = file.create_group(name)
            _create_dataset(group, GLOBAL_DS_NAME, to_f32(global_descriptor))

    with File(str(root_path / "features.h5"), "w") as file:
        for name, image_keypoints in keypoints.items():
            group = file.create_group(name)
            _create_dataset(group, KEYPOINTS_DS_NAME, image_keypoints)
            _create_dataset(group, PQ_CODES_DS_NAME, pq_codes[name])

    return root_path / "global_descriptors.h5", root_path / "features.h5"


def read_h5_features_for_reconstruction(reconstruction: Reconstruction, root_path: Path, device: str):
    images_mapping = cast(Mapping[int, Any], reconstruction.images)

    # Canonical order: sort by image id for determinism
    image_items = sorted(images_mapping.items(), key=lambda item: int(item[0]))
    image_ids_in_order = [int(image_id) for image_id, _ in image_items]
    image_names = [str(img.name) for _, img in image_items]

    # --- global descriptors ---
    with File(str(root_path / "global_descriptors.h5"), "r") as file:
        global_rows = [
            asarray(cast(Group | File, cast(Group, file[name])[GLOBAL_DS_NAME])[()], dtype=float32)
            for name in image_names
        ]

    global_matrix = from_numpy(stack(global_rows, axis=0)).to(device).float()

    keypoints: Dict[int, Tensor] = {}
    pq_codes: Dict[int, NDArray[uint8]] = {}

    with File(str(root_path / "features.h5"), "r") as file:
        for image_id, name in zip(image_ids_in_order, image_names):
            group = cast(Group, file[name])
            keypoints[image_id] = from_numpy(asarray(cast(Dataset, group[KEYPOINTS_DS_NAME])[()], dtype=float32)).to(
                device
            )
            pq_codes[image_id] = asarray(cast(Dataset, group[PQ_CODES_DS_NAME])[()], dtype=uint8)

    return image_names, image_ids_in_order, global_matrix, keypoints, pq_codes


def _create_dataset(group: Group, name: str, data: Any):
    group.create_dataset(name, data=data, compression="gzip", compression_opts=9, shuffle=True, chunks=True)
