from typing import cast

from core.utility import to_f32
from faiss import OPQMatrix, ProductQuantizer  # type: ignore
from numpy import ascontiguousarray, float32, uint8
from numpy.linalg import norm
from numpy.typing import NDArray
from torch import Tensor


def train_opq_matrix(number_of_subvectors: int, number_of_training_iterations: int, training_unit: NDArray[float32]):
    opq_matrix = OPQMatrix(training_unit.shape[1], number_of_subvectors)
    opq_matrix.niter = number_of_training_iterations
    opq_matrix.verbose = True
    opq_matrix.train(training_unit)  # type: ignore
    return opq_matrix


def train_pq_quantizer(
    number_of_subvectors: int, number_of_bits_per_subvector: int, opq_matrix: OPQMatrix, training_unit: NDArray[float32]
):
    rotated_training_unit = opq_matrix.apply(training_unit)  # type: ignore
    product_quantizer = ProductQuantizer(training_unit.shape[1], number_of_subvectors, number_of_bits_per_subvector)
    product_quantizer.verbose = True
    product_quantizer.train(rotated_training_unit)  # type: ignore
    return product_quantizer


def encode_descriptors(
    opq_matrix: OPQMatrix, product_quantizer: ProductQuantizer, image_descriptors: dict[str, Tensor]
):
    images_codes: dict[str, NDArray[uint8]] = {}
    for name in image_descriptors.keys():
        descriptors_contiguous = ascontiguousarray(to_f32(image_descriptors[name]))
        descriptors_rotated = cast(NDArray[float32], opq_matrix.apply(descriptors_contiguous))  # type: ignore
        codes = cast(NDArray[uint8], product_quantizer.compute_codes(descriptors_rotated))  # type: ignore
        images_codes[name] = codes

    return images_codes


def decode_descriptors(opq_matrix: OPQMatrix, product_quantizer: ProductQuantizer, pq_codes: dict[int, NDArray[uint8]]):
    descriptors: dict[int, NDArray[float32]] = {}
    for image_id, code in pq_codes.items():
        decoded = cast(NDArray[float32], product_quantizer.decode(code))  # type: ignore
        reversed_transformed = cast(NDArray[float32], opq_matrix.reverse_transform(decoded))  # type: ignore
        descriptors[image_id] = _l2_normalize_rows(reversed_transformed)

    return descriptors


def _l2_normalize_rows(matrix: NDArray[float32]) -> NDArray[float32]:
    return (matrix / (norm(matrix, axis=1, keepdims=True).astype(float32) + float32(1e-12))).astype(float32, copy=False)
