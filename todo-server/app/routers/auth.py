"""认证路由（SPD §5：register / login / refresh）。"""
from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..models import User
from ..schemas import LoginIn, RefreshIn, RegisterIn, TokenPair
from ..security import make_access_token, make_refresh_token, read_token, verify_password, hash_password

router = APIRouter(prefix="/api/v1/auth", tags=["auth"])


def _pair(user_id: int) -> TokenPair:
    return TokenPair(
        access_token=make_access_token(user_id),
        refresh_token=make_refresh_token(user_id),
    )


@router.post("/register", response_model=TokenPair, status_code=status.HTTP_201_CREATED)
async def register(body: RegisterIn, session: AsyncSession = Depends(get_session)):
    exists = (
        await session.execute(select(User).where(User.email == body.email))
    ).scalar_one_or_none()
    if exists is not None:
        raise HTTPException(status.HTTP_409_CONFLICT, "email already registered")
    user = User(email=body.email, password_hash=hash_password(body.password))
    session.add(user)
    await session.commit()
    await session.refresh(user)
    return _pair(user.id)


@router.post("/login", response_model=TokenPair)
async def login(body: LoginIn, session: AsyncSession = Depends(get_session)):
    user = (
        await session.execute(select(User).where(User.email == body.email))
    ).scalar_one_or_none()
    if user is None or not verify_password(body.password, user.password_hash):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "wrong email or password")
    return _pair(user.id)


@router.post("/refresh", response_model=TokenPair)
async def refresh(body: RefreshIn, session: AsyncSession = Depends(get_session)):
    user_id = read_token(body.refresh_token, "refresh")
    if user_id is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "invalid refresh token")
    user = await session.get(User, user_id)
    if user is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "user not found")
    return _pair(user.id)
