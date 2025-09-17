import uuid
from datetime import datetime

from piccolo.columns import (
    UUID,
    Boolean,
    Float,
    Integer,
    Timestamptz,
    Varchar,
)
from piccolo.columns.defaults.timestamptz import TimestamptzNow
from pydantic import BaseModel, ConfigDict

from ..piccolo_shims import Table


# require=true
# https://github.com/piccolo-orm/piccolo/issues/1132
class Node(Table):
    id = UUID(primary_key=True)

    name = Varchar(length=255, unique=False)

    created_at = Timestamptz(default=TimestamptzNow())

    active = Boolean(True, null=False)

    positionX = Float(null=False)

    positionY = Float(null=False)

    positionZ = Float(null=False)

    rotationX = Float(null=False)

    rotationY = Float(null=False)

    rotationZ = Float(null=False)

    rotationW = Float(null=False)

    labelType = Integer(null=True)

    label = Varchar(length=255, null=True)

    linkType = Integer(null=True)

    link = Varchar(length=255, null=True)

    labelScale = Float(null=True)

    labelWidth = Float(null=True)

    labelHeight = Float(null=True)

    parent = UUID(default=None, null=True)

    layer = UUID(default=None, null=True)


class NodeModel(BaseModel):
    model_config = ConfigDict(from_attributes=True)

    id: uuid.UUID
    name: str
    created_at: datetime | None = None
    active: bool
    positionX: float
    positionY: float
    positionZ: float
    rotationX: float
    rotationY: float
    rotationZ: float
    rotationW: float
    labelType: int | None = None
    label: str | None = None
    linkType: int | None = None
    link: str | None = None
    labelScale: float | None = None
    labelWidth: float | None = None
    labelHeight: float | None = None
    parent: uuid.UUID | None = None
    layer: uuid.UUID | None = None
