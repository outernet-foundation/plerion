# Wrappers for some Piccolo functionality to provide type signatures that pass strict Pyright checks

from typing import Any, Dict, Self, Tuple, Type, TypeVar, Union, cast

from piccolo.columns.base import Column
from piccolo.columns.combination import And, Or, WhereRaw  # for typing of where clauses
from piccolo.engine.postgres import PostgresEngine as PiccoloPostgresEngine
from piccolo.query import Exists as _Exists
from piccolo.query import Objects as _Objects
from piccolo.query import Select as _Select
from piccolo.query import Update as _Update
from piccolo.table import Table as PiccoloTable
from piccolo.utils.pydantic import (
    create_pydantic_model as _create_pydantic_model,  # type: ignore[import]
)
from pydantic import BaseModel, ConfigDict

_TTable = TypeVar("_TTable", bound="Table")


WhereExpr = Union[And, Or, WhereRaw, object]


class Select(_Select[_TTable]):
    def where(self, *where: WhereExpr) -> Self:
        return super().where(*where)  # type: ignore[override]


class Update(_Update[_TTable]):
    def where(self, *where: WhereExpr) -> Self:
        return super().where(*where)  # type: ignore[override]


class Objects(_Objects[_TTable]):
    def where(self, *where: WhereExpr) -> Self:
        return super().where(*where)  # type: ignore[override]


class Exists(_Exists[_TTable]):
    def where(self, *where: WhereExpr) -> Self:
        return super().where(*where)  # type: ignore[override]


class Table(PiccoloTable):
    @classmethod
    def objects(cls: Type[_TTable], *prefetch: Any) -> Objects[_TTable]:
        return cast(Objects[_TTable], super().objects(*prefetch))  # type: ignore[override]

    @classmethod
    def exists(cls: Type[_TTable]) -> Exists[_TTable]:
        return cast(Exists[_TTable], super().exists())  # type: ignore[override]

    @classmethod
    def select(
        cls: Type[_TTable], *columns: Any, exclude_secrets: bool = False
    ) -> Select[_TTable]:
        return cast(
            Select[_TTable],
            super().select(*columns, exclude_secrets=exclude_secrets),  # type: ignore[override]
        )

    @classmethod
    def update(cls: Type[_TTable], *args: Any, **kwargs: Any) -> Update[_TTable]:
        return cast(Update[_TTable], super().update(*args, **kwargs))  # type: ignore[override]


def create_pydantic_model(
    table: Type[_TTable],
    *,
    nested: bool | Any = False,
    exclude_columns: Tuple[Column, ...] = (),
    include_columns: Tuple[Column, ...] = (),
    include_default_columns: bool = False,
    include_readable: bool = False,
    all_optional: bool = False,
    model_name: str | None = None,
    deserialize_json: bool = False,
    recursion_depth: int = 0,
    max_recursion_depth: int = 5,
    pydantic_config: ConfigDict | None = None,
    json_schema_extra: Dict[str, Any] | None = None,
) -> Type[BaseModel]:
    return _create_pydantic_model(
        table=table,
        nested=nested,
        exclude_columns=exclude_columns,
        include_columns=include_columns,
        include_default_columns=include_default_columns,
        include_readable=include_readable,
        all_optional=all_optional,
        model_name=model_name,
        deserialize_json=deserialize_json,
        recursion_depth=recursion_depth,
        max_recursion_depth=max_recursion_depth,
        pydantic_config=pydantic_config,
        json_schema_extra=json_schema_extra,
    )


class PostgresEngine(PiccoloPostgresEngine):
    async def start_connection_pool(self) -> None:  # type: ignore[override]
        return await super().start_connection_pool()  # type: ignore[override]
