from __future__ import annotations

import json
from io import BytesIO
from itertools import combinations
from pathlib import Path
from tarfile import open as open_tar
from typing import Iterable, List, Optional, Sequence, Tuple, cast

import torch
from common.boto_clients import create_s3_client
from core.reconstruction_manifest import (
    ReconstructionManifest,
    ReconstructionMetricsBuilder,
    ReconstructionOptionsBuilder,
)
from core.rig import Config
from core.ugh import Rig

# from core.ugh import create_colmap_rig
from faiss import (  # type: ignore
    OPQMatrix,
    ProductQuantizer,
    write_ProductQuantizer,  # type: ignore
    write_VectorTransform,  # type: ignore
)
from h5py import File
from neural_networks.dir import load_DIR
from neural_networks.image import create_image_tensors
from neural_networks.lightglue import load_lightglue, load_superpoint
from numpy import (
    asarray,
    ascontiguousarray,
    eye,
    float16,
    float32,
    float64,
    int32,
    nonzero,
    percentile,
    random,
    stack,
    uint8,
    uint32,
    vstack,
)
from numpy.linalg import norm
from numpy.typing import NDArray
from pycolmap import Database, ImageMap, Point3D, Point3DMap, PosePrior, PosePriorCoordinateSystem
from pycolmap import Image as PycolmapImage
from pycolmap import Image as pycolmapImage
from pycolmap._core import apply_rig_config, incremental_mapping, match_spatial, set_random_seed
from torch import Tensor, cuda, no_grad

from .find_pairs import pairs_from_poses
from .settings import get_settings

settings = get_settings()

DEVICE = "cuda" if cuda.is_available() else "cpu"
print(f"Using device: {DEVICE}")

UINT64_MAX = 18446744073709551615  # sentinel used by Point2D.point3D_id default

WORK_DIR = Path("/tmp/reconstruction")
OUTPUT_DIRECTORY = WORK_DIR / "outputs"
CAPTURE_SESSION_DIRECTORY = WORK_DIR / "capture_session"
PAIRS_FILE = WORK_DIR / "pairs.txt"
GLOBAL_DESCRIPTORS_FILE = WORK_DIR / "global_descriptors.h5"
FEATURES_FILE = WORK_DIR / "features.h5"
OPQ_MATRIX_FILE = WORK_DIR / "opq_matrix.tf"
PQ_QUANTIZER_FILE = WORK_DIR / "pq_quantizer.pq"
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


def _put_reconstruction_object(key: str, body: bytes):
    print(f"Putting object in bucket {settings.reconstructions_bucket} with key {settings.reconstruction_id}/{key}")
    s3_client.put_object(Bucket=settings.reconstructions_bucket, Key=f"{settings.reconstruction_id}/{key}", Body=body)


def _to_f32(t: Tensor) -> NDArray[float32]:
    return t.detach().cpu().numpy().astype(float32, copy=False)


def _to_f16(t: Tensor) -> NDArray[float16]:
    return t.detach().cpu().numpy().astype(float16, copy=False)


class Image:
    def __init__(self, colmap_id: int, global_descriptor: Tensor, keypoints: Tensor, descriptors: Tensor, size: int):
        self.colmap_id = colmap_id
        self.global_descriptor = global_descriptor
        self.keypoints = keypoints
        self.descriptors = descriptors
        self.size = size


def main():
    print(f"Starting reconstruction {settings.reconstruction_id}")

    manifest = ReconstructionManifest.model_validate_json(
        s3_client.get_object(Bucket=settings.reconstructions_bucket, Key=f"{settings.reconstruction_id}/manifest.json")[
            "Body"
        ].read()
    )

    options_builder = ReconstructionOptionsBuilder(manifest.options)
    metrics_builder = ReconstructionMetricsBuilder(COLMAP_DB_PATH)

    if manifest.options.random_seed is not None:
        random.seed(manifest.options.random_seed)
        set_random_seed(manifest.options.random_seed)

    dir = load_DIR(DEVICE)
    superpoint = load_superpoint(max_num_keypoints=manifest.options.max_keypoints_per_image, device=DEVICE)
    lightglue = load_lightglue(DEVICE)

    if COLMAP_DB_PATH.exists():
        COLMAP_DB_PATH.unlink()
    database = Database.open(str(COLMAP_DB_PATH))

    print("Fetching capture data")
    capture_tar = s3_client.get_object(Bucket=settings.captures_bucket, Key=f"{settings.capture_id}.tar")["Body"].read()

    # extract tar to temporary directory
    with open_tar(fileobj=BytesIO(capture_tar), mode="r:*") as tar:
        tar.extractall(path=CAPTURE_SESSION_DIRECTORY)

    def _get_capture_object(key: str) -> bytes:
        file_path = CAPTURE_SESSION_DIRECTORY / key
        with open(file_path, "rb") as f:
            return f.read()

    rigs: dict[str, Rig] = {
        rig.id: Rig(rig, _get_capture_object(f"{rig.id}/frames.csv").decode("utf-8"))
        for rig in Config.model_validate(json.loads(_get_capture_object("config.json").decode("utf-8"))).rigs
    }
    images: dict[str, Image] = {}
    for rig_id, rig in rigs.items():
        for camera in rig.cameras.values():
            colmap_camera_id = database.write_camera(camera[1])

            for frame_id, transform in rig.frame_poses.items():
                name = f"{rig_id}/{camera[0].id}/{frame_id}.jpg"
                print(f"Processing image: {name}")

                colmap_image_id = database.write_image(pycolmapImage(name=name, camera_id=colmap_camera_id))

                if camera[0].ref_sensor:
                    database.write_pose_prior(
                        colmap_image_id,
                        PosePrior(
                            position=transform.translation.reshape(3, 1),
                            position_covariance=(POSE_PRIOR_POS_SIGMA_M**2) * eye(3, dtype=float64),
                            coordinate_system=PosePriorCoordinateSystem.CARTESIAN,
                        ),
                    )

                rbg_image_tensor, grayscale_image_tensor, image_size = create_image_tensors(
                    camera[0].camera_config.rotation,
                    _get_capture_object(f"{rig_id}/{camera[0].id}/{frame_id}.jpg"),
                    device=DEVICE,
                )

                with no_grad():
                    # Extract global descriptor using NetVLAD
                    global_descriptor = dir({"image": rbg_image_tensor})["global_descriptor"][0]

                    # Extract local features and descriptors using SuperPoint
                    superpoint_output = superpoint({"image": grayscale_image_tensor})
                    keypoints = superpoint_output["keypoints"][0]
                    descriptors = superpoint_output["descriptors"][0]

                    keypoints_array = keypoints.detach().cpu().numpy().astype(float32, copy=False)
                    database.write_keypoints(colmap_image_id, keypoints_array)

                images[name] = Image(
                    colmap_id=colmap_image_id,
                    global_descriptor=global_descriptor,
                    keypoints=keypoints,
                    descriptors=descriptors,
                    size=image_size,
                )

    metrics_builder.metrics.average_keypoints_per_image = float(
        sum(len(img.keypoints) for img in images.values()) / len(images)
    )

    pairs = [
        # Cross-frame, cross-sensor pairs by frame proximity
        (f"{rig_id_a}/{camera_a[0].id}/{frame_id_a}.jpg", f"{rig_id_b}/{camera_b[0].id}/{frame_id_b}.jpg")
        for (rig_id_a, frame_id_a), (rig_id_b, frame_id_b) in pairs_from_poses(
            {
                (rig_id, frame_id): transform
                for rig_id, rig in rigs.items()
                for frame_id, transform in rig.frame_poses.items()
            },
            min(manifest.options.neighbors_count or DEFAULT_NEIGHBORS_COUNT, max(1, len(images) - 1)),
        )
        for camera_a in rigs[rig_id_a].cameras.values()
        for camera_b in rigs[rig_id_b].cameras.values()
    ] + [
        # Intra-frame, cross-sensor pairs
        (f"{rig_id}/{camera_a[0].id}/{frame_id}.jpg", f"{rig_id}/{camera_b[0].id}/{frame_id}.jpg")
        for rig_id, rig in rigs.items()
        for frame_id in rig.frame_poses.keys()
        for camera_a, camera_b in combinations(list(rig.cameras.values()), 2)
    ]

    # Canonicalize and deduplicate pairs
    canonical_pairs = sorted({tuple(sorted((a, b))) for a, b in pairs if a != b})

    PAIRS_FILE.parent.mkdir(parents=True, exist_ok=True)
    PAIRS_FILE.write_text("\n".join(f"{a} {b}" for a, b in sorted({tuple(sorted((a, b))) for a, b in pairs if a != b})))

    # Match features using SuperGlue and add matches to COLMAP database
    all_matches: dict[Tuple[str, str], Tensor] = {}
    for imageA, imageB in canonical_pairs:
        print(f"  {imageA}:{imageB}")

        image_size = torch.tensor([images[imageA].size], device=DEVICE)

        all_matches[(imageA, imageB)] = lightglue({
            "image0": {
                "keypoints": images[imageA].keypoints.unsqueeze(0),
                "descriptors": images[imageA].descriptors.unsqueeze(0),
                "image_size": image_size,
            },
            "image1": {
                "keypoints": images[imageB].keypoints.unsqueeze(0),
                "descriptors": images[imageB].descriptors.unsqueeze(0),
                "image_size": image_size,
            },
        })["matches0"][0]

    valid_matches: dict[Tuple[str, str], NDArray[int32]] = {}
    for imageA, imageB in canonical_pairs:
        matches = all_matches[(imageA, imageB)].cpu().numpy().astype(int32)

        # Filter invalid matches
        valid_mask = matches >= 0
        query_indices = nonzero(valid_mask)[0]
        train_indices = matches[valid_mask]

        # Convert matches to COLMAP format
        valid_matches[(imageA, imageB)] = stack((query_indices, train_indices), axis=1).astype(int32)

    # Write two-view geometries (matches) to database
    for a, b in canonical_pairs:
        database.write_matches(
            images[a].colmap_id, images[b].colmap_id, valid_matches[(a, b)].astype(uint32, copy=False)
        )

    # Apply rig configuration to database
    # (This must be done after all cameras and images have been added)
    apply_rig_config([rig.colmap_rig_config for rig in rigs.values()], database)

    database.close()

    match_spatial(
        database_path=str(COLMAP_DB_PATH),
        matching_options=options_builder.feature_matching_options(),
        verification_options=options_builder.two_view_geometry_options(),
    )

    metrics_builder.build_verified_matches_metrics(canonical_pairs)

    CAPTURE_SESSION_DIRECTORY.mkdir(parents=True, exist_ok=True)
    reconstructions = incremental_mapping(
        database_path=str(COLMAP_DB_PATH),
        image_path=str(CAPTURE_SESSION_DIRECTORY),
        output_path=str(COLMAP_SFM_DIRECTORY),
        options=options_builder.incremental_pipeline_options(),
    )

    if len(reconstructions) == 0:
        manifest.status = "failed"
        manifest.error = "No model was created"

    # Choose the reconstruction with the most registered images
    best_id = max(range(len(reconstructions)), key=lambda i: reconstructions[i].num_reg_images())
    best = reconstructions[best_id]

    best_out = COLMAP_SFM_DIRECTORY / "best"
    best_out.mkdir(parents=True, exist_ok=True)
    best.write_text(str(best_out))
    best.export_PLY(str(best_out / "points3D.ply"))

    # opq + pq
    number_of_subvectors = 16
    number_of_bits_per_subvector = 8
    opq_iterations = 20
    manifest.options.compression_opq_number_of_subvectors = number_of_subvectors
    manifest.options.compression_opq_number_bits_per_subvector = number_of_bits_per_subvector
    manifest.options.compression_opq_number_of_training_iterations = opq_iterations

    training_descriptors = vstack([_to_f32(images[name].descriptors) for name in images.keys()])
    dimension = int(training_descriptors.shape[1])

    # Train OPQ matrix
    opq_matrix = OPQMatrix(dimension, number_of_subvectors)
    opq_matrix.niter = opq_iterations
    opq_matrix.verbose = True
    training_unit = ascontiguousarray(training_descriptors)
    opq_matrix.train(training_unit)  # type: ignore
    write_VectorTransform(opq_matrix, str(OPQ_MATRIX_FILE))

    # Train PQ quantizer on OPQ-rotated descriptors
    rotated_training_unit = opq_matrix.apply(training_unit)  # type: ignore
    faiss_pq = ProductQuantizer(dimension, number_of_subvectors, number_of_bits_per_subvector)
    faiss_pq.verbose = True
    faiss_pq.train(rotated_training_unit)  # type: ignore
    write_ProductQuantizer(faiss_pq, str(PQ_QUANTIZER_FILE))

    # Compute metrics
    points3d: Point3DMap = best.points3D
    points3d_values: Iterable[Point3D] = cast(Iterable[Point3D], points3d.values())  # type: ignore
    reconstruction_images: ImageMap = best.images
    reconstruction_image_values: Iterable[PycolmapImage] = cast(Iterable[PycolmapImage], reconstruction_images.values())  # type: ignore
    track_lengths = [len(p.track.elements) for p in points3d_values]

    reproject_errors: List[float] = []
    for image in reconstruction_image_values:
        for point2d in image.points2D:
            if point2d.point3D_id == UINT64_MAX or point2d.point3D_id not in points3d:
                continue
            projection: Optional[NDArray[float64]] = image.project_point(points3d[point2d.point3D_id].xyz)
            if projection is None:
                continue
            reproject_errors.append(
                float(
                    norm(asarray(projection, dtype=float64).reshape(2) - asarray(point2d.xy, dtype=float64).reshape(2))
                )
            )

    manifest.metrics.total_images = len(images)
    manifest.metrics.registered_images = best.num_reg_images()
    manifest.metrics.registration_rate = float(best.num_reg_images() / len(images) * 100.0)
    manifest.metrics.num_3d_points = len(points3d)
    manifest.metrics.track_length_50th_percentile = _percentile([float(L) for L in track_lengths], 50.0)
    manifest.metrics.percent_tracks_with_length_greater_than_or_equal_to_3 = float(
        sum(1 for L in track_lengths if L >= 3) / float(len(track_lengths)) * 100.0
    )

    def q(x: float, eps: float = 1e-9) -> float:
        return float(round(x / eps) * eps)

    manifest.metrics.reprojection_pixel_error_50th_percentile = q(_percentile(reproject_errors, 50.0))
    manifest.metrics.reprojection_pixel_error_90th_percentile = q(_percentile(reproject_errors, 90.0))
    _put_reconstruction_object(key="manifest.json", body=manifest.model_dump_json().encode("utf-8"))

    print("Uploading reconstruction results")

    manifest.status = "uploading"
    _put_reconstruction_object(key="manifest.json", body=manifest.model_dump_json().encode("utf-8"))

    with File(str(GLOBAL_DESCRIPTORS_FILE), "w") as gfile:
        # for name, gdesc in global_descriptors.items():
        for name, gdesc in ((name, images[name].global_descriptor) for name in images.keys()):
            group = gfile.require_group(name)  # type: ignore
            if "global_descriptor" in group:
                del group["global_descriptor"]
            group.create_dataset("global_descriptor", data=_to_f32(gdesc), compression="gzip", compression_opts=3)  # type: ignore

    with File(str(FEATURES_FILE), "w") as ffile:
        # for name in global_descriptors.keys():  # ensures the same set/order as globals
        for name in images.keys():
            group = ffile.require_group(name)  # type: ignore

            # Clean any existing datasets
            if "keypoints" in group:
                del group["keypoints"]
            if "descriptors" in group:  # we no longer store raw float descriptors
                del group["descriptors"]
            if "scores" in group:
                del group["scores"]
            if "pq_codes" in group:
                del group["pq_codes"]

            # write FP16 + gzip-9 + shuffle + chunks
            group.create_dataset(  # type: ignore
                "keypoints",
                data=_to_f16(images[name].keypoints),
                compression="gzip",
                compression_opts=9,
                shuffle=True,
                chunks=True,
            )

            # Descriptors -> OPQ rotate (via apply) -> PQ encode
            descriptors_contiguous = ascontiguousarray(_to_f32(images[name].descriptors))
            descriptors_rotated = cast(NDArray[float32], opq_matrix.apply(descriptors_contiguous))  # type: ignore
            codes = cast(NDArray[uint8], faiss_pq.compute_codes(descriptors_rotated))  # type: ignore

            group.create_dataset(  # type: ignore
                "pq_codes", data=codes, compression="gzip", compression_opts=9, shuffle=True, chunks=True
            )

    _put_reconstruction_object(key="global_descriptors.h5", body=GLOBAL_DESCRIPTORS_FILE.read_bytes())
    _put_reconstruction_object(key="features.h5", body=FEATURES_FILE.read_bytes())
    _put_reconstruction_object(key="opq_matrix.tf", body=OPQ_MATRIX_FILE.read_bytes())
    _put_reconstruction_object(key="pq_quantizer.pq", body=PQ_QUANTIZER_FILE.read_bytes())
    _put_reconstruction_object(key="pairs.txt", body=PAIRS_FILE.read_bytes())

    for file_path in best_out.rglob("*"):
        if file_path.is_file():
            _put_reconstruction_object(key=f"sfm_model/{file_path.relative_to(best_out)}", body=file_path.read_bytes())

    manifest.status = "succeeded"
    _put_reconstruction_object(key="manifest.json", body=manifest.model_dump_json().encode("utf-8"))


def _percentile(xs: Sequence[float], q: float):
    arr = asarray(xs, dtype=float64)
    if arr.size == 0:
        raise ValueError("Cannot compute percentile of empty array")
    return float(percentile(arr, q))


if __name__ == "__main__":
    main()
