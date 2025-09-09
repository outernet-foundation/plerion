from __future__ import annotations

from common.fastapi import create_fastapi_app
from mangum import Mangum

from .routers import databases

app = create_fastapi_app(title="Database Manager")
app.include_router(databases.router)
handler = Mangum(app)
