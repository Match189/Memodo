"""密码哈希与 JWT（SPD §15：禁止明文密码；日志禁止输出 Token）。"""
from datetime import datetime, timedelta, timezone

from jose import JWTError, jwt
from passlib.context import CryptContext

from .config import get_settings

_pwd = CryptContext(schemes=["bcrypt"], deprecated="auto")

_ALGO = "HS256"


def hash_password(raw: str) -> str:
    return _pwd.hash(raw)


def verify_password(raw: str, hashed: str) -> bool:
    return _pwd.verify(raw, hashed)


def _make_token(subject: str, kind: str, minutes: int) -> str:
    now = datetime.now(timezone.utc)
    payload = {
        "sub": subject,
        "typ": kind,
        "iat": int(now.timestamp()),
        "exp": int((now + timedelta(minutes=minutes)).timestamp()),
    }
    return jwt.encode(payload, get_settings().secret_key, algorithm=_ALGO)


def make_access_token(user_id: int | str) -> str:
    return _make_token(str(user_id), "access", get_settings().access_token_minutes)


def make_refresh_token(user_id: int | str) -> str:
    days = get_settings().refresh_token_days
    return _make_token(str(user_id), "refresh", days * 24 * 60)


def read_token(token: str, expected_kind: str) -> int | None:
    """返回 user_id；无效/过期/类型不符返回 None。"""
    try:
        payload = jwt.decode(token, get_settings().secret_key, algorithms=[_ALGO])
    except JWTError:
        return None
    if payload.get("typ") != expected_kind:
        return None
    sub = payload.get("sub")
    try:
        return int(sub)
    except (TypeError, ValueError):
        return None
