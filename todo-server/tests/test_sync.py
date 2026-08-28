"""同步协议测试（SPD §6/§7/§8/§19）：push/pull、LWW、cursor 增量、软删除。"""
import pytest

pytestmark = pytest.mark.asyncio

EMAIL = "bob@example.com"
PASSWORD = "super-secret-123"


async def _auth(client) -> dict:
    r = await client.post(
        "/api/v1/auth/register", json={"email": EMAIL, "password": PASSWORD}
    )
    assert r.status_code == 201
    return r.json()


def _auth_header(tokens: dict) -> dict:
    return {"Authorization": f"Bearer {tokens['access_token']}"}


def _push_body(changes: list[dict], device_id: str = "windows-test") -> dict:
    return {"deviceId": device_id, "changes": changes}


async def test_push_pull_roundtrip(client):
    tokens = await _auth(client)
    h = _auth_header(tokens)

    change = {
        "entity": "todo",
        "id": "todo-1",
        "operation": "upsert",
        "data": {"title": "买牛奶", "done": False},
        "updatedAt": 1000,
        "deviceId": "windows-test",
    }
    r = await client.post("/api/v1/sync/push", json=_push_body([change]), headers=h)
    assert r.status_code == 200
    assert r.json()["results"] == [{"id": "todo-1", "status": "applied", "reason": None}]

    # cursor=0 全量拉回
    r = await client.get("/api/v1/sync/pull?cursor=0", headers=h)
    body = r.json()
    assert body["cursor"] > 0
    assert body["hasMore"] is False
    assert len(body["changes"]) == 1
    got = body["changes"][0]
    assert got["entity"] == "todo"
    assert got["id"] == "todo-1"
    assert got["data"]["title"] == "买牛奶"
    assert got["deviceId"] == "windows-test"

    # cursor 之后没有新变化
    r = await client.get(f"/api/v1/sync/pull?cursor={body['cursor']}", headers=h)
    assert r.json()["changes"] == []
    assert r.json()["cursor"] == body["cursor"]


async def test_lww_rejects_stale(client):
    tokens = await _auth(client)
    h = _auth_header(tokens)

    new_change = {
        "entity": "todo",
        "id": "t1",
        "operation": "upsert",
        "data": {"title": "新版"},
        "updatedAt": 2000,
        "deviceId": "windows-test",
    }
    stale_change = {**new_change, "data": {"title": "旧版"}, "updatedAt": 1000}

    r = await client.post("/api/v1/sync/push", json=_push_body([new_change]), headers=h)
    assert r.json()["results"][0]["status"] == "applied"

    # 相同时间戳 → 允许覆盖（等号 applies，保证幂等重放安全）
    equal_change = {**new_change, "data": {"title": "等时重放"}, "updatedAt": 2000}
    r = await client.post("/api/v1/sync/push", json=_push_body([equal_change]), headers=h)
    assert r.json()["results"][0]["status"] == "applied"

    # 更旧的时间戳 → rejected（SPD §19 LWW）
    r = await client.post("/api/v1/sync/push", json=_push_body([stale_change]), headers=h)
    assert r.json()["results"][0]["status"] == "rejected"
    assert r.json()["results"][0]["reason"] == "stale"

    # 服务器上仍是"新版"
    r = await client.get("/api/v1/todos", headers=h)
    assert r.json()[-1]["data"]["title"] == "等时重放"


async def test_tombstone_and_cursor_increment(client):
    tokens = await _auth(client)
    h = _auth_header(tokens)

    todo_a = {
        "entity": "todo", "id": "a", "operation": "upsert",
        "data": {"title": "A"}, "updatedAt": 100, "deviceId": "d1",
    }
    todo_b = {
        "entity": "memo", "id": "b", "operation": "upsert",
        "data": {"title": "B"}, "updatedAt": 101, "deviceId": "d1",
    }
    r = await client.post("/api/v1/sync/push", json=_push_body([todo_a, todo_b]), headers=h)
    assert all(x["status"] == "applied" for x in r.json()["results"])

    first_pull = (await client.get("/api/v1/sync/pull?cursor=0", headers=h)).json()
    cursor = first_pull["cursor"]
    assert len(first_pull["changes"]) == 2

    # 另一台设备删除 A（软删除墓碑，SPD §18）
    tombstone = {
        "entity": "todo", "id": "a", "operation": "delete",
        "data": {}, "updatedAt": 900, "deletedAt": 900, "deviceId": "android-1",
    }
    r = await client.post(
        "/api/v1/sync/push", json=_push_body([tombstone], device_id="android-1"), headers=h
    )
    assert r.json()["results"][0]["status"] == "applied"

    # 第一台设备增量拉取：只拿到墓碑
    r = await client.get(f"/api/v1/sync/pull?cursor={cursor}", headers=h)
    body = r.json()
    assert len(body["changes"]) == 1
    change = body["changes"][0]
    assert change["id"] == "a"
    assert change["deletedAt"] == 900
    assert change["deviceId"] == "android-1"


async def test_devices_registration(client):
    tokens = await _auth(client)
    h = _auth_header(tokens)

    # push 会自动注册设备（touch_device）
    change = {
        "entity": "todo", "id": "x", "operation": "upsert",
        "data": {"title": "x"}, "updatedAt": 1, "deviceId": "windows-abc",
    }
    await client.post(
        "/api/v1/sync/push", json=_push_body([change], device_id="windows-abc"), headers=h
    )

    r = await client.get("/api/v1/devices", headers=h)
    devices = r.json()
    assert len(devices) == 1
    assert devices[0]["deviceId"] == "windows-abc"
    assert devices[0]["lastSyncAt"] > 0

    # 删除设备
    r = await client.delete("/api/v1/devices/windows-abc", headers=h)
    assert r.status_code == 204
    r = await client.get("/api/v1/devices", headers=h)
    assert r.json() == []
