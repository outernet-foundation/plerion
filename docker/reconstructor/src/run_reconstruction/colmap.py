from __future__ import annotations

from pathlib import Path
from typing import Any

from numpy import eye, float64, intp, stack, uint32
from numpy.typing import NDArray
from pycolmap import Database, PosePrior, PosePriorCoordinateSystem
from pycolmap import Image as pycolmapImage
from pycolmap._core import apply_rig_config, incremental_mapping, match_spatial
from torch import cuda

from .metrics_builder import ReconstructionMetricsBuilder
from .options_builder import ReconstructionOptionsBuilder
from .rig import Rig

DEVICE = "cuda" if cuda.is_available() else "cpu"
print(f"Using device: {DEVICE}")

WORK_DIR = Path("/tmp/reconstruction")
OUTPUT_DIRECTORY = WORK_DIR / "outputs"
CAPTURE_SESSION_DIRECTORY = WORK_DIR / "capture_session"
COLMAP_DB_PATH = OUTPUT_DIRECTORY / "database.db"
COLMAP_SFM_DIRECTORY = OUTPUT_DIRECTORY / "sfm_model"
COLMAP_SFM_DIRECTORY.mkdir(parents=True, exist_ok=True)
POSE_PRIOR_POS_SIGMA_M = 0.25


def run_colmap_reconstruction(
    options_builder: ReconstructionOptionsBuilder,
    metrics_builder: ReconstructionMetricsBuilder,
    rigs: dict[str, Rig],
    keypoints: dict[str, Any],
    canonical_pairs: list[tuple[str, str]],
    match_indices: dict[tuple[str, str], tuple[NDArray[intp], NDArray[intp]]],
):
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
    #
    # Calling match_spatial() when matches already exist in the database skips doing actual
    # SIFT-based feature matching, instead only performing geometric verification on those matches
    # and adding verified two-view geometries. And because a rig configuration exists in the
    # database, that verification is rig-aware
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
    reconstructions = incremental_mapping(
        database_path=str(COLMAP_DB_PATH),
        image_path=str(CAPTURE_SESSION_DIRECTORY),
        output_path=str(COLMAP_SFM_DIRECTORY),
        options=options_builder.incremental_pipeline_options(),
    )

    # Check that at least one reconstruction was created
    if len(reconstructions) == 0:
        return None

    # Choose the reconstruction with the most registered images
    # TODO: Write information to metrics about this for visibility
    return reconstructions[max(range(len(reconstructions)), key=lambda i: reconstructions[i].num_reg_images())]
