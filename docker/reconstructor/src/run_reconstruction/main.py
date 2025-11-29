from __future__ import annotations

from io import BytesIO
from pathlib import Path
from tarfile import open as open_tar
from typing import Iterable, Iterator, Literal, TypeVar

import torch
from common.boto_clients import create_s3_client
from core.map import MapWriter
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

from .opq import train_opq_and_encode
from .pairs import generate_image_pairs
from .rig import Rig
from .settings import get_settings

settings = get_settings()

DEVICE = "cuda" if cuda.is_available() else "cpu"
print(f"Using device: {DEVICE}")

WORK_DIR = Path("/tmp/reconstruction")
OUTPUT_DIRECTORY = WORK_DIR / "outputs"
CAPTURE_SESSION_DIRECTORY = WORK_DIR / "capture_session"
COLMAP_DB_PATH = OUTPUT_DIRECTORY / "database.db"
COLMAP_SFM_DIRECTORY = OUTPUT_DIRECTORY / "sfm_model"
COLMAP_SFM_DIRECTORY.mkdir(parents=True, exist_ok=True)
DEFAULT_NEIGHBORS_COUNT = 12
ROTATION_THRESHOLD_DEG = 30.0
POSE_PRIOR_POS_SIGMA_M = 0.25

s3_client = create_s3_client(
    s3_endpoint_url=settings.s3_endpoint_url, s3_access_key=settings.s3_access_key, s3_secret_key=settings.s3_secret_key
)


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
    map_writer = MapWriter(
        WORK_DIR, manifest, s3_client, COLMAP_DB_PATH, settings.reconstructions_bucket, settings.reconstruction_id
    )

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

    canonical_pairs = generate_image_pairs(
        rigs, manifest.options.neighbors_count or DEFAULT_NEIGHBORS_COUNT, ROTATION_THRESHOLD_DEG
    )
    map_writer.write_pairs(canonical_pairs)

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
        for rig_id, camera, frame_id in _iterate_and_log(
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
    map_writer.metrics.metrics.average_keypoints_per_image = float(
        sum(len(img.keypoints) for img in images.values()) / len(images)
    )

    # Train OPQ and PQ, and encode all image descriptors
    print("Training OPQ and PQ, and encoding descriptors")
    opq_matrix, product_quantizer, image_codes = train_opq_and_encode(
        manifest.options, {key: images[key].descriptors for key in images.keys()}
    )

    map_writer.write_opq(opq_matrix, product_quantizer)

    map_writer.write_h5_features(
        global_descriptors={name: images[name].global_descriptor for name in images.keys()},
        keypoints=keypoints,
        pq_codes=image_codes,
    )

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
        for imageA, imageB in _iterate_and_log(canonical_pairs, label="Matching features")
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
        matching_options=map_writer.options.feature_matching_options(),
        verification_options=map_writer.options.two_view_geometry_options(),
    )

    # Compute and store verified matches metrics
    map_writer.metrics.build_verified_matches_metrics(canonical_pairs)

    # Run incremental mapping
    print("Running incremental mapping")
    reconstructions = incremental_mapping(
        database_path=str(COLMAP_DB_PATH),
        image_path=str(CAPTURE_SESSION_DIRECTORY),
        output_path=str(COLMAP_SFM_DIRECTORY),
        options=map_writer.options.incremental_pipeline_options(),
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
    map_writer.metrics.build_reconstruction_metrics(best)

    # Write reconstruction results
    map_writer.write_reconstruction(best)


T = TypeVar("T")


def _iterate_and_log(iterable: Iterable[T], every: int = 1, label: str = "Progress") -> Iterator[T]:
    for i, item in enumerate(iterable, start=1):
        if i % every == 0:
            print(f"{label}: {i} items processed", flush=True)
        yield item


if __name__ == "__main__":
    main()
