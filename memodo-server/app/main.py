"""念念 Memodo 同步服务器（FastAPI + PostgreSQL）。

仅负责「共享数据协议」（任务书 §28）：认证、设备、LWW 同步。
不持有任何客户端 UI。
"""
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from .config import settings
from .db import init_db
from .routers import auth, devices, sync


@asynccontextmanager
async def lifespan(app: FastAPI):
    await init_db()
    yield


app = FastAPI(title="念念 Memodo Sync Server", version="0.2.0", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.cors_origin_list,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(auth.router)
app.include_router(devices.router)
app.include_router(sync.router)


@app.get("/health")
async def health():
    return {"status": "ok"}
