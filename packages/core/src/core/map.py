# core/map.py
from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Dict

from core.h5 import read_h5_features_for_reconstruction, read_opq
from faiss import (  # type: ignore
    OPQMatrix,
    ProductQuantizer,  # type: ignore
)
from numpy import uint8
from numpy.typing import NDArray
from pycolmap import Reconstruction
from torch import Tensor  # type: ignore


@dataclass(frozen=True)
class Map:
    reconstruction: Reconstruction
    image_names: list[str]
    image_ids_in_order: list[int]
    image_id_by_name: dict[str, int]
    global_matrix: Tensor
    keypoints: Dict[int, Tensor]
    opq_matrix: OPQMatrix
    pq: ProductQuantizer
    pq_codes: dict[int, NDArray[uint8]]


def load_map_data(reconstruction_path: Path, device: str) -> Map:
    reconstruction = Reconstruction(str(reconstruction_path / "sfm_model"))

    (opq_matrix, pq) = read_opq(reconstruction_path)

    (image_names, image_ids_in_order, global_matrix, keypoints, pq_codes) = read_h5_features_for_reconstruction(
        reconstruction, reconstruction_path, device
    )

    return Map(
        reconstruction=reconstruction,
        image_names=image_names,
        image_ids_in_order=image_ids_in_order,
        image_id_by_name={n: i for n, i in zip(image_names, image_ids_in_order)},
        global_matrix=global_matrix,
        keypoints=keypoints,
        opq_matrix=opq_matrix,
        pq=pq,
        pq_codes=pq_codes,
    )
