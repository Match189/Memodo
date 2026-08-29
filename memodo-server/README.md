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

本服务仅定义并托管协议；Windows / Android 客户端的「接入服务端」为后续里程碑。
