from piccolo.columns import UUID, Array, BigInt, Boolean, Float, Integer, Varchar

from ..piccolo_shims import Table


# require=true
# https://github.com/piccolo-orm/piccolo/issues/1132
class LocalizationMap(Table):
    id = UUID(primary_key=True, required=True)

    name = Varchar(length=255, unique=False, required=True)

    active = Boolean(True, null=False, required=True)

    lighting = Integer(null=False, required=True)

    color = BigInt(null=False, required=True)

    positionX = Float(null=False, required=True)

    positionY = Float(null=False, required=True)

    positionZ = Float(null=False, required=True)

    rotationX = Float(null=False, required=True)

    rotationY = Float(null=False, required=True)

    rotationZ = Float(null=False, required=True)

    rotationW = Float(null=False, required=True)

    points = Array(base_column=Float())
