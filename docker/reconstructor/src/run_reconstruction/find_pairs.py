from __future__ import annotations

from typing import Dict, Optional

import torch
from numpy import arccos, clip, degrees, fill_diagonal, ndarray, stack, where
from numpy.linalg import norm
from torch import from_numpy, topk  # type: ignore

ROTATION_THRESHOLD_DEG = 30.0


class Transform:
    def __init__(self, rotation: ndarray, translation: ndarray):
        self.rotation = rotation
        self.translation = translation


def pairs_from_poses(
    images: dict[tuple[str, str], Transform], num_neighbors: int, rotation_thresh_deg: float = ROTATION_THRESHOLD_DEG
):
    names = list(images.keys())
    R_w_c = stack([images[name].rotation for name in names], axis=0)  # (N, 3, 3)
    centers = stack([images[name].translation for name in names], axis=0)  # (N, 3)

    # Euclidean distances of camera centres
    diff = centers[:, None, :] - centers[None, :, :]
    dists = norm(diff, axis=-1)

    # Principal optical axis (Z direction) in world frame
    optical_axes = R_w_c[:, :, 2]
    cosines = clip(optical_axes @ optical_axes.T, -1.0, 1.0)
    ang_deg = degrees(arccos(cosines))

    scores = -from_numpy(dists)
    invalid = ang_deg >= rotation_thresh_deg
    fill_diagonal(invalid, True)
    invalid = from_numpy(invalid).to(scores.device)  # NEW: convert mask to torch bool tensor
    selected = pairs_from_score_matrix(scores, invalid, num_neighbors)

    return [(names[i], names[j]) for i, j in selected]


def pairs_from_retrieval(descriptors: Dict[str, torch.Tensor], num_neighbors: int, min_score: Optional[float] = None):
    names = list(descriptors.keys())
    tensor = torch.stack([descriptors[n] for n in names], dim=0)
    similarity = tensor @ tensor.t()

    invalid = torch.eye(len(names), dtype=torch.bool)
    if min_score is not None:
        invalid |= similarity < min_score

    selected = pairs_from_score_matrix(similarity, invalid, num_neighbors, min_score)
    return [(names[i], names[j]) for i, j in selected]


def pairs_from_score_matrix(
    scores: torch.Tensor, invalid: torch.Tensor, num_select: int, min_score: Optional[float] = None
):
    scores.masked_fill_(invalid, float("-inf"))
    top_k = topk(scores, num_select, dim=1)
    indices = top_k.indices.cpu().numpy()
    valid = top_k.values.isfinite().cpu().numpy()

    return [(int(i), int(indices[i, j])) for i, j in zip(*where(valid))]
