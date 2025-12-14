from enum import Enum

from numpy import array, ndarray


class AxisConvention(Enum):
    OPENCV = "OPENCV"
    UNITY = "UNITY"


basis_unity = array([[1, 0, 0], [0, 1, 0], [0, 0, 1]])
basis_opencv = array([[1, 0, 0], [0, -1, 0], [0, 0, 1]])
basic_change_unity_from_opencv = basis_unity.T @ basis_opencv
basis_change_opencv_from_unity = basic_change_unity_from_opencv.T


def change_basis_opencv_from_unity(translation: ndarray, rotation: ndarray) -> tuple[ndarray, ndarray]:
    new_translation = basis_change_opencv_from_unity @ translation
    new_rotation = basis_change_opencv_from_unity @ rotation @ basic_change_unity_from_opencv
    return new_translation, new_rotation
