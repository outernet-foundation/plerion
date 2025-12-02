from __future__ import annotations

from io import BytesIO
from pathlib import Path
from tarfile import open as open_tar
from typing import Iterable, Iterator, Literal, TypeVar

from common.boto_clients import create_s3_client
from core.map import MapWriter
from core.opq import encode_descriptors, train_opq_matrix, train_pq_quantizer
from core.reconstruction_manifest import ReconstructionManifest
from core.rig import Config
from core.utility import to_f32
from neural_networks.dir import load_DIR
from neural_networks.image import Image
from neural_networks.lightglue import lightglue_match, load_lightglue, load_superpoint
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

    def create_image(image_path: str, rotation: Literal["None", "90_CW", "180", "90_CCW"]) -> Image:
        with open(CAPTURE_SESSION_DIRECTORY / image_path, "rb") as img_file:
            return Image.from_buffer(img_file.read(), rotation, superpoint, dir, DEVICE)

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
    metrics_builder.metrics.average_keypoints_per_image = float(
        sum(len(img.keypoints) for img in images.values()) / len(images)
    )

    training_unit = ascontiguousarray(vstack([to_f32(images[name].descriptors) for name in images.keys()]))

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
        opq_matrix, product_quantizer, {name: images[name].descriptors for name in images.keys()}
    )

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
    match_indices = lightglue_match(lightglue, canonical_pairs, images, batch_size=32, device=DEVICE)

    reconstruction = run_colmap_reconstruction(
        options_builder, metrics_builder, rigs, keypoints, canonical_pairs, match_indices
    )

    manifest.metrics = metrics_builder.metrics
    if reconstruction is None:
        manifest.status = "failed"
        manifest.error = "No model was created"
    else:
        map_writer.write_reconstruction(reconstruction)


T = TypeVar("T")


def _iterate_and_log(iterable: Iterable[T], every: int = 1, label: str = "Progress") -> Iterator[T]:
    for i, item in enumerate(iterable, start=1):
        if i % every == 0:
            print(f"{label}: {i} items processed", flush=True)
        yield item


if __name__ == "__main__":
    main()
