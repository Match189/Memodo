"""同步路由（SPD §6/§7/§8）：/sync/push 与 /sync/pull，LWW + cursor 增量。"""
import json

from fastapi import APIRouter, Depends, Query
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..config import get_settings
from ..db import get_session
from ..deps import get_current_user
from ..models import Item, User, now_ms
from ..routers.devices import touch_device
from ..schemas import ChangeOut, ChangeResult, PullOut, PushIn, PushOut

router = APIRouter(prefix="/api/v1/sync", tags=["sync"])


def _dumps(data: dict) -> str:
    return json.dumps(data, ensure_ascii=False, separators=(",", ":"))


def _loads(raw: str) -> dict:
    return json.loads(raw)


async def _next_seq(session: AsyncSession, user_id: int) -> int:
    """该用户的下一个单调序列号（每次写入推进；并发低，够用）。"""
    current = (
        await session.execute(
            select(func.max(Item.server_seq)).where(Item.user_id == user_id)
        )
    ).scalar_one()
    return (current or 0) + 1


@router.post("/push", response_model=PushOut)
async def push(
    body: PushIn,
    user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session),
):
    """SPD §6：逐条 LWW 应用客户端变更。

    rejected = 服务器上的版本更新（客户端应丢弃自己的旧版本）。
    """
    results: list[ChangeResult] = []
    for change in body.changes:
        if change.entity not in ("todo", "memo"):
            results.append(
                ChangeResult(id=change.id, status="error", reason="bad entity")
            )
            continue
        existing = (
            await session.execute(
                select(Item).where(
                    Item.user_id == user.id,
                    Item.entity == change.entity,
                    Item.item_uuid == change.id,
                )
            )
        ).scalar_one_or_none()

        if existing is not None and change.updatedAt < existing.updated_at:
            results.append(ChangeResult(id=change.id, status="rejected", reason="stale"))
            continue

        deleted_at = change.deletedAt if change.operation == "delete" else None
        seq = await _next_seq(session, user.id)
        if existing is None:
            session.add(
                Item(
                    user_id=user.id,
                    entity=change.entity,
                    item_uuid=change.id,
                    device_id=change.deviceId or body.deviceId,
                    data=_dumps(change.data),
                    updated_at=change.updatedAt,
                    deleted_at=deleted_at,
                    server_seq=seq,
                )
            )
        else:
            existing.device_id = change.deviceId or body.deviceId
            existing.data = _dumps(change.data)
            existing.updated_at = change.updatedAt
            existing.deleted_at = deleted_at
            existing.server_seq = seq
        results.append(ChangeResult(id=change.id, status="applied"))

    await touch_device(session, user.id, body.deviceId)
    await session.commit()
    return PushOut(results=results, serverTime=now_ms())


@router.get("/pull", response_model=PullOut)
async def pull(
    cursor: int = Query(default=0, ge=0),
    deviceId: str | None = Query(default=None, max_length=64),
    user: User = Depends(get_current_user),
    session: AsyncSession = Depends(get_session),
):
    """SPD §7/§8：返回 cursor 之后的变化，按 server 序分页。

    cursor 用服务端自增 server_seq，单调不回退；客户端持久化它实现增量。
    可选 deviceId 用于设备心跳。
    """
    page = get_settings().pull_page_size
    if deviceId:
        await touch_device(session, user.id, deviceId)
    rows = (
        await session.execute(
            select(Item)
            .where(Item.user_id == user.id, Item.server_seq > cursor)
            .order_by(Item.server_seq)
            .limit(page)
        )
    ).scalars().all()

    changes = [
        ChangeOut(
            entity=r.entity,
            id=r.item_uuid,
            data=_loads(r.data),
            updatedAt=r.updated_at,
            deletedAt=r.deleted_at,
            deviceId=r.device_id,
        )
        for r in rows
    ]
    new_cursor = rows[-1].server_seq if rows else cursor
    return PullOut(
        cursor=new_cursor,
        changes=changes,
        hasMore=len(rows) == page,
        serverTime=now_ms(),
    )
