from __future__ import annotations

from io import BytesIO
from itertools import combinations
from pathlib import Path
from tarfile import open as open_tar
from typing import Iterable, Iterator, Literal, TypeVar

import torch
from common.boto_clients import create_s3_client
from core.h5 import write_h5_features, write_opq, write_pairs
from core.reconstruction_manifest import ReconstructionManifest
from core.rig import Config
from neural_networks.dir import load_DIR
from neural_networks.image import Image
from neural_networks.lightglue import load_lightglue, load_superpoint
from numpy import eye, float32, float64, int32, intp, nonzero, random, stack, uint32
from numpy.typing import NDArray
from pycolmap import Database, PosePrior, PosePriorCoordinateSystem
from pycolmap import Image as pycolmapImage
from pycolmap._core import apply_rig_config, incremental_mapping, match_spatial, set_random_seed
from torch import cuda

from .find_pairs import pairs_from_poses
from .metrics_builder import MetricsBuilder
from .opq import train_opq_and_encode
from .options_builder import OptionsBuilder
from .rig import Rig
from .settings import get_settings

settings = get_settings()

DEVICE = "cuda" if cuda.is_available() else "cpu"
print(f"Using device: {DEVICE}")

UINT64_MAX = 18446744073709551615  # sentinel used by Point2D.point3D_id default

WORK_DIR = Path("/tmp/reconstruction")
OUTPUT_DIRECTORY = WORK_DIR / "outputs"
CAPTURE_SESSION_DIRECTORY = WORK_DIR / "capture_session"
# PAIRS_FILE = WORK_DIR / "pairs.txt"
# GLOBAL_DESCRIPTORS_FILE = WORK_DIR / "global_descriptors.h5"
# FEATURES_FILE = WORK_DIR / "features.h5"
# OPQ_MATRIX_FILE = WORK_DIR / "opq_matrix.tf"
# PQ_QUANTIZER_FILE = WORK_DIR / "pq_quantizer.pq"
COLMAP_DB_PATH = OUTPUT_DIRECTORY / "database.db"
COLMAP_SFM_DIRECTORY = OUTPUT_DIRECTORY / "sfm_model"
COLMAP_SFM_DIRECTORY.mkdir(parents=True, exist_ok=True)

WEIGHTS = "indoor"
DEFAULT_NEIGHBORS_COUNT = 12
RETRIEVAL_TOP_K = 20
# Prior weights (tweak as you like)
POSE_PRIOR_ROT_SIGMA_DEG = 5.0  # 1-sigma on rotation, in degrees
POSE_PRIOR_POS_SIGMA_M = 0.25  # 1-sigma on position, in meters
MIN_MODEL_SIZE = 10  # minimum number of registered images for a model to be valid
# TRACK_MIN_NUM_MATCH = 15
# BUNDLE_ADJUST_MAX_REPROJECTION = 1.0

s3_client = create_s3_client(
    s3_endpoint_url=settings.s3_endpoint_url, s3_access_key=settings.s3_access_key, s3_secret_key=settings.s3_secret_key
)

T = TypeVar("T")


def iterate_and_log(iterable: Iterable[T], every: int = 1, label: str = "Progress") -> Iterator[T]:
    for i, item in enumerate(iterable, start=1):
        if i % every == 0:
            print(f"{label}: {i} items processed", flush=True)
        yield item


def _put_reconstruction_object(key: str, body: bytes):
    print(f"Putting object in bucket {settings.reconstructions_bucket} with key {settings.reconstruction_id}/{key}")
    s3_client.put_object(Bucket=settings.reconstructions_bucket, Key=f"{settings.reconstruction_id}/{key}", Body=body)


def main():
    # Load reconstruction manifest
    manifest = ReconstructionManifest.model_validate_json(
        s3_client.get_object(Bucket=settings.reconstructions_bucket, Key=f"{settings.reconstruction_id}/manifest.json")[
            "Body"
        ].read()
    )

    # Set random seed if one is provided
    if manifest.options.random_seed is not None:
        random.seed(manifest.options.random_seed)
        set_random_seed(manifest.options.random_seed)

    # Create builders for options and metrics
    options_builder = OptionsBuilder(manifest.options)
    metrics_builder = MetricsBuilder(COLMAP_DB_PATH)

    # Download and extract capture session tarball
    print("Downloading capture session")
    with open_tar(
        fileobj=BytesIO(
            s3_client.get_object(Bucket=settings.captures_bucket, Key=f"{settings.capture_id}.tar")["Body"].read()
        ),
        mode="r:*",
    ) as tar:
        tar.extractall(path=CAPTURE_SESSION_DIRECTORY)

    # Load capture config
    with open(CAPTURE_SESSION_DIRECTORY / "config.json", "rb") as file:
        config = Config.model_validate_json(file.read().decode("utf-8"))

    # Load and validate rigs
    rigs = {rig.id: Rig(rig, (CAPTURE_SESSION_DIRECTORY / f"{rig.id}/frames.csv").read_text()) for rig in config.rigs}

    # Find proximal frame pairs across all rigs
    proximal_frame_pairs = pairs_from_poses(
        {
            (rig_id, frame_id): transform
            for rig_id, rig in rigs.items()
            for frame_id, transform in rig.frame_poses.items()
        },
        manifest.options.neighbors_count or DEFAULT_NEIGHBORS_COUNT,
    )

    # Determine image pairs to match
    pairs = [
        # Cross-frame image pairs by frame proximity
        (f"{rig_id_a}/{camera_a[0].id}/{frame_id_a}.jpg", f"{rig_id_b}/{camera_b[0].id}/{frame_id_b}.jpg")
        for (rig_id_a, frame_id_a), (rig_id_b, frame_id_b) in proximal_frame_pairs
        for camera_a in rigs[rig_id_a].cameras.values()
        for camera_b in rigs[rig_id_b].cameras.values()
    ] + [
        # Intra-frame, cross-sensor image pairs
        (f"{rig_id}/{camera_a[0].id}/{frame_id}.jpg", f"{rig_id}/{camera_b[0].id}/{frame_id}.jpg")
        for rig_id, rig in rigs.items()
        for frame_id in rig.frame_poses.keys()
        for camera_a, camera_b in combinations(list(rig.cameras.values()), 2)
    ]

    # Canonicalize and deduplicate pairs
    canonical_pairs = sorted({tuple(sorted((a, b))) for a, b in pairs if a != b})

    pairs_file = write_pairs(WORK_DIR, canonical_pairs)
    _put_reconstruction_object(key="pairs.txt", body=pairs_file.read_bytes())

    # Load DIR and SuperPoint models
    print("Loading DIR")
    dir = load_DIR(DEVICE)
    print("Loading SuperPoint")
    superpoint = load_superpoint(max_num_keypoints=manifest.options.max_keypoints_per_image, device=DEVICE)

    def create_image(image_path: str, rotation: Literal["None", "90_CW", "180", "90_CCW"]) -> Image:
        with open(CAPTURE_SESSION_DIRECTORY / image_path, "rb") as img_file:
            return Image(
                image_buffer=img_file.read(), camera_rotation=rotation, superpoint=superpoint, dir=dir, device=DEVICE
            )

    # Extract global descriptors and keypoints/descriptors for all images
    images = {
        f"{rig_id}/{camera[0].id}/{frame_id}.jpg": create_image(
            image_path=f"{rig_id}/{camera[0].id}/{frame_id}.jpg", rotation=camera[0].camera_config.rotation
        )
        for rig_id, camera, frame_id in iterate_and_log(
            (
                (rig_id, camera, frame_id)
                for rig_id, rig in rigs.items()
                for camera in rig.cameras.values()
                for frame_id in rig.frame_poses.keys()
            ),
            label="Extracting features",
        )
    }

    # Move keypoints to cpu and convert to numpy
    keypoints = {
        name: image.keypoints.detach().cpu().numpy().astype(float32, copy=False) for name, image in images.items()
    }

    # Compute and store average keypoints per image metric
    metrics_builder.metrics.average_keypoints_per_image = float(
        sum(len(img.keypoints) for img in images.values()) / len(images)
    )

    # Train OPQ and PQ, and encode all image descriptors
    print("Training OPQ and PQ, and encoding descriptors")
    opq_matrix, product_quantizer, image_codes = train_opq_and_encode(
        manifest.options, {key: images[key].descriptors for key in images.keys()}
    )

    opq_matrix_file, pq_file = write_opq(opq_matrix, product_quantizer, WORK_DIR)
    _put_reconstruction_object(key="opq_matrix.tf", body=opq_matrix_file.read_bytes())
    _put_reconstruction_object(key="pq_quantizer.pq", body=pq_file.read_bytes())

    global_descriptors_file, features_file = write_h5_features(
        root_path=WORK_DIR,
        global_descriptors={name: images[name].global_descriptor for name in images.keys()},
        keypoints=keypoints,
        pq_codes=image_codes,
    )
    _put_reconstruction_object(key="global_descriptors.h5", body=global_descriptors_file.read_bytes())
    _put_reconstruction_object(key="features.h5", body=features_file.read_bytes())

    # Load LightGlue model
    print("Loading LightGlue")
    lightglue = load_lightglue(DEVICE)

    # Match features for all image pairs
    print("Matching features")
    matches_gpu = {
        (imageA, imageB): lightglue({
            "image0": {
                "keypoints": images[imageA].keypoints.unsqueeze(0),
                "descriptors": images[imageA].descriptors.unsqueeze(0),
                "image_size": torch.tensor([images[imageA].size], device=DEVICE),
            },
            "image1": {
                "keypoints": images[imageB].keypoints.unsqueeze(0),
                "descriptors": images[imageB].descriptors.unsqueeze(0),
                "image_size": torch.tensor([images[imageB].size], device=DEVICE),
            },
        })["matches0"][0]
        for imageA, imageB in iterate_and_log(canonical_pairs, label="Matching features")
    }

    # Move matches to CPU and convert to numpy
    matches = {key: value.cpu().numpy().astype(int32) for key, value in matches_gpu.items()}

    # Extract keypoint indices for matches
    match_indices: dict[tuple[str, str], tuple[NDArray[intp], NDArray[intp]]] = {}
    for image_a, image_b in canonical_pairs:
        image_a_keypoint_to_image_b_keypoint = matches[(image_a, image_b)]

        # Create mask of actual matches (where the keypoint index in image B for a keypoint in image A is not -1)
        mask = image_a_keypoint_to_image_b_keypoint >= 0

        # Extract indices of valid matches
        image_a_indices = nonzero(mask)[0]
        image_b_indices = image_a_keypoint_to_image_b_keypoint[mask]
        match_indices[(image_a, image_b)] = (image_a_indices, image_b_indices)

    # Create COLMAP database
    database = Database.open(str(COLMAP_DB_PATH))

    # Write cameras, images, keypoints, and pose priors to database
    colmap_image_ids: dict[str, int] = {}
    for rig_id, rig in rigs.items():
        for camera_id, camera in rig.cameras.items():
            colmap_camera_id = database.write_camera(camera[1])

            for frame_id, transform in rig.frame_poses.items():
                image_name = f"{rig_id}/{camera_id}/{frame_id}.jpg"

                colmap_image_ids[image_name] = database.write_image(
                    pycolmapImage(name=image_name, camera_id=colmap_camera_id)
                )
                database.write_keypoints(colmap_image_ids[image_name], keypoints[image_name])

                # Only write pose prior for images from reference sensors (all others are implied by rig)
                if camera[0].ref_sensor:
                    database.write_pose_prior(
                        colmap_image_ids[image_name],
                        PosePrior(
                            position=transform.translation.reshape(3, 1),
                            position_covariance=(POSE_PRIOR_POS_SIGMA_M**2) * eye(3, dtype=float64),
                            coordinate_system=PosePriorCoordinateSystem.CARTESIAN,
                        ),
                    )

    # Apply rig configuration to database (must be done after writing cameras and images)
    apply_rig_config([rig.colmap_rig_config for rig in rigs.values()], database)

    # Write matches to database
    for a, b in canonical_pairs:
        (image_a_indices, image_b_indices) = match_indices[(a, b)]
        database.write_matches(
            colmap_image_ids[a],
            colmap_image_ids[b],
            stack((image_a_indices, image_b_indices), axis=1).astype(uint32, copy=False),
        )

    # Close database
    database.close()

    # Perform rig-aware geometric verification of matches
    print("Verifying geometry for matches")
    match_spatial(
        database_path=str(COLMAP_DB_PATH),
        matching_options=options_builder.feature_matching_options(),
        verification_options=options_builder.two_view_geometry_options(),
    )

    # Compute and store verified matches metrics
    metrics_builder.build_verified_matches_metrics(canonical_pairs)

    # Run incremental mapping
    print("Running incremental mapping")
    CAPTURE_SESSION_DIRECTORY.mkdir(parents=True, exist_ok=True)
    reconstructions = incremental_mapping(
        database_path=str(COLMAP_DB_PATH),
        image_path=str(CAPTURE_SESSION_DIRECTORY),
        output_path=str(COLMAP_SFM_DIRECTORY),
        options=options_builder.incremental_pipeline_options(),
    )

    # Check that at least one reconstruction was created
    if len(reconstructions) == 0:
        manifest.status = "failed"
        manifest.error = "No model was created"

    # Choose the reconstruction with the most registered images
    # TODO: Write information to metrics about this for visibility
    best_id = max(range(len(reconstructions)), key=lambda i: reconstructions[i].num_reg_images())
    best = reconstructions[best_id]

    # Compute and store reconstruction metrics
    metrics_builder.build_reconstruction_metrics(best)

    best_out = COLMAP_SFM_DIRECTORY / "best"
    best_out.mkdir(parents=True, exist_ok=True)
    best.write_text(str(best_out))
    best.export_PLY(str(best_out / "points3D.ply"))

    for file_path in best_out.rglob("*"):
        if file_path.is_file():
            _put_reconstruction_object(key=f"sfm_model/{file_path.relative_to(best_out)}", body=file_path.read_bytes())

    manifest.status = "succeeded"
    _put_reconstruction_object(key="manifest.json", body=manifest.model_dump_json().encode("utf-8"))


if __name__ == "__main__":
    main()
