from __future__ import annotations

from io import BytesIO
from pathlib import Path
from tarfile import open as open_tar
from typing import Any, Literal

from common.boto_clients import create_s3_client
from core.dir import dir_extract
from core.image import create_tensors_from_path
from core.lightglue import lightglue_match_tensors
from core.map import MapWriter
from core.opq import encode_descriptors, train_opq_matrix, train_pq_quantizer
from core.reconstruction_manifest import ReconstructionManifest
from core.rig import Config
from core.superpoint import superpoint_extract
from core.utility import to_f32
from neural_networks.models import load_DIR, load_lightglue, load_superpoint
from numpy import ascontiguousarray, float32, random, vstack
from pycolmap._core import set_random_seed
from torch import cuda

from .colmap import run_colmap_reconstruction
from .metrics_builder import ReconstructionMetricsBuilder
from .options_builder import ReconstructionOptionsBuilder
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
    options_builder = ReconstructionOptionsBuilder(manifest.options)
    metrics_builder = ReconstructionMetricsBuilder(COLMAP_DB_PATH)

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

    images: list[tuple[str, Literal["None", "90_CW", "180", "90_CCW"]]] = [
        (f"{rig_id}/{camera[0].id}/{frame_id}.jpg", camera[0].camera_config.rotation)
        for rig_id, rig in rigs.items()
        for camera in rig.cameras.values()
        for frame_id in rig.frame_poses.keys()
    ]

    global_descriptors: dict[str, Any] = {}
    keypoints: dict[str, Any] = {}
    descriptors: dict[str, Any] = {}
    sizes: dict[str, tuple[int, int]] = {}
    batch_size = 8
    num_batches = (len(images) + batch_size - 1) // batch_size
    for batch_start in range(0, len(images), batch_size):
        print(f"Processing image tensor batch {batch_start // batch_size + 1} of {num_batches}")
        image_tensors = {
            name: create_tensors_from_path(image_path=CAPTURE_SESSION_DIRECTORY / name, camera_rotation=camera_rotation)
            for name, camera_rotation in images[batch_start : batch_start + batch_size]
        }
        global_descriptors.update(
            dir_extract(
                dir,
                {name: image_tensors[name][0] for name in image_tensors.keys()},
                batch_size=batch_size,
                device=DEVICE,
            )
        )
        batch_keypoints, batch_descriptors = superpoint_extract(
            superpoint,
            {name: image_tensors[name][1] for name in image_tensors.keys()},
            batch_size=batch_size,
            device=DEVICE,
        )
        keypoints.update(batch_keypoints)
        descriptors.update(batch_descriptors)
        sizes.update({name: image_tensors[name][2] for name in image_tensors.keys()})

    # print("Creating image tensors")
    # image_tensors = {
    #     f"{rig_id}/{camera[0].id}/{frame_id}.jpg": create_tensors_from_path(
    #         image_path=CAPTURE_SESSION_DIRECTORY / f"{rig_id}/{camera[0].id}/{frame_id}.jpg",
    #         camera_rotation=camera[0].camera_config.rotation,
    #     )
    #     for rig_id, rig in rigs.items()
    #     for camera in rig.cameras.values()
    #     for frame_id in rig.frame_poses.keys()
    # }

    # print("Extracting DIR global descriptors")
    # global_descriptors = dir_extract(
    #     dir, {name: image_tensors[name][0] for name in image_tensors.keys()}, batch_size=8, device=DEVICE
    # )

    # print("Extracting SuperPoint keypoints and descriptors")
    # keypoints, descriptors = superpoint_extract(
    #     superpoint, {name: image_tensors[name][1] for name in image_tensors.keys()}, batch_size=8, device=DEVICE
    # )

    keypoints_cpu = {
        name: keypoints[name].detach().cpu().numpy().astype(float32, copy=False) for name in keypoints.keys()
    }

    # Compute and store average keypoints per image metric
    metrics_builder.metrics.average_keypoints_per_image = float(
        sum(len(keypoints_cpu[name]) for name in keypoints_cpu.keys()) / len(keypoints_cpu)
    )

    training_unit = ascontiguousarray(vstack([to_f32(descriptors[name]) for name in descriptors.keys()]))

    print("Training OPQ matrix")
    opq_matrix = train_opq_matrix(
        options_builder.compression_opq_number_of_subvectors(),
        options_builder.compression_opq_number_of_training_iterations(),
        training_unit,
    )
    map_writer.write_opq_matrix(opq_matrix)

    print("Training PQ quantizer")
    product_quantizer = train_pq_quantizer(
        options_builder.compression_opq_number_of_subvectors(),
        options_builder.compression_opq_number_of_bits_per_subvector(),
        opq_matrix,
        training_unit,
    )
    map_writer.write_pq_product_quantizer(product_quantizer)

    print("Encoding image descriptors")
    image_codes = encode_descriptors(
        opq_matrix, product_quantizer, {name: descriptors[name] for name in descriptors.keys()}
    )

    map_writer.write_h5_features(
        global_descriptors={name: global_descriptors[name] for name in global_descriptors.keys()},
        keypoints=keypoints_cpu,
        pq_codes=image_codes,
    )

    # Load LightGlue model
    print("Loading LightGlue")
    lightglue = load_lightglue(DEVICE)

    # Match features for all image pairs
    print("Matching features")
    # match_indices = lightglue_match(lightglue, canonical_pairs, images, batch_size=32, device=DEVICE)
    match_indices = lightglue_match_tensors(
        lightglue,
        canonical_pairs,
        {k: keypoints[k] for k in keypoints.keys()},
        {k: descriptors[k] for k in descriptors.keys()},
        {k: sizes[k] for k in sizes.keys()},
        batch_size=32,
        device=DEVICE,
    )

    reconstruction = run_colmap_reconstruction(
        options_builder, metrics_builder, rigs, keypoints_cpu, canonical_pairs, match_indices
    )

    manifest.metrics = metrics_builder.metrics
    if reconstruction is None:
        manifest.status = "failed"
        manifest.error = "No model was created"
    else:
        map_writer.write_reconstruction(reconstruction)


if __name__ == "__main__":
    main()
