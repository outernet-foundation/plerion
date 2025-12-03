from dataclasses import dataclass
from typing import Dict

from faiss import (  # type: ignore
    OPQMatrix,
    ProductQuantizer,  # type: ignore
)
from numpy import float32, uint8
from numpy.typing import NDArray
from pycolmap import Reconstruction


@dataclass(frozen=True)
class Map:
    reconstruction: Reconstruction
    image_names: list[str]
    image_ids_in_order: list[int]
    image_id_by_name: dict[str, int]
    global_matrix: NDArray[float32]
    keypoints: Dict[int, NDArray[float32]]
    opq_matrix: OPQMatrix
    pq: ProductQuantizer
    pq_codes: dict[int, NDArray[uint8]]
