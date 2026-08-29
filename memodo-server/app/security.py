"""极简安全工具：HS256 JWT（stdlib hmac，无 PyJWT 依赖）+ PBKDF2 口令哈希。

生产环境请确保 jwt_secret 足够随机且通过环境变量注入。
"""
import base64
import hashlib
import hmac
import json
import os
import time
import uuid

from .config import settings


# ---------- JWT (HS256) ----------
def _b64u(b: bytes) -> bytes:
    return base64.urlsafe_b64encode(b).rstrip(b"=")


def _b64d(s: str) -> bytes:
    return base64.urlsafe_b64decode(s + "=" * (-len(s) % 4))


def encode_jwt(payload: dict, expire_minutes: int | None = None) -> str:
    now = int(time.time())
    body = dict(payload)
    body["iat"] = now
    body["exp"] = now + (expire_minutes or settings.access_token_expire_minutes) * 60
    header = _b64u(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    payload_b = _b64u(json.dumps(body).encode())
    signing_input = header + b"." + payload_b
    sig = hmac.new(settings.jwt_secret.encode(), signing_input, hashlib.sha256).digest()
    return (signing_input + b"." + _b64u(sig)).decode()


def decode_jwt(token: str) -> dict | None:
    try:
        header_b, payload_b, sig_b = token.split(".")
    except ValueError:
        return None
    signing_input = (header_b + "." + payload_b).encode()
    expected = _b64u(hmac.new(settings.jwt_secret.encode(), signing_input, hashlib.sha256).digest())
    if not hmac.compare_digest(expected, sig_b.encode()):
        return None
    try:
        body = json.loads(_b64d(payload_b))
    except Exception:
        return None
    if body.get("exp", 0) < int(time.time()):
        return None
    return body


# ---------- 口令哈希 (PBKDF2-HMAC-SHA256) ----------
def hash_password(password: str) -> str:
    salt = os.urandom(16)
    dk = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, 100_000)
    return f"pbkdf2_sha256$100000${salt.hex()}${dk.hex()}"


def verify_password(password: str, stored: str) -> bool:
    try:
        _, rounds, salt_hex, hash_hex = stored.split("$")
        dk = hashlib.pbkdf2_hmac("sha256", password.encode(), bytes.fromhex(salt_hex), int(rounds))
        return hmac.compare_digest(dk.hex(), hash_hex)
    except Exception:
        return False


def new_token_id() -> str:
    return uuid.uuid4().hex
