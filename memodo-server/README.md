# 念念 Memodo 同步服务器

FastAPI + PostgreSQL 实现的「共享数据协议」服务端（任务书 §5-§9、§28）。
**只负责协议，不持有任何客户端 UI**——Windows(WPF) 与 Android(Compose) 各自渲染。

## 协议概览

- **认证**：`/auth/register`、`/auth/login` 返回 `access_token`(HS256 JWT, 15min) +
  `refresh_token`(7d，可轮换)。受保护接口带 `Authorization: Bearer <token>`。
- **设备**：`/devices/register` 按 `(user_id, device_id)` 幂等登记；`/devices/heartbeat` 心跳。
- **同步 push**：`POST /sync/push` 逐条上报 `{entity, entity_id, data, updated_at, deleted_at, device_id}`。
  服务端按 `(entity, entity_id)` 做 **LWW 合并**：接受条件 = `updated_at` 更大，
  或相等时按 `device_id` 字典序决胜（任务书 §19）。被拒的写入不改数据、不前进游标。
- **同步 pull**：`GET /sync/pull?cursor=&limit=` 返回 `server_seq > cursor` 的增量，
  以及新的 `cursor`（`server_seq` 每次「被接受的写入」都会前进，覆盖 insert 与 update）。

数据以 JSON 存于统一的 `sync_items` 表（`data` 列），软删除走 `deleted_at` 墓碑，
与双端本地库的列语义保持一致。

## 运行（Docker，推荐）

```bash
cp .env.example .env        # 改 JWT_SECRET
docker compose up --build
# 文档: http://localhost:8000/docs
```

## 运行（本地 Python）

```bash
pip install -r requirements.txt
cp .env.example .env
uvicorn app.main:app --reload --port 8000
```

需本机可访问的 PostgreSQL（库名/账号见 `.env` 的 `DATABASE_URL`）。
启动时 `init_db()` 自动建表。

## 目录

```
app/
  config.py     # 配置（环境变量 / .env）
  db.py         # 异步引擎 / 会话 / init_db
  models.py     # users / devices / refresh_tokens / sync_items
  schemas.py    # 请求响应模型
  security.py   # HS256 JWT + PBKDF2 口令哈希（仅标准库依赖）
  deps.py       # Bearer 解析当前用户
  main.py       # FastAPI 装配 + CORS + 启动建表
  routers/      # auth / devices / sync
```

## 说明

本服务仅定义并托管协议；Windows / Android 客户端已在「设置 → 同步方式 → 自建服务器」接入
（注册 → 登录 → push/pull，详见客户端 README）。

## 数据隔离与墓碑回收（2026-08 更新）

- `sync_items` 行按 **user_id 隔离**，唯一键为 `(user_id, entity, entity_id)`——
  用户之间数据完全不可见。
- 拉取游标索引为 `(user_id, server_seq)`，pull 只返回本用户的增量。
- **软删除墓碑保留 90 天**后物理清理（全量 pull 时顺带执行），防止快照无限增长。

## 升级注意（旧库迁移）

v0.1 旧库的 `sync_items` 没有 user_id 列。升级时**最简单可靠的方式是清库重建**
（客户端数据在本地完整，重新注册账号后 push 即恢复云端）：

```bash
docker compose down
docker volume rm memodo-server_pgdata   # 卷名以 docker volume ls 实际为准
docker compose up --build
```

或手工迁移（保留数据）：

```sql
ALTER TABLE sync_items ADD COLUMN user_id UUID REFERENCES users(id);
UPDATE sync_items s SET user_id = u.id FROM users u LIMIT 1;  -- 单用户场景直接归属
-- 迁移后重建唯一约束/索引（见 app/models.py SyncItem.__table_args__）
```

## 测试（2026-09 起）

全链路回归脚本：`../.qa/test-server.mjs`（Node ≥18，无第三方依赖），共 24 个用例，
覆盖认证（注册/登录/refresh 轮换与重放）、push LWW 三分支（旧拒/新收/平局 device_id 决胜）、
pull cursor 增量与分页、**E2EE 行级密文字符串**（推/存/回读/口令解出/无明文泄露）、
墓碑、双用户数据隔离、未授权访问：

```bash
node ../.qa/test-server.mjs http://localhost:8000
# 输出 "24 passed, 0 failed" 即全部通过
```

配套资产：`../.qa/CryptoHarness/`（C# SyncCrypto 自测与 C#↔Node 跨语言互操作）、
`../.qa/webdav-mock.mjs`（WebDAV 快照全链路模拟器）。E2EE 行级依赖 `data: dict | str`
（schemas.py，2026-09-01 修复）；90 天前墓碑在 cursor=0 全量拉取时清理属设计行为。
