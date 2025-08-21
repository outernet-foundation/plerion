from uuid import UUID

from fastapi import APIRouter, HTTPException, status

from db.tables.captures import Capture

router = APIRouter(prefix="/reconstructions", tags=["reconstructions"])


@router.post("")
async def create_reconstruction(capture_id: UUID):
    # Validate capture id exists
    if not await Capture.objects().get(Capture.id == capture_id):
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=f"Capture with id {capture_id} not found",
        )

    pass
