"""pytest 夹具：每测试独立 SQLite 文件库 + httpx AsyncClient。"""
import os

os.environ["TODOLIST_DATABASE_URL"] = "sqlite+aiosqlite:///./test.db"

import pytest_asyncio
from httpx import ASGITransport, AsyncClient

from app.db import get_engine, init_db
from app.main import app
from app.models import Base


@pytest_asyncio.fixture
async def client():
    await init_db()
    async with AsyncClient(
        transport=ASGITransport(app=app), base_url="http://test"
    ) as c:
        yield c
    # 每个测试后整表重建：DELETE 会让 rowid 复用造成跨测试串数据
    async with get_engine().begin() as conn:
        await conn.run_sync(Base.metadata.drop_all)
        await conn.run_sync(Base.metadata.create_all)
