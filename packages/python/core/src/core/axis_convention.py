from enum import Enum

from numpy import array, ndarray
from scipy.spatial.transform import Rotation


class AxisConvention(Enum):
    OPENCV = "OPENCV"
    UNITY = "UNITY"


basis_unity = array([[1, 0, 0], [0, 1, 0], [0, 0, 1]])
basis_opencv = array([[1, 0, 0], [0, -1, 0], [0, 0, 1]])
basic_change_unity_from_opencv = basis_unity.T @ basis_opencv
basis_change_opencv_from_unity = basic_change_unity_from_opencv.T


def change_basis_opencv_from_unity_pose(translation: ndarray, rotation: ndarray) -> tuple[ndarray, ndarray]:
    new_translation = basis_change_opencv_from_unity @ translation
    new_rotation = basis_change_opencv_from_unity @ rotation @ basic_change_unity_from_opencv
    return new_translation, new_rotation


def change_basis_unity_from_opencv_points(points: ndarray) -> ndarray:
    return (basic_change_unity_from_opencv @ points.T).T


def change_basis_unity_from_opencv_poses(translations: ndarray, orientations_xyzw: ndarray) -> tuple[ndarray, ndarray]:
    new_translations = change_basis_unity_from_opencv_points(translations)

    rotation_matrices = Rotation.from_quat(orientations_xyzw).as_matrix()
    new_rotation_matrices = basic_change_unity_from_opencv @ rotation_matrices @ basis_change_opencv_from_unity
    new_orientations_xyzw = Rotation.from_matrix(new_rotation_matrices).as_quat()

    return new_translations, new_orientations_xyzw
