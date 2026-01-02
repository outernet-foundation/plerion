# Vibe code: Gemini 3
#
# OpenAPI Operation subclass that adds 'encoding' metadata for multipart/form-data requests.
# This ensures that clients understand which parts are JSON and which are binary files.

from typing import Any, Dict, cast

from litestar.openapi.spec import Operation


class MultipartJsonBinaryOperation(Operation):
    def to_schema(self) -> dict[str, Any]:
        schema_dict: dict[str, Any] = super().to_schema()

        if "requestBody" not in schema_dict:
            return schema_dict

        request_body = cast(Dict[str, Any], schema_dict["requestBody"])
        content = cast(Dict[str, Any], request_body.get("content", {}))

        if "multipart/form-data" not in content:
            return schema_dict

        media_type_obj = cast(Dict[str, Any], content["multipart/form-data"])
        schema = cast(Dict[str, Any], media_type_obj.get("schema", {}))
        properties = cast(Dict[str, Any], schema.get("properties", {}))

        encoding_map: dict[str, Any] = {}

        prop_name: str
        prop_schema: dict[str, Any]

        for prop_name, prop_val in properties.items():
            prop_schema = cast(Dict[str, Any], prop_val)

            # Heuristic: If it's a $ref, it's a complex object (Pydantic Model)
            is_ref: bool = "$ref" in prop_schema

            # Heuristic: If it's an array of $refs
            # We explicitly cast 'items' to dict to allow the 'in' check
            items_schema = cast(Dict[str, Any], prop_schema.get("items", {}))
            is_array_ref: bool = prop_schema.get("type") == "array" and "$ref" in items_schema

            if is_ref or is_array_ref:
                encoding_map[prop_name] = {"contentType": "application/json"}

        # Apply the encoding map if we found any JSON fields
        if encoding_map:
            if "encoding" not in media_type_obj:
                media_type_obj["encoding"] = {}

            encoding_dict = cast(Dict[str, Any], media_type_obj["encoding"])
            encoding_dict.update(encoding_map)

        return schema_dict
