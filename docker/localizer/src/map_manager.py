from __future__ import annotations

from pathlib import Path
from typing import Any, Mapping, cast
from uuid import UUID

from common.boto_clients import create_s3_client
from core.axis_convention import AxisConvention
from core.camera_config import PinholeCameraConfig
from core.h5 import FEATURES_FILE, GLOBAL_DESCRIPTORS_FILE, read_features, read_global_descriptors
from core.localization_metrics import LocalizationMetrics
from core.opq import OPQ_MATRIX_FILE, PQ_QUANTIZER_FILE, read_opq_matrix, read_pq_quantizer
from core.transform import Transform
from numpy import float32, stack, uint8
from numpy.typing import NDArray
from pycolmap import Reconstruction
from pydantic import BaseModel

from .localize import localize_image_against_reconstruction
from .map import Map
from .settings import get_settings

RECONSTRUCTIONS_DIR = Path("/tmp/reconstructions")
RETRIEVAL_TOP_K = 12  # how many similar database images to keep
RANSAC_THRESHOLD = 12.0  # reprojection error in pixels


_maps: dict[UUID, Map] = {}

settings = get_settings()
s3_client = create_s3_client(
    minio_endpoint_url=settings.minio_endpoint_url,
    minio_access_key=settings.minio_access_key,
    minio_secret_key=settings.minio_secret_key,
)


class Localization(BaseModel):
    id: UUID
    transform: Transform
    metrics: LocalizationMetrics


def localize(
    map_ids: list[UUID], prior: Transform, camera: PinholeCameraConfig, axis_convention: AxisConvention, image: bytes
):
    for id in map_ids:
        if id not in _maps:
            _load(id)

    return [
        Localization(id=id, transform=result[0], metrics=result[1])
        for id, result in zip(
            map_ids,
            [
                localize_image_against_reconstruction(
                    map=_maps[id],
                    camera=camera,
                    axis_convention=axis_convention,
                    image_buffer=image,
                    retrieval_top_k=RETRIEVAL_TOP_K,
                    ransac_threshold=RANSAC_THRESHOLD,
                )
                for id in map_ids
            ],
        )
    ]


def _load(id: UUID):
    for page in s3_client.get_paginator("list_objects_v2").paginate(
        Bucket=settings.reconstructions_bucket, Prefix=f"{id}/"
    ):
        for obj in page.get("Contents", []):
            key = obj["Key"]  # type: ignore
            if not (
                key.startswith(f"{id}/sfm_model/")
                or key
                in {
                    f"{id}/{GLOBAL_DESCRIPTORS_FILE}",
                    f"{id}/{FEATURES_FILE}",
                    f"{id}/{OPQ_MATRIX_FILE}",
                    f"{id}/{PQ_QUANTIZER_FILE}",
                }
            ):
                continue

            local_path = RECONSTRUCTIONS_DIR / str(id) / key[len(str(id)) :].lstrip("/")
            local_path.parent.mkdir(parents=True, exist_ok=True)
            print(f"Downloading s3://{settings.reconstructions_bucket}/{key} to {local_path}")
            s3_client.download_file(settings.reconstructions_bucket, key, str(local_path))

    reconstruction_path = RECONSTRUCTIONS_DIR / str(id)
    reconstruction = Reconstruction(str(reconstruction_path / "sfm_model"))
    ordered_image_ids: list[int] = sorted(cast(Mapping[int, Any], reconstruction.images).keys())
    ordered_image_names = [reconstruction.images[image_id].name for image_id in ordered_image_ids]
    global_descriptors_by_name = read_global_descriptors(reconstruction_path, ordered_image_names)
    (keypoints_by_name, pq_codes_by_name) = read_features(reconstruction_path, ordered_image_names)

    image_sizes: dict[str, tuple[int, int]] = {}
    global_descriptor_rows: list[NDArray[float32]] = []
    keypoints: dict[int, NDArray[float32]] = {}
    pq_codes: dict[int, NDArray[uint8]] = {}

    for image_id in ordered_image_ids:
        image = reconstruction.images[image_id]
        camera = reconstruction.cameras[image.camera_id]
        name = image.name
        image_sizes[str(image_id)] = (camera.height, camera.width)
        keypoints[image_id] = keypoints_by_name[name]
        pq_codes[image_id] = pq_codes_by_name[name]
        global_descriptor_rows.append(global_descriptors_by_name[name])

    _maps[id] = Map(
        reconstruction.points3D,
        reconstruction.images,
        ordered_image_ids,
        image_sizes,
        keypoints,
        pq_codes,
        stack(global_descriptor_rows, axis=0),
        read_opq_matrix(reconstruction_path),
        read_pq_quantizer(reconstruction_path),
    )
