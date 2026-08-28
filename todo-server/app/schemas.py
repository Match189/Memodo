"""API 数据结构（pydantic；与 SPD §5-§9 的协议一一对应）。"""
from typing import Any, Literal

from pydantic import BaseModel, EmailStr, Field


# ---------- auth ----------
class RegisterIn(BaseModel):
    email: EmailStr
    password: str = Field(min_length=8, max_length=128)


class LoginIn(BaseModel):
    email: EmailStr
    password: str


class RefreshIn(BaseModel):
    refresh_token: str


class TokenPair(BaseModel):
    access_token: str
    refresh_token: str
    token_type: str = "bearer"


# ---------- devices ----------
class DeviceIn(BaseModel):
    deviceId: str = Field(min_length=1, max_length=64)
    platform: str = ""
    deviceName: str = ""


class DeviceOut(DeviceIn):
    lastSyncAt: int = 0


# ---------- sync ----------
class ChangeIn(BaseModel):
    entity: Literal["todo", "memo"]
    id: str = Field(min_length=1, max_length=64)  # 客户端 uuid
    operation: Literal["upsert", "delete"] = "upsert"
    data: dict[str, Any] = Field(default_factory=dict)
    updatedAt: int
    deletedAt: int | None = None
    deviceId: str = ""


class PushIn(BaseModel):
    deviceId: str
    changes: list[ChangeIn] = Field(default_factory=list)


class ChangeResult(BaseModel):
    id: str
    status: Literal["applied", "rejected", "error"]
    reason: str | None = None


class PushOut(BaseModel):
    results: list[ChangeResult]
    serverTime: int


class ChangeOut(BaseModel):
    entity: str
    id: str
    data: dict[str, Any]
    updatedAt: int
    deletedAt: int | None
    deviceId: str


class PullOut(BaseModel):
    cursor: int
    changes: list[ChangeOut]
    hasMore: bool
    serverTime: int
