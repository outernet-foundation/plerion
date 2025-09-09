from typing import List, Optional
from uuid import UUID

from fastapi import APIRouter, HTTPException, Query, status
from pydantic import ConfigDict

from ..db.piccolo_shims import create_pydantic_model
from ..db.tables.layers import Layer


class LayerModel(create_pydantic_model(Layer)):
    model_config = ConfigDict(from_attributes=True)


# ----------- Router -----------

router = APIRouter(prefix="/layers", tags=["layers"])


# CREATE
@router.post("")
async def create_layer(layer: LayerModel):
    exists = await Layer.exists().where(Layer.id == layer.id)  # type: ignore

    if exists:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=f"Layer with id {id} already exists",
        )

    row = await Layer.objects().create(**layer.model_dump())

    return LayerModel.model_validate(row)


# READ ALL
@router.get("")
async def get_layers(
    ids: Optional[List[UUID]] = Query(
        None, description="Optional list of Ids to filter by"
    ),
) -> List[LayerModel]:
    if ids:
        rows = await Layer.objects().where(Layer.id.is_in(ids))
    else:
        rows = await Layer.objects()
    return [LayerModel.model_validate(r) for r in rows]


# READ ONE
@router.get("/{id}")
async def get_layer(id: UUID):
    layer = await Layer.objects().where(Layer.id == id).first()
    if not layer:
        raise HTTPException(status_code=404, detail="Layer not found")
    return layer


# UPDATE
@router.put("/{id}")
async def update_layer(id: UUID, layer: LayerModel):
    exists = await Layer.exists().where(Layer.id == id)
    if not exists:
        raise HTTPException(status_code=404, detail="Layer not found")

    await Layer.update(layer.model_dump()).where(Layer.id == id)  # type: ignore
    return await Layer.objects().where(Layer.id == id).first()


# DELETE
@router.delete("/{id}")
async def delete_layer(id: UUID):
    deleted_count = await Layer.delete().where(Layer.id == id)  # type: ignore
    if deleted_count == 0:
        raise HTTPException(status_code=404, detail="Layer not found")
    return {"ok": True, "deleted": str(id)}
