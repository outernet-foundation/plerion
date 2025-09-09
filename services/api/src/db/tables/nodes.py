from piccolo.columns import UUID, Boolean, Float, Integer, Timestamp, Varchar
from piccolo.columns.defaults.timestamp import TimestampNow

from ..piccolo_shims import Table


# require=true
# https://github.com/piccolo-orm/piccolo/issues/1132
class Node(Table):
    id = UUID(primary_key=True, required=True)

    name = Varchar(length=255, unique=True, required=True)

    created_at = Timestamp(default=TimestampNow(), null=True, required=False)

    active = Boolean(True, null=False, required=True)

    positionX = Float(null=False, required=True)

    positionY = Float(null=False, required=True)

    positionZ = Float(null=False, required=True)

    rotationX = Float(null=False, required=True)

    rotationY = Float(null=False, required=True)

    rotationZ = Float(null=False, required=True)

    rotationW = Float(null=False, required=True)

    labelType = Integer(null=True, required=False)

    label = Varchar(length=255, null=True, required=False)

    linkType = Integer(null=True, required=False)

    link = Varchar(length=255, null=True, required=False)

    labelScale = Float(null=True, required=False)

    labelWidth = Float(null=True, required=False)

    labelHeight = Float(null=True, required=False)

    parent = UUID(null=True, required=False)

    layer = UUID(null=True, required=False)

    something = Float(null=True, required=False)
