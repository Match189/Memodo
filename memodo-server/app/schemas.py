"""Pydantic 请求/响应模型。"""
from pydantic import BaseModel


class RegisterIn(BaseModel):
    email: str
    password: str


class LoginIn(BaseModel):
    email: str
    password: str


class TokenPair(BaseModel):
    access_token: str
    refresh_token: str
    token_type: str = "bearer"


class RefreshIn(BaseModel):
    refresh_token: str


class UserOut(BaseModel):
    id: str
    email: str


class DeviceRegisterIn(BaseModel):
    device_id: str
    name: str = ""


class DeviceOut(BaseModel):
    id: str
    device_id: str
    name: str


class SyncItemIn(BaseModel):
    entity: str
    entity_id: str
    data: dict
    updated_at: int
    deleted_at: int | None = None
    device_id: str = ""


class PushIn(BaseModel):
    items: list[SyncItemIn]


class SyncItemOut(BaseModel):
    entity: str
    entity_id: str
    data: dict
    updated_at: int
    deleted_at: int | None
    device_id: str
    server_seq: int


class PullOut(BaseModel):
    items: list[SyncItemOut]
    cursor: int
