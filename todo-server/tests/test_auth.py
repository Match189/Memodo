"""认证流程测试（SPD §5/§15）。"""
import pytest

pytestmark = pytest.mark.asyncio

EMAIL = "alice@example.com"
PASSWORD = "super-secret-123"


async def _register(client) -> dict:
    r = await client.post(
        "/api/v1/auth/register", json={"email": EMAIL, "password": PASSWORD}
    )
    assert r.status_code == 201
    body = r.json()
    assert body["access_token"] and body["refresh_token"]
    return body


async def test_register_login_refresh(client):
    tokens = await _register(client)

    # 重复注册 → 409
    r = await client.post(
        "/api/v1/auth/register", json={"email": EMAIL, "password": "another-pass-1"}
    )
    assert r.status_code == 409

    # 正确登录
    r = await client.post(
        "/api/v1/auth/login", json={"email": EMAIL, "password": PASSWORD}
    )
    assert r.status_code == 200
    assert r.json()["access_token"]

    # 错误密码
    r = await client.post(
        "/api/v1/auth/login", json={"email": EMAIL, "password": "wrong-password"}
    )
    assert r.status_code == 401

    # refresh 换新 token 对
    r = await client.post("/api/v1/auth/refresh", json={"refresh_token": tokens["refresh_token"]})
    assert r.status_code == 200
    assert r.json()["access_token"] != tokens["access_token"]


async def test_protected_requires_token(client):
    r = await client.get("/api/v1/devices")
    assert r.status_code == 401
    r = await client.get("/api/v1/sync/pull")
    assert r.status_code == 401
