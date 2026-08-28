"""只读数据接口（SPD §5：GET /todos、GET /memos），便于调试与脚本。"""
import json

from fastapi import APIRouter, Depends
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..deps import get_current_user
from ..models import Item, User
from ..schemas import ChangeOut

router = APIRouter(prefix="/api/v1", tags=["data"])


async def _list(entity: str, user: User, session: AsyncSession):
    rows = (
        await session.execute(
            select(Item)
            .where(Item.user_id == user.id, Item.entity == entity)
            .order_by(Item.id)
        )
    ).scalars().all()
    return [
        ChangeOut(
            entity=r.entity,
            id=r.item_uuid,
            data=json.loads(r.data),
            updatedAt=r.updated_at,
            deletedAt=r.deleted_at,
            deviceId=r.device_id,
        )
        for r in rows
    ]


@router.get("/todos", response_model=list[ChangeOut])
async def list_todos(
    user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session),
):
    return await _list("todo", user, session)


@router.get("/memos", response_model=list[ChangeOut])
async def list_memos(
    user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session),
):
    return await _list("memo", user, session)
