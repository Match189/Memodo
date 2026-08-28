"""数据库模型（SPD §9/§17/§18）。

Item = 一条 Todo 或 Memo 的服务端事实来源：
- item_uuid 是客户端生成的全局标识
- id（自增）同时充当增量同步 cursor
- updated_at 存客户端时间戳（LWW 依据）；deleted_at 为软删除墓碑
"""
from __future__ import annotations

import time

from sqlalchemy import BigInteger, Index, Integer, String, Text, UniqueConstraint
from sqlalchemy.orm import Mapped, mapped_column

from .db import Base


def now_ms() -> int:
    return int(time.time() * 1000)


class User(Base):
    __tablename__ = "users"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    email: Mapped[str] = mapped_column(String(255), unique=True, index=True)
    password_hash: Mapped[str] = mapped_column(String(255))
    created_at: Mapped[int] = mapped_column(BigInteger, default=now_ms)


class Device(Base):
    __tablename__ = "devices"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    user_id: Mapped[int] = mapped_column(Integer, index=True)
    device_id: Mapped[str] = mapped_column(String(64))
    platform: Mapped[str] = mapped_column(String(32), default="")
    device_name: Mapped[str] = mapped_column(String(128), default="")
    last_sync_at: Mapped[int] = mapped_column(BigInteger, default=0)
    created_at: Mapped[int] = mapped_column(BigInteger, default=now_ms)

    __table_args__ = (UniqueConstraint("user_id", "device_id"),)


class Item(Base):
    __tablename__ = "items"

    # 自增主键 = 全局单调序列，pull 的 cursor 就是它
    # SQLite 只有 INTEGER PRIMARY KEY 才自增，故用 with_variant
    id: Mapped[int] = mapped_column(
        BigInteger().with_variant(Integer, "sqlite"),
        primary_key=True,
        autoincrement=True,
    )
    user_id: Mapped[int] = mapped_column(Integer, index=True)
    entity: Mapped[str] = mapped_column(String(16))  # todo | memo
    item_uuid: Mapped[str] = mapped_column(String(64))
    device_id: Mapped[str] = mapped_column(String(64), default="")
    data: Mapped[str] = mapped_column(Text)  # 客户端字段的 JSON
    updated_at: Mapped[int] = mapped_column(BigInteger)  # 客户端时间戳（LWW 依据）
    deleted_at: Mapped[int | None] = mapped_column(BigInteger, nullable=True)
    server_created_at: Mapped[int] = mapped_column(BigInteger, default=now_ms)

    # 增量同步序列：每次插入/更新都推进（全局 max+1），pull 的 cursor 看它。
    # 原地更新行不会换主键，所以 cursor 不能用主键 id。
    server_seq: Mapped[int | None] = mapped_column(BigInteger, nullable=True)

    __table_args__ = (
        UniqueConstraint("user_id", "entity", "item_uuid"),
        Index("ix_items_user_seq", "user_id", "server_seq"),
    )
