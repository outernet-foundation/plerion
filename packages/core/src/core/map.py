from pathlib import Path
from tempfile import TemporaryDirectory
from typing import TYPE_CHECKING, Any, Dict, Mapping, cast

from faiss import (  # type: ignore
    OPQMatrix,
    ProductQuantizer,
    read_ProductQuantizer,  # type: ignore
    read_VectorTransform,  # type: ignore
    write_ProductQuantizer,  # type: ignore
    write_VectorTransform,  # type: ignore
)
from h5py import Dataset, File, Group
from numpy import asarray, float32, stack, uint8
from numpy.typing import NDArray
from pycolmap import Reconstruction
from torch import Tensor, from_numpy  # type: ignore

from .metrics import Metrics
from .options import Options
from .reconstruction_manifest import ReconstructionManifest
from .utility import to_f32

if TYPE_CHECKING:
    from mypy_boto3_s3 import S3Client
else:
    S3Client = Any


GLOBAL_DESCRIPTORS_DATASET_NAME = "global_descriptor"
KEYPOINTS_DATASET_NAME = "keypoints"
PQ_CODES_DATASET_NAME = "pq_codes"

GLOBAL_DESCRIPTORS_FILE = "global_descriptors.h5"
FEATURES_FILE = "features.h5"
PQ_QUANTIZER_FILE = "pq_quantizer.pq"
OPQ_MATRIX_FILE = "opq_matrix.tf"
PAIRS_FILE = "pairs.txt"
COLMAP_DATABASE_FILE = "database.db"


class MapWriter:
    def __init__(
        self,
        root_path: Path,
        manifest: ReconstructionManifest,
        s3_client: S3Client,
        colmap_db_path: Path,
        reconstruction_bucket: str,
        reconstruction_id: str,
    ):
        self.root_path = root_path
        self.root_path.mkdir(parents=True, exist_ok=True)
        self.manifest = manifest
        self.s3_client = s3_client
        self.reconstruction_bucket = reconstruction_bucket
        self.reconstruction_id = reconstruction_id
        self.options = Options(manifest.options)
        self.metrics = Metrics(colmap_db_path)

    def write_pairs(self, pairs: list[tuple[str, ...]]):
        (self.root_path / PAIRS_FILE).write_text("\n".join([" ".join(pair) for pair in pairs]))
        self._put_reconstruction_object(key=PAIRS_FILE, body=(self.root_path / PAIRS_FILE).read_bytes())

    def write_opq(self, opq_matrix: OPQMatrix, pq_quantizer: ProductQuantizer):
        write_VectorTransform(opq_matrix, str(self.root_path / OPQ_MATRIX_FILE))
        write_ProductQuantizer(pq_quantizer, str(self.root_path / PQ_QUANTIZER_FILE))
        self._put_reconstruction_object(key=OPQ_MATRIX_FILE, body=(self.root_path / OPQ_MATRIX_FILE).read_bytes())
        self._put_reconstruction_object(key=PQ_QUANTIZER_FILE, body=(self.root_path / PQ_QUANTIZER_FILE).read_bytes())

    def write_h5_features(
        self,
        global_descriptors: Mapping[str, Tensor],
        keypoints: Mapping[str, NDArray[float32]],
        pq_codes: Mapping[str, NDArray[uint8]],
    ):
        with File(str(self.root_path / GLOBAL_DESCRIPTORS_FILE), "w") as file:
            for name, global_descriptor in global_descriptors.items():
                group = file.create_group(name)
                self._create_dataset(group, GLOBAL_DESCRIPTORS_DATASET_NAME, to_f32(global_descriptor))

        with File(str(self.root_path / FEATURES_FILE), "w") as file:
            for name, image_keypoints in keypoints.items():
                group = file.create_group(name)
                self._create_dataset(group, KEYPOINTS_DATASET_NAME, image_keypoints)
                self._create_dataset(group, PQ_CODES_DATASET_NAME, pq_codes[name])

        self._put_reconstruction_object(
            key=GLOBAL_DESCRIPTORS_FILE, body=(self.root_path / GLOBAL_DESCRIPTORS_FILE).read_bytes()
        )
        self._put_reconstruction_object(key=FEATURES_FILE, body=(self.root_path / FEATURES_FILE).read_bytes())

    def write_reconstruction(self, reconstruction: Reconstruction):
        with TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            reconstruction.write_text(str(temp_path))
            reconstruction.export_PLY(str(temp_path / "points3D.ply"))

            for file_path in temp_path.rglob("*"):
                if file_path.is_file():
                    self._put_reconstruction_object(
                        key=f"sfm_model/{file_path.relative_to(temp_path)}", body=file_path.read_bytes()
                    )

        self.manifest.status = "succeeded"
        self._put_reconstruction_object(key="manifest.json", body=self.manifest.model_dump_json().encode("utf-8"))

    def _create_dataset(self, group: Group, name: str, data: Any):
        group.create_dataset(name, data=data, compression="gzip", compression_opts=9, shuffle=True, chunks=True)

    def _put_reconstruction_object(self, key: str, body: bytes):
        print(f"Putting object in bucket {self.reconstruction_bucket} with key {self.reconstruction_id}/{key}")
        self.s3_client.put_object(Bucket=self.reconstruction_bucket, Key=f"{self.reconstruction_id}/{key}", Body=body)


def read_opq(root_path: Path) -> tuple[OPQMatrix, ProductQuantizer]:
    return (
        cast(OPQMatrix, read_VectorTransform(str(root_path / OPQ_MATRIX_FILE))),
        cast(ProductQuantizer, read_ProductQuantizer(str(root_path / PQ_QUANTIZER_FILE))),
    )


def read_h5_features_for_reconstruction(reconstruction: Reconstruction, root_path: Path, device: str):
    images_mapping = cast(Mapping[int, Any], reconstruction.images)

    # Canonical order: sort by image id for determinism
    image_items = sorted(images_mapping.items(), key=lambda item: int(item[0]))
    image_ids_in_order = [int(image_id) for image_id, _ in image_items]
    image_names = [str(img.name) for _, img in image_items]

    # --- global descriptors ---
    with File(str(root_path / GLOBAL_DESCRIPTORS_FILE), "r") as file:
        global_rows = [
            asarray(cast(Group | File, cast(Group, file[name])[GLOBAL_DESCRIPTORS_DATASET_NAME])[()], dtype=float32)
            for name in image_names
        ]

    global_matrix = from_numpy(stack(global_rows, axis=0)).to(device).float()

    keypoints: Dict[int, Tensor] = {}
    pq_codes: Dict[int, NDArray[uint8]] = {}

    with File(str(root_path / FEATURES_FILE), "r") as file:
        for image_id, name in zip(image_ids_in_order, image_names):
            group = cast(Group, file[name])
            keypoints[image_id] = from_numpy(
                asarray(cast(Dataset, group[KEYPOINTS_DATASET_NAME])[()], dtype=float32)
            ).to(device)
            pq_codes[image_id] = asarray(cast(Dataset, group[PQ_CODES_DATASET_NAME])[()], dtype=uint8)

    return image_names, image_ids_in_order, global_matrix, keypoints, pq_codes
