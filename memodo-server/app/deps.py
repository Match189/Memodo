"""依赖：从 Bearer Token 解析当前用户。"""
from dataclasses import dataclass

from fastapi import Depends, HTTPException, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy.ext.asyncio import AsyncSession

from .db import get_db
from .security import decode_jwt

bearer = HTTPBearer(auto_error=False)


@dataclass
class CurrentUser:
    id: str
    email: str


async def get_current_user(
    creds: HTTPAuthorizationCredentials | None = Depends(bearer),
    _db: AsyncSession = Depends(get_db),
) -> CurrentUser:
    if creds is None or not creds.credentials:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "missing token")
    payload = decode_jwt(creds.credentials)
    if payload is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "invalid or expired token")
    sub = payload.get("sub")
    email = payload.get("email", "")
    if not sub:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "invalid token")
    return CurrentUser(id=sub, email=email)
