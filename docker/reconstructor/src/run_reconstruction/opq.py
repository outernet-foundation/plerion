from typing import cast

from core.reconstruction_manifest import ReconstructionOptions
from core.utility import to_f32
from faiss import OPQMatrix, ProductQuantizer  # type: ignore
from numpy import ascontiguousarray, float32, uint8, vstack
from numpy.typing import NDArray
from torch import Tensor


def train_opq_and_encode(options: ReconstructionOptions, image_descriptors: dict[str, Tensor]):
    # opq + pq
    number_of_subvectors = 16
    number_of_bits_per_subvector = 8
    opq_iterations = 20
    options.compression_opq_number_of_subvectors = number_of_subvectors
    options.compression_opq_number_bits_per_subvector = number_of_bits_per_subvector
    options.compression_opq_number_of_training_iterations = opq_iterations

    training_descriptors = vstack([to_f32(image_descriptors[name]) for name in image_descriptors.keys()])
    dimension = int(training_descriptors.shape[1])

    # Train OPQ matrix
    opq_matrix = OPQMatrix(dimension, number_of_subvectors)
    opq_matrix.niter = opq_iterations
    opq_matrix.verbose = True
    training_unit = ascontiguousarray(training_descriptors)
    opq_matrix.train(training_unit)  # type: ignore

    # Train PQ quantizer on OPQ-rotated descriptors
    rotated_training_unit = opq_matrix.apply(training_unit)  # type: ignore
    product_quantizer = ProductQuantizer(dimension, number_of_subvectors, number_of_bits_per_subvector)
    product_quantizer.verbose = True
    product_quantizer.train(rotated_training_unit)  # type: ignore

    images_codes: dict[str, NDArray[uint8]] = {}
    for name in image_descriptors.keys():
        # Descriptors -> OPQ rotate (via apply) -> PQ encode
        descriptors_contiguous = ascontiguousarray(to_f32(image_descriptors[name]))
        descriptors_rotated = cast(NDArray[float32], opq_matrix.apply(descriptors_contiguous))  # type: ignore
        codes = cast(NDArray[uint8], product_quantizer.compute_codes(descriptors_rotated))  # type: ignore
        images_codes[name] = codes

    return opq_matrix, product_quantizer, images_codes
