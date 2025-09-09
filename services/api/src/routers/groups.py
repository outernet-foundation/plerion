from typing import List, Optional
from uuid import UUID

from fastapi import APIRouter, HTTPException, Query, status
from pydantic import ConfigDict

from ..db.piccolo_shims import create_pydantic_model
from ..db.tables.groups import Group


class GroupModel(create_pydantic_model(Group)):
    model_config = ConfigDict(from_attributes=True)


# ----------- Router -----------

router = APIRouter(prefix="/groups", tags=["groups"])


# CREATE
@router.post("")
async def create_group(group: GroupModel):
    exists = await Group.exists().where(Group.id == group.id)  # type: ignore

    if exists:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=f"Group with id {id} already exists",
        )

    row = await Group.objects().create(**group.model_dump())

    return GroupModel.model_validate(row)


# READ ALL
@router.get("")
async def get_groups(
    ids: Optional[List[UUID]] = Query(
        None, description="Optional list of Ids to filter by"
    ),
) -> List[GroupModel]:
    if ids:
        rows = await Group.objects().where(Group.id.is_in(ids))
    else:
        rows = await Group.objects()
    return [GroupModel.model_validate(r) for r in rows]


# READ ONE
@router.get("/{id}")
async def get_group(id: UUID):
    group = await Group.objects().where(Group.id == id).first()
    if not group:
        raise HTTPException(status_code=404, detail="Group not found")
    return group


# UPDATE
@router.put("/{id}")
async def update_group(id: UUID, group: GroupModel):
    exists = await Group.exists().where(Group.id == id)
    if not exists:
        raise HTTPException(status_code=404, detail="Group not found")

    await Group.update(group.model_dump()).where(Group.id == id)  # type: ignore
    return await Group.objects().where(Group.id == id).first()


# DELETE
@router.delete("/{id}")
async def delete_group(id: UUID):
    deleted_count = await Group.delete().where(Group.id == id)  # type: ignore
    if deleted_count == 0:
        raise HTTPException(status_code=404, detail="Group not found")
    return {"ok": True, "deleted": str(id)}
