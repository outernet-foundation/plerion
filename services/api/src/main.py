import os
from contextlib import asynccontextmanager

from common.fastapi import create_fastapi_app
from fastapi import FastAPI

from .routers.captures import router as captures_router
from .routers.nodes import router as nodes_router
from .routers.reconstructions import router as reconstructions_router
from .settings import get_api_settings

settings = get_api_settings()


@asynccontextmanager
async def lifespan(_: FastAPI):
    # During codegen, we don't want to connect to the database
    if os.environ.get("CODEGEN") == "1":
        yield
    else:
        # Must import here so connection occurs after event loop is running
        from .db.conf import DB

        await DB.start_connection_pool()
        try:
            yield
        finally:
            await DB.close_connection_pool()


app = create_fastapi_app(
    title="Plerion",
    lifespan=lifespan,
)

app.include_router(captures_router)
app.include_router(reconstructions_router)
app.include_router(nodes_router)
