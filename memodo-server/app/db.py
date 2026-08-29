"""异步 SQLAlchemy 引擎与会话（SQLAlchemy 2.0 async）。"""
from collections.abc import AsyncIterator

from sqlalchemy.ext.asyncio import (
    AsyncSession,
    async_sessionmaker,
    create_async_engine,
)
from sqlalchemy.orm import DeclarativeBase

from .config import settings


class Base(DeclarativeBase):
    pass


engine = create_async_engine(settings.database_url, echo=False, future=True)
SessionLocal = async_sessionmaker(engine, class_=AsyncSession, expire_on_commit=False)


async def get_db() -> AsyncIterator[AsyncSession]:
    async with SessionLocal() as session:
        yield session


async def init_db() -> None:
    # 同步协议只需要建表；server_seq 序列随 SyncItem 自动创建。
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)
