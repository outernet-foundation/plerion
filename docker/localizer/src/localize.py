from __future__ import annotations

from os import environ
from typing import Any, cast

import numpy as np
from common.boto_clients import create_s3_client
from core.classes import Quaternion, Transform, Vector3
from core.localization_metrics import LocalizationMetrics
from core.opq import decode_descriptors
from core.rig import PinholeCameraConfig, transform_intrinsics
from neural_networks.image import Image
from neural_networks.lightglue import lightglue_match_tensors
from pycolmap import AbsolutePoseEstimationOptions, RANSACOptions
from pycolmap import Camera as ColmapCamera
from pycolmap._core import estimate_and_refine_absolute_pose  # type: ignore
from torch import cuda, from_numpy, mv, topk  # type: ignore

from .build_metrics import build_localization_metrics
from .map import Map
from .settings import get_settings

WEIGHTS = "indoor"
MAX_KEYPOINTS = 2500
RETRIEVAL_TOP_K = 12  # how many similar database images to keep
RANSAC_THRESHOLD = 12.0  # reprojection error in pixels

settings = get_settings()
s3_client = create_s3_client(
    s3_endpoint_url=settings.s3_endpoint_url, s3_access_key=settings.s3_access_key, s3_secret_key=settings.s3_secret_key
)

if environ.get("CODEGEN"):
    dir: Any = None
    superpoint: Any = None
    lightglue: Any = None
else:
    from neural_networks.dir import load_DIR
    from neural_networks.lightglue import load_lightglue, load_superpoint

    DEVICE = "cuda" if cuda.is_available() else "cpu"
    print(f"Using device: {DEVICE}")

    print("Loading deep-image-retrieval model")
    dir = load_DIR(DEVICE)

    print("Loading superpoint model")
    superpoint = load_superpoint(max_num_keypoints=MAX_KEYPOINTS, device=DEVICE)

    print("Loading lightglue model")
    lightglue = load_lightglue(DEVICE)


async def localize_image_against_reconstruction(
    map: Map, camera: PinholeCameraConfig, image_buffer: bytes
) -> tuple[Transform, LocalizationMetrics]:
    pycolmap_camera = ColmapCamera(
        width=camera.width, height=camera.height, model="PINHOLE", params=transform_intrinsics(camera)
    )

    query_image = Image.from_buffer(image_buffer, camera.rotation, superpoint, dir, DEVICE)

    # Retrieve top K most similar database images
    similarity_scores = mv(map.global_matrix, query_image.global_descriptor)
    topk_rows: list[int] = topk(similarity_scores, RETRIEVAL_TOP_K).indices.cpu().tolist()  # type: ignore
    matched_names: list[str] = [map.image_names[i] for i in topk_rows]
    matched_image_ids: list[int] = [map.image_id_by_name[n] for n in matched_names]

    descriptors = decode_descriptors(
        map.opq_matrix, map.pq, {image_id: map.pq_codes[image_id] for image_id in matched_image_ids}
    )

    pairs = [(str(image_id), "query") for image_id in matched_image_ids]

    match_indices = lightglue_match_tensors(
        lightglue,
        pairs,
        keypoints={
            **{str(image_id): map.keypoints[image_id].to(DEVICE) for image_id in matched_image_ids},
            "query": query_image.keypoints.to(DEVICE),
        },
        descriptors={
            **{str(image_id): from_numpy(descriptors[image_id]).to(DEVICE) for image_id in matched_image_ids},
            "query": query_image.descriptors.to(DEVICE),
        },
        sizes={
            **{
                str(image_id): (
                    map.reconstruction.cameras[map.reconstruction.images[image_id].camera_id].height,
                    map.reconstruction.cameras[map.reconstruction.images[image_id].camera_id].width,
                )
                for image_id in matched_image_ids
            },
            "query": query_image.size,
        },
        batch_size=len(pairs),
        device=DEVICE,
    )

    # Gather 2D-3D correspondences
    query_keypoint_indices: list[int] = []
    point3d_indices: list[int] = []
    for image_id in matched_image_ids:
        for database_image_keypoint_index, query_image_keypoint_index in zip(*match_indices[(str(image_id), "query")]):
            point2D = map.reconstruction.images[image_id].points2D[int(database_image_keypoint_index)]

            if not point2D.has_point3D():
                continue

            query_keypoint_indices.append(int(query_image_keypoint_index))
            point3d_indices.append(int(point2D.point3D_id))

    if not query_keypoint_indices:
        raise ValueError("No matching keypoints found")

    points2d = query_image.keypoints[query_keypoint_indices].cpu().numpy()
    points3d = np.vstack([map.reconstruction.points3D[i].xyz for i in point3d_indices])

    # Estimate pose using PnP RANSAC
    ransac_options = RANSACOptions()
    ransac_options.max_error = RANSAC_THRESHOLD
    estimationOptions = AbsolutePoseEstimationOptions()
    estimationOptions.ransac = ransac_options
    pnp_result = cast(
        dict[str, Any] | None,
        estimate_and_refine_absolute_pose(points2d, points3d, pycolmap_camera, estimation_options=estimationOptions),
    )

    if pnp_result is None:
        raise ValueError("Pose estimation failed")

    # print every key in pnp_result for debugging
    for key in pnp_result.keys():
        print(f"PnP result key: {key}")

    transform = Transform(
        position=Vector3(
            x=float(pnp_result["cam_from_world"].translation[0]),
            y=float(pnp_result["cam_from_world"].translation[1]),
            z=float(pnp_result["cam_from_world"].translation[2]),
        ),
        rotation=Quaternion(
            x=float(pnp_result["cam_from_world"].rotation.quat[0]),
            y=float(pnp_result["cam_from_world"].rotation.quat[1]),
            z=float(pnp_result["cam_from_world"].rotation.quat[2]),
            w=float(pnp_result["cam_from_world"].rotation.quat[3]),
        ),
    )

    print(f"Localization successful: {transform}")

    return transform, build_localization_metrics(pnp_result, points2d, points3d, pycolmap_camera)
