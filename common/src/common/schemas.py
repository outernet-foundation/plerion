from fastapi import Response

binary_schema = {
    "application/octet-stream": {"schema": {"type": "string", "format": "binary"}}
}

zip_schema = {"application/zip": {"schema": {"type": "string", "format": "binary"}}}

tar_schema = {"application/x-tar": {"schema": {"type": "string", "format": "binary"}}}


class OctetStreamResponse(Response):
    media_type = "application/octet-stream"


class ZipStreamResponse(Response):
    media_type = "application/zip"


class TarStreamResponse(Response):
    media_type = "application/x-tar"
