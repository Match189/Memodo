# SPD — TodoList 跨平台 Todo / Memo / Widget / Sync

> **项目正式名称**：TodoList — Cross-platform Todo & Memo with Desktop/Mobile Widgets and Pluggable Sync
>
> 本文档是项目的**开发标准（SPD）**。所有代码变更必须符合本文档的架构原则与禁止事项。
> 各条目的当前实现状态以 🟢（已达成）/ 🟡（部分）/ ⬜（待开发） 标注，详见 [PHASES.md](PHASES.md)。

---

## 1. 项目概述

TodoList 是一个基于 Flutter 的跨平台 Todo / Memo 应用。

本阶段目标是在现有应用基础上进行架构升级，形成完整的：

> **Todo + Memo + Desktop Widget + Android Widget + Pluggable Sync**

整体产品：

```text
                         TodoList
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
     Windows              Android             Sync
        │                   │                   │
 Desktop Widget        Home Screen Widget       │
        │                   │                   │
        └───────────────────┼───────────────────┘
                            │
                       Local First
                            │
                       Sync Manager
                            │
             ┌──────────────┼──────────────┐
             │              │              │
           WebDAV          OSS       Self-hosted Server
```

---

## 2. 产品目标

让 Todo / Memo 成为用户在 Windows 和 Android 设备上**随时可见、随时可操作的信息入口**：

```text
设备桌面 → 直接看到 Todo → 快速完成 → 快速添加 → 需要复杂操作时进入 App
```

## 3. 产品定位

不是重新开发一个 Todo 软件。核心是：

> **在现有 TodoList 基础上建立统一的数据层、Widget 层和可插拔同步架构。**

## 4. 平台范围

- **Windows**：Flutter Todo App + Desktop Widget + 本地数据 + 同步
- **Android**：Flutter Todo App + Home Screen Widget + 本地数据 + 同步
- **Server**：用户认证 + 设备管理 + Todo/Memo 同步 + 增量同步 + 冲突处理

## 5. 同步方式

客户端支持三种同步方式，设置中可切换：

```text
○ 不同步   ○ WebDAV   ○ OSS / S3   ○ 自建服务器
```

> 三种同步方式都是 Sync Provider，而不是三套 Todo 业务逻辑。

## 6. Sync Provider 架构 🟢

```text
SyncManager
     ├── WebDAVProvider
     ├── OSSProvider
     └── ServerProvider
```

Todo 核心业务不关心具体同步方式：

```text
TodoRepository → Local Database → SyncManager → {WebDAV | OSS | Server}
```

## 7. WebDAV

WebDAV 支持坚果云、Nextcloud、群晖等**标准 WebDAV 服务**。配置项：

```text
Server URL / Username / Password / Remote Path / [测试连接] / [立即同步]
```

> ⚠️ Provider 命名必须为 **WebDAV**，不得写死为"坚果云"（坚果云只能作为 UI 示例文案出现）。

## 8. OSS / S3

统一抽象为 **OSS / S3 Compatible**：阿里云 OSS、腾讯云 COS、AWS S3、Cloudflare R2、MinIO 等。配置项：Endpoint / Bucket / Access Key / Secret Key / Remote Path。

## 9-10. 自建服务器定位

自建服务器是独立项目（FastAPI + PostgreSQL + JWT + Docker + Nginx/Caddy），只负责：

> **身份 + 数据存储 + 多设备同步。**

不负责 Flutter UI、Widget UI、客户端交互。未来支持 Docker 一键部署，用户填 Server URL 即可同步。

## 11. 本地优先 🟢

所有平台必须 Local First：

```text
用户操作 → Local Database → UI 立即更新 → 后台 Sync → Remote
```

网络断开仍可正常使用；恢复后 Local Changes → Sync → Remote。本地数据同步失败必须保留，下次继续。

## 12-13. Windows Widget（两阶段）

V1 — 普通 Widget Window：

```text
frameless / transparent / resizable / draggable
位置与尺寸持久化 / Todo 显示与完成 / 添加 Todo / 打开 App / 透明度 / 置顶（可选，不得强制）/ 开机启动
```

V2 — 真正 Desktop Layer（Progman / WorkerW `SetParent`）。
**Desktop Layer 不稳定时必须保留普通 Widget Window 作为 fallback。**

## 14-15. Android Widget

原生 `AppWidgetProvider + RemoteViews + PendingIntent`，**不得依赖 Flutter Engine 长时间运行**。数据经 Widget Snapshot（Flutter App → Snapshot → Widget）。

至少支持 **2×2 / 4×2 / 4×4**。V1 功能范围：查看 Todo、完成 Todo、添加 Todo、打开 App；不要求完整 Todo 管理。

## 16. Memo

Memo 与 Todo 使用统一数据体系。未来支持 Todo Widget / Memo Widget / 混合 Widget；第一版 Widget 只做 Todo。

## 17. 数据模型

核心实体：`User / Device / Todo / Memo / WidgetSettings / SyncState / SyncOperation`

Todo 字段：

```text
id / uuid(全局标识) / userId / title / description / completed
priority / dueAt / createdAt / updatedAt / deletedAt / deviceId
```

## 18. 软删除

**禁止直接删除远端记录**，使用 `deletedAt` 墓碑：

```text
Windows 删除 → 墓碑同步 → Android Pull → Android 删除本地显示
```

Server 确认所有设备完成同步后才允许垃圾回收。

## 19. 冲突策略

V1 = **Last Write Wins**：`updatedAt` 较新者胜出，`deviceId` 作为并列时的确定性决胜。暂不实现 CRDT / OT / 多人协作。

## 20. 项目结构

```text
todo/
├── lib/
│   ├── core/       # database / sync / models / repositories
│   ├── features/   # todo / memo / settings
│   └── widget/
├── android/  windows/
todo-server/
├── app/ (api / models / services / auth / database)
├── tests/  Dockerfile  docker-compose.yml
```

> 现有工程使用 `lib/{models,data,state,sync,pages,desktop}` 的等价结构，
> 属于上述结构的既成实现（映射见 ARCHITECTURE.md），**允许保留，不强制目录改名**。

---

# SPEC — 技术规格

## 1. Client Architecture

```text
UI → State Management → Repository ─┬→ Local DB
                                    └→ Sync Manager ─→ {WebDAV, OSS, Server}
```

## 2. Local Database

现有数据库（SQLite/sqflite）**优先复用，未经确认禁止更换**（Hive/Isar/Drift 等）。当前实现满足要求 → 保留。

## 3. Sync Provider Interface

```dart
abstract class SyncProvider {
  Future<SyncResult> push();
  Future<SyncResult> pull();
  Future<SyncResult> sync();
  Future<bool> testConnection();
}
```

具体实现：`WebDAVSyncProvider / OssSyncProvider / ServerSyncProvider`。

## 4. Sync Manager

成员：`currentProvider / sync() / push() / pull() / testConnection() / syncStatus`
状态：`idle / syncing / success / failed / offline`

## 5. Server API（第一版）

```text
POST /api/v1/auth/register | login | refresh
GET  /api/v1/devices        POST /api/v1/devices        DELETE /api/v1/devices/{id}
POST /api/v1/sync/push      GET  /api/v1/sync/pull
GET  /api/v1/todos          GET  /api/v1/memos
```

实际同步通过 `/sync/push` + `/sync/pull` 完成。

## 6-7. Sync Push / Pull

Push 请求：`{ deviceId, changes: [{entity, id, operation, data, updatedAt}] }`
服务器流程：验证身份 → 验证 Device → 处理 Changes → 冲突检测(LWW) → 保存 → 返回结果。

Pull 请求：`GET /api/v1/sync/pull?cursor=xxx`
响应：`{ cursor, changes }`。客户端：写 Local DB → 更新 cursor → 刷新 UI → 刷新 Widget。

## 8. Cursor

必须支持增量同步：第一次 Full Sync，之后经 cursor 只拉变化，**不得每次全量下载**。

## 9. Device

每个客户端生成 `deviceId`（如 `windows-xxxx` / `android-xxxx`）。Server 保存：`deviceId / userId / platform / deviceName / lastSyncAt / createdAt`。

## 10. Widget Data Flow

```text
TodoRepository → Local DB → Widget Data Provider → {Windows Widget, Android Widget}
```

**Widget 禁止自己维护 Todo 数据库。**

## 11. Windows Native API

`createWidget / destroyWidget / showWidget / hideWidget / setPosition / getPosition /
setSize / getSize / setOpacity / setAlwaysOnTop / attachToDesktop / detachFromDesktop`

## 12. Android Native API

核心：`AppWidgetProvider / RemoteViews / PendingIntent / AppWidgetManager`
Action：`TOGGLE_TODO / OPEN_TODO / ADD_TODO / OPEN_APP`

## 13. Widget Snapshot

```json
{ "updatedAt": 1787923000, "todos": [{ "id": "1", "title": "修改简历", "completed": false }] }
```

## 14. Settings

```text
General: Theme / Language
Sync: Provider / WebDAV / OSS / Server / Sync Now / Sync Status
Windows Widget: Enable / Position / Size / Opacity / Always On Top / Lock Position / Start With Windows
Android Widget: Display Mode / Max Items / Show Completed
```

## 15. 安全要求

Server：HTTPS / JWT / Password Hash（bcrypt 等）；禁止明文保存密码、明文传输密码、日志输出 Token/Secret。
OSS 的 Access Key / Secret Key 不得进入 Git、GitHub、日志、源码仓库。

## 16. Docker 部署

Server 必须支持 `docker compose up -d`：Caddy/Nginx → FastAPI → PostgreSQL。

## 17. 非功能要求

- 性能：Widget 空闲时低 CPU/内存，无高频轮询
- 稳定性：任何网络/服务端故障不得影响本地 Todo 使用
- 数据安全：本地优先保存；同步失败时 Local Data 与 Sync Operation 保留，下次继续

## 18. 开发阶段

`Phase 0 审计 → 1 统一数据/SyncProvider → 2 Windows Widget → 3 Windows Desktop Layer → 4 Android Widget → 5 Sync Server → 6 WebDAV → 7 OSS → 8 跨设备一致性 → 9 打包发布`
（原 SPD 的 8 阶段展开为 0-9，映射见 PHASES.md。）

## 19. 最重要的开发约束（禁止事项）

1. 禁止重写整个项目
2. 禁止擅自更换数据库
3. 禁止建立第二套 Todo 数据
4. 禁止 Widget 使用 Mock Todo
5. 禁止为每种同步方式复制 Todo 业务逻辑
6. 禁止把 Android Widget 做成持续运行的 Flutter 页面
7. 禁止把 Windows Widget 强制成 Always On Top（只能是可选项）
8. 禁止在没有测试的情况下修改核心同步逻辑
9. 禁止提交任何密码、Token、Access Key、Secret Key
10. 禁止一次性修改整个项目（按 Phase 推进）

## 20. 每个 Phase 完成后必须输出

修改文件列表 / 修改原因 / 架构变化 / 测试结果 / 已知问题 / 下一阶段计划 → 记录于 [PHASES.md](PHASES.md)。
