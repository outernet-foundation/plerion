from pathlib import Path
from statistics import mean, median
from typing import Any, List, Literal, Optional

# from core.ugh import create_colmap_rig
from pycolmap import Database, FeatureMatchingOptions, IncrementalPipelineOptions, TwoViewGeometryOptions
from pycolmap import Image as pycolmapImage
from pydantic import BaseModel, Field


class ReconstructionOptions(BaseModel):
    random_seed: Optional[int] = Field(default=None, description="Random seed to use (for deterministic behavior).")
    single_threaded: Optional[bool] = Field(
        default=None, description="If true, run reconstruction in single-threaded mode (for deterministic behavior)."
    )
    neighbors_count: Optional[int] = Field(
        default=None,
        description=(
            "How many pose-nearest neighbors to consider when generating image pairs. "
            "Use -1 for exhaustive matching (all pairs). If None, a sensible default is used (currently 12). "
            "Smaller values reduce weak overlaps and speed up matching at the cost of some coverage."
        ),
    )
    max_keypoints_per_image: Optional[int] = Field(
        default=None,
        description=(
            "Upper bound on detected local features per image (SuperPoint). "
            "Reduces descriptor/match volume and downstream 3D points; too small can hurt registration."
        ),
    )
    ransac_max_error: Optional[float] = Field(
        default=None,
        description=(
            "Two-view RANSAC inlier threshold (pixels) used by verify_matches(). "
            "Lower = stricter inlier test; removes borderline correspondences before SfM."
        ),
    )
    ransac_min_inlier_ratio: Optional[float] = Field(
        default=None,
        description=(
            "Two-view RANSAC minimum inlier ratio to accept the model. "
            "Higher = reject more weak pairs; typically 0.10–0.20 for stricter matching."
        ),
    )
    use_prior_position: Optional[bool] = Field(
        default=None,
        description=(
            "If true, use position priors during registration. "
            "This leverages PosePrior(position=...) written into the database to guide image registration."
        ),
    )
    rig_verification: Optional[bool] = Field(
        default=None,
        description=(
            "If true, perform rig-based verification during feature matching and two-view geometry verification. "
            "Requires images to be tagged with rig/camera IDs."
        ),
    )
    triangulation_minimum_angle: Optional[float] = Field(
        default=None,
        description=(
            "Minimum triangulation angle (degrees). Applied at creation time (triangulation.min_angle) and "
            "again during mapper filtering (mapper.filter_min_tri_angle). Raising it removes low-parallax points."
        ),
    )
    triangulation_complete_max_reprojection_error: Optional[float] = Field(
        default=None,
        description=(
            "Triangulation-time gate (pixels) for COMPLETING tracks into new 3D points "
            "(triangulation.complete_max_reproj_error). Lower → fewer borderline new points."
        ),
    )
    triangulation_merge_max_reprojection_error: Optional[float] = Field(
        default=None,
        description=(
            "Triangulation-time gate (pixels) for MERGING near-duplicate 3D points "
            "(triangulation.merge_max_reproj_error). Lower → fewer merges; higher → more aggressive deduplication."
        ),
    )
    mapper_filter_max_reprojection_error: Optional[float] = Field(
        default=None,
        description=(
            "Mapper-level **post-BA outlier** threshold (pixels) (mapper.filter_max_reproj_error). "
            "Points exceeding this after local/global BA are culled. "
            "This is NOT a triangulation accept threshold."
        ),
    )
    bundle_adjustment_refine_sensor_from_rig: Optional[bool] = Field(
        default=None,
        description=(
            "If true, refine per-camera extrinsics within the rig during BA "
            "(ba_refine_sensor_from_rig). Useful when rig calibration is approximate."
        ),
    )
    bundle_adjustment_refine_focal_length: Optional[bool] = Field(
        default=None, description="If true, refine the camera focal length during BA (ba_refine_focal_length)."
    )
    bundle_adjustment_refine_principal_point: Optional[bool] = Field(
        default=None, description="If true, refine the camera principal point during BA (ba_refine_principal_point)."
    )
    bundle_adjustment_refine_additional_params: Optional[bool] = Field(
        default=None,
        description=(
            "If true, refine model-specific additional parameters during BA (ba_refine_extra_params), "
            "e.g., radial/tangential distortion where applicable."
        ),
    )
    compression_opq_number_of_subvectors: Optional[int] = Field(
        default=None, description="Number of subvectors for OPQ compression."
    )
    compression_opq_number_bits_per_subvector: Optional[int] = Field(
        default=None, description="Number of bits per subvector for OPQ compression."
    )
    compression_opq_number_of_training_iterations: Optional[int] = Field(
        default=None, description="Number of training iterations for OPQ compression."
    )


class ReconstructionMetrics(BaseModel):
    total_images: Optional[int] = Field(
        default=None, description="Total number of input images considered for this reconstruction run."
    )
    registered_images: Optional[int] = Field(
        default=None, description="Number of images successfully registered into the final model."
    )
    registration_rate: Optional[float] = Field(
        default=None,
        description=(
            "Registration rate in percent: 100 × (registered_images / total_images). "
            "Computed after selecting the best reconstruction (max registered images)."
        ),
    )
    num_3d_points: Optional[int] = Field(
        default=None, description="Count of 3D points in the selected 'best' reconstruction."
    )
    average_keypoints_per_image: Optional[float] = Field(
        default=None,
        description=(
            "Average number of detected keypoints per image (after SuperPoint extraction), computed across all images."
        ),
    )
    reprojection_pixel_error_50th_percentile: Optional[float] = Field(
        default=None,
        description=(
            "Median (50th percentile) reprojection error in pixels across all valid 2D observations "
            "in registered images, measured using image.project_point(point3D.xyz) vs. observed 2D keypoint."
        ),
    )
    reprojection_pixel_error_90th_percentile: Optional[float] = Field(
        default=None,
        description=(
            "90th percentile reprojection error in pixels across all valid 2D observations, "
            "computed the same way as the median."
        ),
    )
    track_length_50th_percentile: Optional[float] = Field(
        default=None,
        description=(
            "Median (50th percentile) track length across 3D points in the selected model. "
            "Track length = number of distinct images observing the point."
        ),
    )
    percent_tracks_with_length_greater_than_or_equal_to_3: Optional[float] = Field(
        default=None,
        description=(
            "Percentage of 3D points whose track length is ≥ 3 (a common robustness threshold). "
            "Computed as 100 × (#points with length≥3 / #points)."
        ),
    )
    all_verified_matches: Optional[int] = Field(
        default=None, description="Total number of verified matches across all image pairs."
    )
    all_verified_match_rate: Optional[float] = Field(
        default=None, description="Percentage of verified matches across all image pairs."
    )
    all_verified_match_inliers_mean: Optional[float] = Field(
        default=None, description="Mean number of inliers for all verified matches."
    )
    all_verified_match_inliers_median: Optional[float] = Field(
        default=None, description="Median number of inliers for all verified matches."
    )
    stereo_verified_matches: Optional[int] = Field(
        default=None, description="Number of verified matches for stereo pairs (same frame, different sensors)."
    )
    stereo_verified_match_rate: Optional[float] = Field(
        default=None, description="Percentage of verified matches for stereo pairs."
    )
    stereo_verified_match_inliers_mean: Optional[float] = Field(
        default=None, description="Mean number of inliers for verified matches for stereo pairs."
    )
    stereo_verified_match_inliers_median: Optional[float] = Field(
        default=None, description="Median number of inliers for verified matches for stereo pairs."
    )
    same_sensor_verified_matches: Optional[int] = Field(
        default=None, description="Number of verified matches for same-sensor pairs (across frames)."
    )
    same_sensor_verified_match_rate: Optional[float] = Field(
        default=None, description="Percentage of verified matches for same-sensor pairs."
    )
    same_sensor_verified_match_inliers_mean: Optional[float] = Field(
        default=None, description="Mean number of inliers for verified matches for same-sensor pairs."
    )
    same_sensor_verified_match_inliers_median: Optional[float] = Field(
        default=None, description="Median number of inliers for verified matches for same-sensor pairs."
    )
    cross_sensor_verified_matches: Optional[int] = Field(
        default=None, description="Number of verified matches for cross-sensor pairs (across frames)."
    )
    cross_sensor_verified_match_rate: Optional[float] = Field(
        default=None, description="Percentage of verified matches for cross-sensor pairs."
    )
    cross_sensor_verified_match_inliers_mean: Optional[float] = Field(
        default=None, description="Mean number of inliers for verified matches for cross-sensor pairs."
    )
    cross_sensor_verified_match_inliers_median: Optional[float] = Field(
        default=None, description="Median number of inliers for verified matches for cross-sensor pairs."
    )


ReconstructionStatus = Literal[
    "queued",
    "pending",
    "downloading",
    "extracting_features",
    "matching_features",
    "reconstructing",
    "training_opq_matrix",
    "training_product_quantizer",
    "uploading",
    "succeeded",
    "failed",
]


class ReconstructionManifest(BaseModel):
    capture_id: str
    status: ReconstructionStatus
    error: Optional[str] = Field(default=None)
    options: ReconstructionOptions
    metrics: ReconstructionMetrics


class ReconstructionOptionsBuilder:
    def __init__(self, options: ReconstructionOptions):
        self.options = options

    def two_view_geometry_options(self):
        two_view_geometry_options = TwoViewGeometryOptions()
        two_view_geometry_options.compute_relative_pose = True
        if self.options.random_seed is not None:
            two_view_geometry_options.ransac.random_seed = self.options.random_seed
        if self.options.ransac_max_error is not None:
            two_view_geometry_options.ransac.max_error = self.options.ransac_max_error
        if self.options.ransac_min_inlier_ratio is not None:
            two_view_geometry_options.ransac.min_inlier_ratio = self.options.ransac_min_inlier_ratio
        two_view_geometry_options.filter_stationary_matches = True
        two_view_geometry_options.stationary_matches_max_error = 4.0
        return two_view_geometry_options

    def feature_matching_options(self):
        feature_matching_options = FeatureMatchingOptions()
        if self.options.rig_verification is not None:
            feature_matching_options.rig_verification = self.options.rig_verification
        feature_matching_options.skip_image_pairs_in_same_frame = False
        return feature_matching_options

    def incremental_pipeline_options(self):
        incremental_pipeline_options = IncrementalPipelineOptions()
        # incremental_pipeline_options.num_threads = 1
        # incremental_pipeline_options.mapper.num_threads = 1

        if self.options.random_seed is not None:
            incremental_pipeline_options.random_seed = self.options.random_seed
            incremental_pipeline_options.triangulation.random_seed = self.options.random_seed

        if self.options.use_prior_position is not None:
            incremental_pipeline_options.use_prior_position = self.options.use_prior_position
        if self.options.bundle_adjustment_refine_sensor_from_rig is not None:
            incremental_pipeline_options.ba_refine_sensor_from_rig = (
                self.options.bundle_adjustment_refine_sensor_from_rig
            )
        if self.options.bundle_adjustment_refine_focal_length is not None:
            incremental_pipeline_options.ba_refine_focal_length = self.options.bundle_adjustment_refine_focal_length
        if self.options.bundle_adjustment_refine_principal_point is not None:
            incremental_pipeline_options.ba_refine_principal_point = (
                self.options.bundle_adjustment_refine_principal_point
            )

        if self.options.triangulation_minimum_angle is not None:
            incremental_pipeline_options.mapper.filter_min_tri_angle = self.options.triangulation_minimum_angle
            incremental_pipeline_options.triangulation.min_angle = self.options.triangulation_minimum_angle
        if self.options.mapper_filter_max_reprojection_error is not None:
            incremental_pipeline_options.mapper.filter_max_reproj_error = (
                self.options.mapper_filter_max_reprojection_error
            )
        if self.options.triangulation_complete_max_reprojection_error is not None:
            incremental_pipeline_options.triangulation.complete_max_reproj_error = (
                self.options.triangulation_complete_max_reprojection_error
            )
        if self.options.triangulation_merge_max_reprojection_error is not None:
            incremental_pipeline_options.triangulation.merge_max_reproj_error = (
                self.options.triangulation_merge_max_reprojection_error
            )
        return incremental_pipeline_options


class ReconstructionMetricsBuilder:
    def __init__(self, db_path: Path):
        self.db_path = db_path
        self.metrics = ReconstructionMetrics()

    def build_verified_matches_metrics(self, canonical_pairs: list[tuple[str, ...]]) -> None:
        database = Database.open(str(self.db_path))
        # Map image name -> image_id (help Pyright with explicit types)
        all_images: list[pycolmapImage] = database.read_all_images()
        name_to_id: dict[str, int] = {img.name: img.image_id for img in all_images}

        def _parse_name(name: str) -> tuple[Optional[str], Optional[str], Optional[str]]:
            # Expected: "<rig_id>/<camera_id>/<frame_id>.jpg"
            try:
                rig_id, cam_id, rest = name.split("/", 2)
                frame_id = rest.rsplit(".", 1)[0]
                return rig_id, cam_id, frame_id
            except Exception:
                return None, None, None

        total: int = 0
        verified: int = 0
        inliers_all: List[int] = []

        st_total = st_verified = 0  # stereo: same frame, different sensors
        st_inliers: List[int] = []

        ss_total = ss_verified = 0  # same sensor across frames
        ss_inliers: List[int] = []

        cs_total = cs_verified = 0  # cross sensor across frames
        cs_inliers: List[int] = []

        for a, b in canonical_pairs:
            total += 1
            ida: int = name_to_id[a]
            idb: int = name_to_id[b]
            tvg: Any = database.read_two_view_geometry(ida, idb)

            ninl: int = len(getattr(tvg, "inlier_matches", []))
            ok: bool = ninl > 0 and int(getattr(tvg, "config", 0)) != 0

            if ok:
                verified += 1
                inliers_all.append(ninl)

            ra, ca, fa = _parse_name(a)
            rb, cb, fb = _parse_name(b)
            if None not in (ra, ca, fa, rb, cb, fb) and ra == rb:
                if fa == fb and ca != cb:
                    st_total += 1
                    if ok:
                        st_verified += 1
                        st_inliers.append(ninl)
                elif ca == cb and fa != fb:
                    ss_total += 1
                    if ok:
                        ss_verified += 1
                        ss_inliers.append(ninl)
                elif ca != cb and fa != fb:
                    cs_total += 1
                    if ok:
                        cs_verified += 1
                        cs_inliers.append(ninl)

        self.metrics.all_verified_matches = verified
        self.metrics.all_verified_match_rate = (100.0 * verified / total) if total else 0.0
        self.metrics.all_verified_match_inliers_mean = mean(inliers_all) if inliers_all else 0.0
        self.metrics.all_verified_match_inliers_median = median(inliers_all) if inliers_all else 0.0
        self.metrics.stereo_verified_matches = st_verified
        self.metrics.stereo_verified_match_rate = (100.0 * st_verified / st_total) if st_total else 0.0
        self.metrics.stereo_verified_match_inliers_mean = mean(st_inliers) if st_inliers else 0.0
        self.metrics.stereo_verified_match_inliers_median = median(st_inliers) if st_inliers else 0.0
        self.metrics.same_sensor_verified_matches = ss_verified
        self.metrics.same_sensor_verified_match_rate = (100.0 * ss_verified / ss_total) if ss_total else 0.0
        self.metrics.same_sensor_verified_match_inliers_mean = mean(ss_inliers) if ss_inliers else 0.0
        self.metrics.same_sensor_verified_match_inliers_median = median(ss_inliers) if ss_inliers else 0.0
        self.metrics.cross_sensor_verified_matches = cs_verified
        self.metrics.cross_sensor_verified_match_rate = (100.0 * cs_verified / cs_total) if cs_total else 0.0
        self.metrics.cross_sensor_verified_match_inliers_mean = mean(cs_inliers) if cs_inliers else 0.0
        self.metrics.cross_sensor_verified_match_inliers_median = median(cs_inliers) if cs_inliers else 0.0

        database.close()
