"""认证：注册 / 登录 / 刷新 / 当前用户（任务书 §5 JWT）。"""
from datetime import datetime, timedelta, timezone

from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from ..config import settings
from ..db import get_db
from ..deps import CurrentUser, get_current_user
from ..models import RefreshToken, User
from ..schemas import LoginIn, RefreshIn, RegisterIn, TokenPair, UserOut
from ..security import (
    encode_jwt,
    hash_password,
    new_token_id,
    verify_password,
)

router = APIRouter(prefix="/auth", tags=["auth"])


async def _get_user_by_email(db: AsyncSession, email: str) -> User | None:
    res = await db.execute(select(User).where(User.email == email))
    return res.scalar_one_or_none()


@router.post("/register", response_model=UserOut, status_code=status.HTTP_201_CREATED)
async def register(body: RegisterIn, db: AsyncSession = Depends(get_db)):
    if await _get_user_by_email(db, body.email):
        raise HTTPException(status.HTTP_409_CONFLICT, "email already registered")
    user = User(email=body.email, password_hash=hash_password(body.password))
    db.add(user)
    await db.commit()
    await db.refresh(user)
    return UserOut(id=str(user.id), email=user.email)


@router.post("/login", response_model=TokenPair)
async def login(body: LoginIn, db: AsyncSession = Depends(get_db)):
    user = await _get_user_by_email(db, body.email)
    if user is None or not verify_password(body.password, user.password_hash):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "invalid credentials")
    access = encode_jwt({"sub": str(user.id), "email": user.email})

    plain = new_token_id() + new_token_id()
    expires = datetime.now(timezone.utc) + timedelta(days=settings.refresh_token_expire_days)
    db.add(RefreshToken(
        user_id=user.id, token_hash=_hash_token(plain),
        expires_at=expires,
    ))
    await db.commit()
    return TokenPair(access_token=access, refresh_token=plain)


@router.post("/refresh", response_model=TokenPair)
async def refresh(body: RefreshIn, db: AsyncSession = Depends(get_db)):
    # refresh token 本身不带签名，这里仅用哈希比对 + 过期/吊销检查
    row = (await db.execute(
        select(RefreshToken).where(RefreshToken.token_hash == _hash_token(body.refresh_token))
    )).scalar_one_or_none()
    if row is None or row.revoked or row.expires_at < datetime.now(timezone.utc):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "invalid refresh token")
    user = (await db.execute(select(User).where(User.id == row.user_id))).scalar_one_or_none()
    if user is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "invalid refresh token")
    access = encode_jwt({"sub": str(user.id), "email": user.email})
    # 轮换 refresh token
    row.revoked = True
    new_plain = new_token_id() + new_token_id()
    new_exp = datetime.now(timezone.utc) + timedelta(days=settings.refresh_token_expire_days)
    db.add(RefreshToken(user_id=user.id, token_hash=_hash_token(new_plain), expires_at=new_exp))
    await db.commit()
    return TokenPair(access_token=access, refresh_token=new_plain)


@router.get("/me", response_model=UserOut)
async def me(user: CurrentUser = Depends(get_current_user)):
    return UserOut(id=user.id, email=user.email)


def _hash_token(t: str) -> str:
    import hashlib
    return hashlib.sha256(t.encode()).hexdigest()
