"""TodoList 自建同步服务器入口。

上线：uvicorn app.main:app --host 0.0.0.0 --port 8080
开发：DATABASE_URL=sqlite+aiosqlite:///./dev.db 时自动建表。
"""
from contextlib import asynccontextmanager

from fastapi import FastAPI

from .config import get_settings
from .db import init_db
from .routers import auth, data, devices, sync


@asynccontextmanager
async def lifespan(app: FastAPI):
    # SQLite（开发/测试）自动建表；PostgreSQL 生产建议 Alembic
    if get_settings().database_url.startswith("sqlite"):
        await init_db()
    yield


app = FastAPI(title="TodoList Sync Server", version="1.0.0", lifespan=lifespan)

app.include_router(auth.router)
app.include_router(devices.router)
app.include_router(sync.router)
app.include_router(data.router)


@app.get("/health")
async def health():
    return {"ok": True}
