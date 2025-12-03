from __future__ import annotations

from os import environ
from typing import Any, cast

import numpy as np
from common.boto_clients import create_s3_client
from core.classes import Quaternion, Transform, Vector3
from core.image import create_tensors_from_buffer
from core.lightglue import lightglue_match_tensors
from core.localization_metrics import LocalizationMetrics
from core.opq import decode_descriptors
from core.rig import PinholeCameraConfig, transform_intrinsics
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
    from neural_networks.models import load_DIR, load_lightglue, load_superpoint

    DEVICE = "cuda" if cuda.is_available() else "cpu"
    print(f"Using device: {DEVICE}")

    print("Loading DIR")
    dir = load_DIR(DEVICE)

    print("Loading SuperPoint")
    superpoint = load_superpoint(max_num_keypoints=MAX_KEYPOINTS, device=DEVICE)

    print("Loading LightGlue")
    lightglue = load_lightglue(DEVICE)


async def localize_image_against_reconstruction(
    map: Map, camera: PinholeCameraConfig, image_buffer: bytes
) -> tuple[Transform, LocalizationMetrics]:

    (rgb_tensor, gray_tensor, size) = create_tensors_from_buffer(image_buffer, camera.rotation)

    print("Extracting features")
    superpoint_output = superpoint({"image": gray_tensor.to(device=DEVICE)})
    query_global_descriptor = dir({"image": rgb_tensor.unsqueeze(0).to(device=DEVICE)})["global_descriptor"][0]

    print("Retrieving similar images")
    similarity_scores = mv(from_numpy(map.global_matrix).to(DEVICE), query_global_descriptor)
    topk_rows: list[int] = topk(similarity_scores, RETRIEVAL_TOP_K).indices.cpu().tolist()  # type: ignore
    matched_names: list[str] = [map.image_names[i] for i in topk_rows]
    matched_image_ids: list[int] = [map.image_id_by_name[n] for n in matched_names]

    print("Decoding database descriptors")
    descriptors = decode_descriptors(
        map.opq_matrix, map.pq, {image_id: map.pq_codes[image_id] for image_id in matched_image_ids}
    )

    print("Matching features")
    pairs = [(str(image_id), "query") for image_id in matched_image_ids]
    keypoints = {str(image_id): map.keypoints[image_id] for image_id in matched_image_ids}
    descriptors = {str(image_id): descriptors[image_id] for image_id in matched_image_ids}
    sizes = {
        str(image_id): (
            map.reconstruction.cameras[map.reconstruction.images[image_id].camera_id].height,
            map.reconstruction.cameras[map.reconstruction.images[image_id].camera_id].width,
        )
        for image_id in matched_image_ids
    }

    keypoints["query"] = superpoint_output["keypoints"][0].to(DEVICE)
    descriptors["query"] = superpoint_output["descriptors"][0].to(DEVICE)
    sizes["query"] = size

    match_indices = lightglue_match_tensors(lightglue, pairs, keypoints, descriptors, sizes, len(pairs), DEVICE)

    print("Gathering 2D-3D correspondences")
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

    points2d = keypoints["query"][query_keypoint_indices].cpu().numpy()
    points3d = np.vstack([map.reconstruction.points3D[i].xyz for i in point3d_indices])

    pycolmap_camera = ColmapCamera(
        width=camera.width, height=camera.height, model="PINHOLE", params=transform_intrinsics(camera)
    )

    print("Esimating pose")
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
