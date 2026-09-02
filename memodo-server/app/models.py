"""ORM 模型（任务书 §5 数据协议）。

核心是一张统一同步表 sync_items：客户端把每一行业务数据作为 JSON 上报，
服务端按 (entity, entity_id) 做 LWW 合并，并用 server_seq 单调序列支撑增量拉取。
软删除走 deleted_at 墓碑，与双端本地库一致。
"""
import uuid
from datetime import datetime, timezone

from sqlalchemy import (
    BigInteger,
    Boolean,
    Column,
    DateTime,
    ForeignKey,
    Index,
    Integer,
    Sequence,
    String,
    Text,
    UniqueConstraint,
    func,
    text,
)
from sqlalchemy.dialects.postgresql import UUID as PG_UUID

from .db import Base


def _utcnow() -> datetime:
    return datetime.now(timezone.utc)


class User(Base):
    __tablename__ = "users"

    id = Column(PG_UUID(as_uuid=True), primary_key=True,
                server_default=text("gen_random_uuid()"))
    email = Column(String(255), unique=True, index=True, nullable=False)
    password_hash = Column(String(255), nullable=False)
    created_at = Column(DateTime(timezone=True), server_default=func.now())


class Device(Base):
    __tablename__ = "devices"
    __table_args__ = (UniqueConstraint("user_id", "device_id", name="uq_user_device"),)

    id = Column(PG_UUID(as_uuid=True), primary_key=True,
                server_default=text("gen_random_uuid()"))
    user_id = Column(PG_UUID(as_uuid=True),
                     ForeignKey("users.id", ondelete="CASCADE"),
                     nullable=False, index=True)
    device_id = Column(String(64), nullable=False)
    name = Column(String(120), nullable=False, default="")
    created_at = Column(DateTime(timezone=True), server_default=func.now())


class RefreshToken(Base):
    __tablename__ = "refresh_tokens"

    id = Column(PG_UUID(as_uuid=True), primary_key=True,
                server_default=text("gen_random_uuid()"))
    user_id = Column(PG_UUID(as_uuid=True),
                     ForeignKey("users.id", ondelete="CASCADE"),
                     nullable=False, index=True)
    token_hash = Column(String(255), nullable=False, unique=True)
    expires_at = Column(DateTime(timezone=True), nullable=False)
    revoked = Column(Boolean, default=False)


class SyncItem(Base):
    """统一同步行（按用户隔离）。server_seq 每次“被接受的写入”都会前进，用于 cursor 增量。"""
    __tablename__ = "sync_items"

    id = Column(BigInteger, primary_key=True, autoincrement=True)
    user_id = Column(PG_UUID(as_uuid=True),
                     ForeignKey("users.id", ondelete="CASCADE"),
                     nullable=False, index=True)
    server_seq = Column(BigInteger, Sequence("sync_seq"), nullable=False,
                        unique=True, index=True)
    entity = Column(String(32), nullable=False)       # tasks/memos/boards/...
    entity_id = Column(String(64), nullable=False)
    data = Column(Text, nullable=False)              # JSON 业务行
    updated_at = Column(BigInteger, nullable=False)   # 客户端 LWW 时钟(ms)
    deleted_at = Column(BigInteger, nullable=True)    # 墓碑
    device_id = Column(String(64), nullable=True)
    created_at = Column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (
        UniqueConstraint("user_id", "entity", "entity_id", name="uq_user_entity_row"),
        Index("ix_user_seq", "user_id", "server_seq"),
    )
