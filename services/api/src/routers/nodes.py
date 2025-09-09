from typing import List, Optional
from uuid import UUID

from fastapi import APIRouter, HTTPException, Query, status
from pydantic import ConfigDict

from ..db.piccolo_shims import create_pydantic_model
from ..db.tables.nodes import Node


class NodeModel(create_pydantic_model(Node)):
    model_config = ConfigDict(from_attributes=True)


# ----------- Router -----------

router = APIRouter(prefix="/nodes", tags=["nodes"])


# CREATE
@router.post("")
async def create_node(node: NodeModel):
    exists = await Node.exists().where(Node.id == node.id)  # type: ignore

    if exists:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=f"Node with id {id} already exists",
        )

    row = await Node.objects().create(**node.model_dump())

    return NodeModel.model_validate(row)


# READ ALL
@router.get("")
async def get_nodes(
    ids: Optional[List[UUID]] = Query(
        None, description="Optional list of Ids to filter by"
    ),
) -> List[NodeModel]:
    if ids:
        rows = await Node.objects().where(Node.id.is_in(ids))
    else:
        rows = await Node.objects()
    return [NodeModel.model_validate(r) for r in rows]


# READ ONE
@router.get("/{id}")
async def get_node(id: UUID):
    node = await Node.objects().where(Node.id == id).first()
    if not node:
        raise HTTPException(status_code=404, detail="Node not found")
    return node


# UPDATE
@router.put("/{id}")
async def update_node(id: UUID, node: NodeModel):
    exists = await Node.exists().where(Node.id == id)
    if not exists:
        raise HTTPException(status_code=404, detail="Node not found")

    await Node.update(node.model_dump()).where(Node.id == id)  # type: ignore
    return await Node.objects().where(Node.id == id).first()


# DELETE
@router.delete("/{id}")
async def delete_node(id: UUID):
    deleted_count = await Node.delete().where(Node.id == id)  # type: ignore
    if deleted_count == 0:
        raise HTTPException(status_code=404, detail="Node not found")
    return {"ok": True, "deleted": str(id)}
