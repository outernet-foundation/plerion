from __future__ import annotations

from io import BytesIO
from struct import pack
from typing import Optional
from uuid import UUID

from common.boto_clients import create_s3_client
from common.schemas import binary_schema
from core.axis_convention import AxisConvention, change_basis_unity_from_opencv_points
from core.classes import Quaternion, Transform, Vector3
from core.reconstruction_manifest import ReconstructionManifest, ReconstructionStatus
from core.reconstruction_metrics import ReconstructionMetrics
from core.reconstruction_options import ReconstructionOptions
from datamodels.public_dtos import (
    ReconstructionCreate,
    ReconstructionRead,
    reconstruction_from_dto,
    reconstruction_to_dto,
)
from datamodels.public_tables import CaptureSession, LocalizationMap, Reconstruction
from fastapi import APIRouter, Depends, HTTPException, Query, status
from fastapi.responses import StreamingResponse
from numpy import array, ascontiguousarray, float32, load, uint8
from pydantic import BaseModel, Field
from scipy.spatial.transform import Rotation
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from ..database import get_session
from ..settings import get_settings

settings = get_settings()
BUCKET = "dev-reconstructions"

router = APIRouter(prefix="/reconstructions", tags=["reconstructions"])

s3_client = create_s3_client(
    s3_endpoint_url=settings.s3_endpoint_url, s3_access_key=settings.s3_access_key, s3_secret_key=settings.s3_secret_key
)


class ReconstructionCreateWithOptions(BaseModel):
    create: ReconstructionCreate
    options: Optional[ReconstructionOptions] = Field(
        default=None, description="Optional reconstruction options to use during reconstruction."
    )


@router.post("")
async def create_reconstruction(
    reconstruction: ReconstructionCreateWithOptions, session: AsyncSession = Depends(get_session)
) -> ReconstructionRead:
    # If we were provided an ID, ensure it doesn't already exist
    if reconstruction.create.id is not None:
        result = await session.execute(select(Reconstruction).where(Reconstruction.id == reconstruction.create.id))
        existing_row = result.scalar_one_or_none()

        if existing_row is not None:
            raise HTTPException(409, f"Reconstruction with id {reconstruction.create.id} already exists")

    capture_session = await session.get(CaptureSession, reconstruction.create.capture_session_id)
    if not capture_session:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Capture session with id {reconstruction.create.capture_session_id} not found",
        )

    row = reconstruction_from_dto(reconstruction.create)

    session.add(row)

    await session.flush()
    await session.refresh(row)

    manifest = ReconstructionManifest(
        capture_id=str(row.capture_session_id),
        status="pending",
        options=reconstruction.options or ReconstructionOptions(),
        metrics=ReconstructionMetrics(),
    )

    s3_client.put_object(
        Bucket=settings.reconstructions_bucket,
        Key=f"{row.id}/manifest.json",
        Body=manifest.model_dump_json().encode("utf-8"),
        ContentType="application/json",
    )

    return reconstruction_to_dto(row)


@router.delete("/{id}")
async def delete_reconstruction(id: UUID, session: AsyncSession = Depends(get_session)) -> None:
    row = await session.get(Reconstruction, id)

    if not row:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=f"Reconstruction with id {id} not found")

    result = await session.execute(select(LocalizationMap).where(LocalizationMap.reconstruction_id == id))
    localization_map = result.scalar_one_or_none()

    if localization_map:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Reconstruction with id {id} has an associated localization map and cannot be deleted",
        )

    await session.delete(row)

    await session.flush()
    return None


@router.get("")
async def get_reconstructions(
    ids: Optional[list[UUID]] = Query(None, description="Optional list of Ids to filter by"),
    capture_session_id: Optional[UUID] = Query(None, description="Optional capture session Id to filter by"),
    capture_session_name: Optional[str] = Query(None, description="Optional capture session name to filter by"),
    session: AsyncSession = Depends(get_session),
) -> list[ReconstructionRead]:
    if capture_session_name and capture_session_id:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Cannot provide both capture_session_id and capture_session_name",
        )

    query = select(Reconstruction)

    if ids:
        query = query.where(Reconstruction.id.in_(ids))

    if capture_session_id:
        query = query.where(Reconstruction.capture_session_id == capture_session_id)

    if capture_session_name:
        result = await session.execute(select(CaptureSession.id).where(CaptureSession.name == capture_session_name))
        capture_session_row = result.scalar_one_or_none()
        if not capture_session_row:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail=f"Capture session with name {capture_session_name} not found",
            )
        query = query.where(Reconstruction.capture_session_id == capture_session_row)

    result = await session.execute(query)

    return [reconstruction_to_dto(row) for row in result.scalars().all()]


@router.get("/{id}")
async def get_reconstruction(id: UUID, session: AsyncSession = Depends(get_session)) -> ReconstructionRead:
    row = await session.get(Reconstruction, id)

    if not row:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=f"Reconstruction with id {id} not found")

    return reconstruction_to_dto(row)


@router.get("/{id}/manifest")
async def get_reconstruction_manifest(id: UUID, session: AsyncSession = Depends(get_session)) -> ReconstructionManifest:
    row = await session.get(Reconstruction, id)

    if not row:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=f"Reconstruction with id {id} not found")

    try:
        manifest = ReconstructionManifest.model_validate_json(
            s3_client.get_object(Bucket=settings.reconstructions_bucket, Key=f"{id}/manifest.json")["Body"].read()
        )
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Error retrieving manifest for reconstruction with id {id}: {e}",
        )

    return manifest


@router.get("/{id}/localization_map")
async def get_reconstruction_localization_map(id: UUID, session: AsyncSession = Depends(get_session)) -> UUID:
    row = await session.get(Reconstruction, id)

    if not row:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=f"Reconstruction with id {id} not found")

    result = await session.execute(select(LocalizationMap.id).where(LocalizationMap.reconstruction_id == id))

    localization_map = result.scalar_one_or_none()

    if localization_map is None:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND, detail=f"Localization map for reconstruction with id {id} not found"
        )

    return localization_map


@router.get("/{id}/status")
async def get_reconstruction_status(id: UUID, session: AsyncSession = Depends(get_session)) -> ReconstructionStatus:
    manifest = await get_reconstruction_manifest(id, session)

    return manifest.status


@router.get("/{id}/points", response_class=StreamingResponse, responses={200: {"content": binary_schema}})
async def get_reconstruction_points(
    id: UUID,
    axis_convention: AxisConvention = Query(AxisConvention.OPENCV),
    session: AsyncSession = Depends(get_session),
) -> StreamingResponse:
    row = await session.get(Reconstruction, id)
    if not row:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=f"Reconstruction with id {id} not found")

    # Load point cloud from S3
    npz_bytes = (
        create_s3_client(settings.s3_endpoint_url, settings.s3_access_key, settings.s3_secret_key)
        .get_object(Bucket=settings.reconstructions_bucket, Key=f"{row.id}/sfm_model/points3D.npz")["Body"]
        .read()
    )

    # Extract positions and colors
    with load(BytesIO(npz_bytes)) as npz:
        point_cloud_positions = npz["positions"]
        point_cloud_colors = npz["colors"]

    # Change basis if needed
    if axis_convention == AxisConvention.UNITY:
        point_cloud_positions = change_basis_unity_from_opencv_points(point_cloud_positions)

    # Serialize and return point cloud as binary stream
    return StreamingResponse(
        BytesIO(
            (
                pack("<I", int(point_cloud_positions.shape[0]))
                + ascontiguousarray(point_cloud_positions, dtype=float32).astype("<f4", copy=False).tobytes()
                + ascontiguousarray(point_cloud_colors, dtype=uint8).tobytes()
            )
        ),
        media_type="application/octet-stream",
    )


@router.get("/{id}/frame_poses")
async def get_reconstruction_frame_poses(id: UUID, session: AsyncSession = Depends(get_session)) -> list[Transform]:
    row = await session.get(Reconstruction, id)
    if not row:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=f"Reconstruction with id {id} not found")

    poses: list[Transform] = []
    for parts in [
        line.split(
            maxsplit=9  # COLMAP images.txt: ID QW QX QY QZ TX TY TZ CAM_ID NAME; NAME may have spaces, so keep it as one field
        )
        for _, line in enumerate([
            line
            for line in [
                line.decode("utf-8").strip()
                for line in create_s3_client(settings.s3_endpoint_url, settings.s3_access_key, settings.s3_secret_key)
                .get_object(Bucket=settings.reconstructions_bucket, Key=f"{row.id}/sfm_model/frames.txt")["Body"]
                .iter_lines()
            ]
            if line and not line.startswith("#")
        ])
    ]:
        qw, qx, qy, qz = map(float, parts[2:6])
        tx, ty, tz = map(float, parts[6:9])

        # Convert from camera-to-world to world-to-camera
        world_from_frame_rotation_matrix = Rotation.from_quat([qx, qy, qz, qw]).as_matrix().T
        world_from_frame_translation = -world_from_frame_rotation_matrix @ array([tx, ty, tz], dtype=float)
        world_from_frame_rotation_quaternion = Rotation.from_matrix(world_from_frame_rotation_matrix).as_quat()

        poses.append(
            Transform(
                position=Vector3(
                    x=world_from_frame_translation[0],
                    y=world_from_frame_translation[1],
                    z=world_from_frame_translation[2],
                ),
                rotation=Quaternion(
                    x=world_from_frame_rotation_quaternion[0],
                    y=world_from_frame_rotation_quaternion[1],
                    z=world_from_frame_rotation_quaternion[2],
                    w=world_from_frame_rotation_quaternion[3],
                ),
            )
        )

    return poses
