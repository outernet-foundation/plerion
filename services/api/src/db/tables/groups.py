from piccolo.columns import UUID, Varchar

from ..piccolo_shims import Table


# require=true
# https://github.com/piccolo-orm/piccolo/issues/1132
class Group(Table):
    id = UUID(primary_key=True, required=True)

    name = Varchar(length=255, unique=False, required=True)

    parent = UUID(default=None, null=True, required=False)
