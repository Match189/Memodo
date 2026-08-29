"""同步协议（任务书 §5-§9）：push 逐条 LWW，pull 用 server_seq 游标增量。

LWW 守卫：接受条件 = 传入 updated_at 更大，或等于时按 device_id 字典序决胜。
被拒绝的写入不会改动服务端数据，也不会推进 server_seq。
"""
import json

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy import text
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_db
from ..deps import CurrentUser, get_current_user
from ..schemas import PullOut, PushIn, SyncItemOut

router = APIRouter(prefix="/sync", tags=["sync"])

_PUSH_SQL = text("""
INSERT INTO sync_items (entity, entity_id, data, updated_at, deleted_at, device_id)
VALUES (:entity, :entity_id, :data, :updated_at, :deleted_at, :device_id)
ON CONFLICT (entity, entity_id) DO UPDATE SET
    data = EXCLUDED.data,
    updated_at = EXCLUDED.updated_at,
    deleted_at = EXCLUDED.deleted_at,
    device_id = EXCLUDED.device_id,
    server_seq = nextval('sync_seq')
WHERE EXCLUDED.updated_at > sync_items.updated_at
   OR (EXCLUDED.updated_at = sync_items.updated_at AND EXCLUDED.device_id > sync_items.device_id)
RETURNING server_seq
""")

_PULL_SQL = text("""
SELECT entity, entity_id, data, updated_at, deleted_at, device_id, server_seq
FROM sync_items
WHERE server_seq > :cursor
ORDER BY server_seq
LIMIT :limit
""")


@router.post("/push", response_model=dict)
async def push(
    body: PushIn,
    user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    accepted: list[dict] = []
    rejected: list[dict] = []
    for it in body.items:
        row = await db.execute(_PUSH_SQL, {
            "entity": it.entity,
            "entity_id": it.entity_id,
            "data": json.dumps(it.data, ensure_ascii=False),
            "updated_at": it.updated_at,
            "deleted_at": it.deleted_at,
            "device_id": it.device_id,
        })
        res = row.fetchone()
        if res is not None:
            accepted.append({"entity": it.entity, "entity_id": it.entity_id,
                             "server_seq": res[0]})
        else:
            rejected.append({"entity": it.entity, "entity_id": it.entity_id})
    await db.commit()
    return {"accepted": accepted, "rejected": rejected}


@router.get("/pull", response_model=PullOut)
async def pull(
    cursor: int = Query(0, ge=0),
    limit: int = Query(500, ge=1, le=2000),
    user: CurrentUser = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
):
    rows = (await db.execute(_PULL_SQL, {"cursor": cursor, "limit": limit})).fetchall()
    items = [
        SyncItemOut(
            entity=r[0], entity_id=r[1], data=json.loads(r[2]),
            updated_at=r[3], deleted_at=r[4], device_id=r[5] or "", server_seq=r[6],
        )
        for r in rows
    ]
    next_cursor = items[-1].server_seq if items else cursor
    return PullOut(items=items, cursor=next_cursor)
