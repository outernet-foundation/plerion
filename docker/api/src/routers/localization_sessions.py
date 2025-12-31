from uuid import UUID, uuid4

from common.docker_compose_client import create_service, destroy_service, get_service_status
from core.axis_convention import AxisConvention
from core.camera_config import CameraConfig
from core.localization_metrics import LocalizationMetrics
from core.transform import Float3, Float4, Transform
from datamodels.public_dtos import LocalizationSessionRead, localization_session_to_dto
from datamodels.public_tables import LocalizationSession
from fastapi import APIRouter, Depends, File, HTTPException, UploadFile, status
from plerion_localizer_client import ApiClient, Configuration
from plerion_localizer_client.api.default_api import DefaultApi
from plerion_localizer_client.models.axis_convention import AxisConvention as LocalizerAxisConvention
from plerion_localizer_client.models.camera import Camera as LocalizerCamera
from plerion_localizer_client.models.transform import Transform as LocalizerTransform
from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from ..database import get_session
from ..settings import get_settings
from .localization_maps import get_localization_maps

router = APIRouter(prefix="/localization_sessions", tags=["localization_sessions"])

settings = get_settings()

SESSION_PORT = 8000  # internal port exposed by the session container


@router.post("")
async def create_localization_session(session: AsyncSession = Depends(get_session)) -> LocalizationSessionRead:
    row = LocalizationSession(id=uuid4())
    container_name = f"localizer-{row.id}"
    row.container_id = create_service(
        settings.localizer_service, container_name, environment={"SESSION_ID": str(row.id)}
    )
    row.container_url = str(f"http://{container_name}:{SESSION_PORT}")

    session.add(row)

    await session.flush()
    await session.refresh(row)

    return localization_session_to_dto(row)


@router.delete("/{localization_session_id}")
async def delete_localization_session(localization_session_id: UUID, session: AsyncSession = Depends(get_session)):
    row = await session.get(LocalizationSession, localization_session_id)

    if row:
        destroy_service(row.container_id)
        await session.delete(row)
        await session.commit()
        return {"detail": f"Localization session with id {localization_session_id} deleted"}
    else:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Localization session with id {localization_session_id} not found",
        )


@router.get("/{localization_session_id}/status")
async def get_localization_session_status(
    localization_session_id: UUID, session: AsyncSession = Depends(get_session)
) -> str:
    return get_service_status(f"localizer-{localization_session_id}", SESSION_PORT)


class MapLocalization(BaseModel):
    id: UUID
    camera_from_map_transform: Transform
    map_transform: Transform
    metrics: LocalizationMetrics


@router.post("/{localization_session_id}/localization")
async def localize_image(
    localization_session_id: UUID,
    map_ids: list[UUID],
    prior: Transform,
    camera: CameraConfig,
    axis_convention: AxisConvention,
    image: UploadFile = File(...),
    session: AsyncSession = Depends(get_session),
) -> list[MapLocalization]:
    url = await _session_base_url(session, localization_session_id)
    async with ApiClient(Configuration(host=url)) as api_client:
        try:
            localizations = await DefaultApi(api_client).localize_image(
                prior=LocalizerTransform.model_validate(prior.model_dump()),
                map_ids=map_ids,
                camera=LocalizerCamera.model_validate(camera.model_dump()),
                axis_convention=LocalizerAxisConvention(axis_convention.value),
                image=await image.read(),
            )
            reconstruction_id_to_map = {
                map.reconstruction_id: map
                for map in await get_localization_maps(
                    reconstruction_ids=[response.id for response in localizations], ids=None, session=session
                )
            }

            return [
                MapLocalization(
                    id=reconstruction_id_to_map[localization.id].id,
                    camera_from_map_transform=Transform.model_validate(localization.transform.model_dump()),
                    map_transform=Transform(
                        translation=Float3(
                            x=reconstruction_id_to_map[localization.id].position_x,
                            y=reconstruction_id_to_map[localization.id].position_y,
                            z=reconstruction_id_to_map[localization.id].position_z,
                        ),
                        rotation=Float4(
                            x=reconstruction_id_to_map[localization.id].rotation_x,
                            y=reconstruction_id_to_map[localization.id].rotation_y,
                            z=reconstruction_id_to_map[localization.id].rotation_z,
                            w=reconstruction_id_to_map[localization.id].rotation_w,
                        ),
                    ),
                    metrics=LocalizationMetrics.model_validate(localization.metrics.model_dump()),
                )
                for localization in localizations
            ]

        except Exception as e:
            raise HTTPException(502, f"session backend unreachable: {e}") from e


async def _session_base_url(db: AsyncSession, session_id: UUID) -> str:
    row = (
        await db.execute(select(LocalizationSession.container_url).where(LocalizationSession.id == session_id))
    ).scalar_one_or_none()
    if not row:
        raise HTTPException(status_code=404, detail="Localization session not found")
    return row.rstrip("/")  # e.g., "http://localizer-<uuid>:8080"
