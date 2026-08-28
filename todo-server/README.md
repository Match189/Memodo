# todo-server — TodoList 自建同步服务器

SPD Phase 5 交付物：FastAPI + SQLAlchemy + JWT + cursor 增量同步 + Docker 一键部署。
数据库默认 PostgreSQL（compose），开发/测试可用 SQLite（自动建表）。

## 快速开始（开发）

```bash
cd todo-server
uv python install 3.12            # 或任何 Python ≥ 3.11
uv venv .venv --python 3.12
. .venv/Scripts/activate          # Windows；Linux/macOS: source .venv/bin/activate
uv pip install -r requirements.txt

# 跑测试（SQLite 内存库）
python -m pytest -q

# 起服务（SQLite 文件库，自动建表）
TODOLIST_DATABASE_URL="sqlite+aiosqlite:///./dev.db" python -m uvicorn app.main:app --port 8080
```

## Docker 部署（生产）

```bash
cp .env.example .env    # 填 SECRET_KEY / POSTGRES_PASSWORD
docker compose up -d
# 服务在 http://127.0.0.1:8080，公网请用 Caddy/Nginx 反代加 HTTPS
```

## 环境变量

| 变量 | 说明 | 默认 |
| --- | --- | --- |
| TODOLIST_SECRET_KEY | JWT 签名密钥（**生产必填**） | dev-only-secret-change-me |
| TODOLIST_DATABASE_URL | SQLAlchemy 异步连接串 | sqlite+aiosqlite:///./todo-server.db |
| TODOLIST_ACCESS_TOKEN_MINUTES | access token 有效期（分） | 720 |
| TODOLIST_REFRESH_TOKEN_DAYS | refresh token 有效期（天） | 30 |
| TODOLIST_PULL_PAGE_SIZE | pull 分页大小 | 500 |

## API（SPD §5-§9）

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| POST | /api/v1/auth/register | 注册，返回 token 对 |
| POST | /api/v1/auth/login | 登录 |
| POST | /api/v1/auth/refresh | 刷新 token |
| GET/POST/DELETE | /api/v1/devices[/{id}] | 设备管理 |
| POST | /api/v1/sync/push | 推送变更（逐条 LWW） |
| GET | /api/v1/sync/pull?cursor=N | 增量拉取（cursor 之后的变化） |
| GET | /api/v1/todos、/api/v1/memos | 全量只读 |
| GET | /health | 探活 |

同步协议要点：

- **id** = 客户端 uuid；服务端自增 id 即 **cursor**
- **LWW**：`updatedAt` 新者胜；更旧的推送被 `rejected: stale`；等时重放幂等 applied
- **软删除**：`operation: "delete"` + `deletedAt` 墓碑，不做物理删除
- 所有写操作自动注册/更新设备心跳（lastSyncAt）

## 安全

- 密码 bcrypt 哈希存储；JWT HS256（access 12h / refresh 30d）
- Secret 只从环境变量读取；日志不打印 Token
- 公网部署务必 HTTPS（Caddy 两行配置即可）
