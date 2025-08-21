from common.fastapi import create_fastapi_app

from .routers.captures import router as captures_router

app = create_fastapi_app("Zed API")

app.include_router(captures_router)
