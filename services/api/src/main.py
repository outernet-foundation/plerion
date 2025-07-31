import os
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.routing import APIRoute
from mangum import Mangum

from .routers.captures import router as captures_router


# Make codegened client functions use the same name as their corresponding server functions
def use_handler_name(route: APIRoute) -> str:
    return route.name


@asynccontextmanager
async def lifespan(_: FastAPI):
    # Importing the DB connects to the DB, which we don't want to during openapi generation.
    if os.environ.get("DONT_IMPORT_DB") == "1":
        yield
    else:
        # Must import here so connection occurs after event loop is running
        from .db.conf import DB

        await DB.start_connection_pool()
        try:
            yield
        finally:
            await DB.close_connection_pool()


app = FastAPI(
    title="Plerion API",
    version="0.0.1",
    generate_unique_id_function=use_handler_name,
    lifespan=lifespan,
)

handler = Mangum(app)

app.include_router(captures_router)
