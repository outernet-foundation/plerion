from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Dict

import numpy as np
from h5py import Dataset, Group
from h5py import File as h5File
from numpy.typing import NDArray
from pycolmap import Reconstruction
from torch import Tensor, from_numpy  # type: ignore


@dataclass(frozen=True)
class Map:
    reconstruction: Reconstruction
    image_names: list[str]  # rows order for global_matrix
    image_ids_in_order: list[int]  # same length/order as image_names/global_matrix
    image_id_by_name: dict[str, int]  # optional lookup by name (subset)
    global_matrix: Tensor  # (N, Dg) aligned with image_names
    keypoints: Dict[int, Tensor]  # only for kept image_ids
    descriptors: Dict[int, Tensor]  # only for kept image_ids


def load_map_data(reconstruction: Reconstruction, features_path: Path, globals_path: Path, device: str) -> Map:
    def _leaf_groups_with_dataset(h5: h5File, dataset: str) -> list[str]:
        paths: list[str] = []

        def _visit(name: str, obj: Group | Dataset) -> None:
            if isinstance(obj, Group) and dataset in obj:
                paths.append(name)

        h5.visititems(_visit)
        return paths

    # Map reconstruction names -> ids once
    recon_name_to_id: dict[str, int] = {img.name: int(img_id) for img_id, img in reconstruction.images.items()}  # type: ignore

    kept_names: list[str] = []
    kept_ids: list[int] = []
    gm_rows: list[NDArray[np.float32]] = []

    # ---- Globals: keep only rows that are present in the reconstruction AND have the dataset ----
    with h5File(str(globals_path), "r") as gfile:
        for name in _leaf_groups_with_dataset(gfile, "global_descriptor"):
            if name not in recon_name_to_id:
                continue
            grp = gfile.get(name)
            if not isinstance(grp, Group):
                continue
            ds = grp.get("global_descriptor")
            if not isinstance(ds, Dataset):
                continue
            kept_names.append(name)
            kept_ids.append(recon_name_to_id[name])
            gm_rows.append(np.asarray(ds[()], dtype=np.float32))

    if not kept_names:
        raise RuntimeError("No overlap between H5 globals and reconstruction images.")

    global_matrix = from_numpy(np.stack(gm_rows, axis=0)).to(device).float()

    # ---- Locals: load only for the kept subset, and only if datasets exist ----
    keypoints: Dict[int, Tensor] = {}
    descriptors: Dict[int, Tensor] = {}
    with h5File(str(features_path), "r") as ffile:
        for name, img_id in zip(kept_names, kept_ids):
            grp = ffile.get(name)
            if not isinstance(grp, Group):
                continue
            k_ds = grp.get("keypoints")
            d_ds = grp.get("descriptors")
            if not isinstance(k_ds, Dataset) or not isinstance(d_ds, Dataset):
                continue
            k_arr = np.asarray(k_ds[()], dtype=np.float32)  # (K,2)
            d_arr = np.asarray(d_ds[()], dtype=np.float32)  # (D,K) or (K,D)
            kpts = from_numpy(k_arr).to(device)
            desc = from_numpy(d_arr).to(device)
            if desc.ndim == 2 and desc.shape[0] == kpts.shape[0] and desc.shape[1] != kpts.shape[0]:
                desc = desc.transpose(0, 1)  # -> (D,K)
            if desc.shape[1] != kpts.shape[0]:
                continue
            keypoints[img_id] = kpts
            descriptors[img_id] = desc

    return Map(
        image_names=kept_names,
        image_ids_in_order=kept_ids,
        image_id_by_name={n: i for n, i in zip(kept_names, kept_ids)},
        global_matrix=global_matrix,
        keypoints=keypoints,
        descriptors=descriptors,
        reconstruction=reconstruction,
    )
