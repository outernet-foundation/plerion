from typing import List, Optional, cast
from uuid import UUID, uuid4

from fastapi import APIRouter, HTTPException, Query, status
from pydantic import BaseModel, ConfigDict

from ..db.piccolo_shims import create_pydantic_model
from ..db.tables.localizationMaps import LocalizationMap


class LocalizationMapModel(create_pydantic_model(LocalizationMap)):
    model_config = ConfigDict(from_attributes=True)


# ----------- Router -----------

router = APIRouter(prefix="/localizationMaps")


# CREATE
@router.post("")
async def create_localizationMap(localizationMap: LocalizationMapModel):
    exists = await LocalizationMap.exists().where(
        LocalizationMap.id == localizationMap.id  # type: ignore
    )

    if exists:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY, detail=f"LocalizationMap with id {id} already exists"
        )

    row = await LocalizationMap.objects().create(**localizationMap.model_dump(exclude_none=True))

    return LocalizationMapModel.model_validate(row)


# READ ALL
@router.get("")
async def get_localizationMaps(
    ids: Optional[List[UUID]] = Query(None, description="Optional list of Ids to filter by"),
) -> List[LocalizationMapModel]:
    if ids:
        rows = await LocalizationMap.objects().where(LocalizationMap.id.is_in(ids))
    else:
        rows = await LocalizationMap.objects()
    return [LocalizationMapModel.model_validate(r) for r in rows]


@router.get("/name/{name}")
async def get_localization_map_by_name(name: str) -> LocalizationMapModel:
    row = await LocalizationMap.objects().get(LocalizationMap.name == name)
    if not row:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND, detail=f"LocalizationMap with name '{name}' not found"
        )
    return LocalizationMapModel.model_validate(row)


# UPDATE
@router.put("/{id:uuid}")
async def update_localizationMap(id: UUID, localizationMap: LocalizationMapModel):
    exists = await LocalizationMap.exists().where(LocalizationMap.id == id)
    if not exists:
        raise HTTPException(status_code=404, detail="LocalizationMap not found")

    await LocalizationMap.update(localizationMap.model_dump()).where(  # type: ignore
        LocalizationMap.id == id
    )
    return await LocalizationMap.objects().where(LocalizationMap.id == id).first()


@router.put("/upsert")
async def upsert_localizationMaps(localizationMaps: List[LocalizationMapModel]):
    """
    Bulk upsert localizationMaps.
    - If a localizationMap has an id and exists → update it.
    - If a localizationMap has an id but doesn't exist → insert it.
    - If a localizationMap has no id → generate a new UUID and insert it.
    """

    for localizationMap in localizationMaps:
        data = localizationMap.model_dump(exclude_none=True)

        # Generate an id if missing
        localizationMap_id = data.get("id")
        if not localizationMap_id:
            localizationMap_id = uuid4()
            data["id"] = localizationMap_id

        # Update or insert
        exists = await LocalizationMap.exists().where(LocalizationMap.id == localizationMap_id)  # type: ignore

        if exists:
            await LocalizationMap.update(data).where(  # type: ignore
                LocalizationMap.id == localizationMap_id
            )
        else:
            await LocalizationMap.objects().create(**data)

        # Fetch the final row
        row = cast(  # type: ignore
            dict,  # type: ignore
            await LocalizationMap.objects().where(LocalizationMap.id == localizationMap_id).first(),
        )
        if not row:
            raise HTTPException(
                status_code=500, detail=f"Upsert failed unexpectedly for localizationMap id {localizationMap_id}"
            )

    return localizationMaps


# DELETE
@router.delete("/", response_model=List[UUID])
async def delete_localizationMaps(ids: List[UUID]):
    if len(ids) == 0:
        return ids

    # First fetch which ids actually exist
    existing = await LocalizationMap.select(LocalizationMap.id).where(  # type: ignore
        LocalizationMap.id.is_in(ids)
    )
    existing_ids = [str(row["id"]) for row in existing]

    if not existing_ids:
        raise HTTPException(status_code=404, detail="No matching Localization Maps found")

    # Delete only those existing ids
    await LocalizationMap.delete().where(LocalizationMap.id.is_in(existing_ids))

    return existing_ids


@router.get("/points/{id:uuid}")
async def get_map_points(id: UUID) -> List[float]:
    return []


@router.put("/updateMapsLocation")
async def update_maps_from_location(latitude: float, longitude: float, height: float, radius: float, lighting: int):
    return


@router.put("/updateMapsList")
async def update_maps_from_list(maps: List[UUID]):
    return


@router.get("/loadedMaps")
async def get_loaded_maps() -> List[LocalizationMapModel]:
    return []


class LocalizationRequest(BaseModel):
    imageWidth: int
    imageHeight: int
    cameraIntrinsics: List[float]
    cameraRotation: List[float]
    cameraOrientation: List[float]
    pixelBuffer: bytes


class LocalizationResponse(BaseModel):
    mapID: UUID
    mapEcefPosition: List[float]
    mapEcefRotation: List[float]
    estimatedCameraPosition: List[float]
    estimatedCameraRotation: List[float]
    confidence: int


@router.get("/localize", response_model=LocalizationResponse)
async def localize(data: LocalizationRequest) -> LocalizationResponse:
    return LocalizationResponse(
        mapID=uuid4(),
        mapEcefPosition=[],
        mapEcefRotation=[],
        estimatedCameraPosition=[],
        estimatedCameraRotation=[],
        confidence=85,
    )
