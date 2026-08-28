# Phase 0 — 项目审计报告（现有架构）

> 审计时间：2026-08-29 凌晨（SPD 升级开工前）。本报告是 SPD Phase 0 的交付物，
> 回答 SPD 要求的 8 个问题，并给出"可复用 / 需扩展 / 不可动"清单。

## 1. 数据库是什么

**SQLite**，经 `sqflite` 抽象访问：

- Android：平台内置实现
- Windows/Linux/macOS：`sqflite_common_ffi`（FFI 驱动，`sqlite3_flutter_libs` 提供原生库）
- 库文件位置：`getApplicationSupportDirectory()/todolist.db`
  （Windows 实际路径 `%APPDATA%\com.example\todolist\todolist.db`）
- 版本：**v2**（v1→v2 迁移已实现：补 `uuid`、`deleted` 列并回填 uuid）
- 附加：`settings` 键值表（同步配置、小组件开关）；`PRAGMA busy_timeout=3000`
  支持主窗口 + 小组件子窗口双引擎并发

**结论：满足 SPD 要求，保留（禁止事项 #2）。**

## 2. Todo 数据在哪里

- 表 `tasks`：`id, uuid, title, done, created_at, updated_at, deleted`
- 访问层：`lib/data/task_repository.dart`（软删除、按 uuid upsert、`listForSync` 含墓碑）
- UI 状态：`lib/state/task_list_model.dart`（ChangeNotifier + provider）

## 3. Memo 数据在哪里

- 表 `memos`：`id, uuid, title, content, created_at, updated_at, deleted`
- 访问层：`lib/data/memo_repository.dart`；状态：`lib/state/memo_list_model.dart`
- 与 Todo 完全同一套体系（同一库、同一同步管线）——满足 SPD §16

## 4. Sync 如何实现

`lib/sync/`：

- `snapshot_codec.dart`：快照 = 全量 JSON（含墓碑），可选 AES-256-GCM 加密
  （PBKDF2-HMAC-SHA256 50k 迭代派生密钥，格式前缀 `TODOLIST-ENC1:`）
- `sync_transport.dart`：`SyncTransport` 接口（fetchSnapshot / uploadSnapshot / testConnection）
- `transports/webdav_transport.dart`：标准 WebDAV（MKCOL/PROPFIND/GET/PUT，Basic Auth），
  未绑定坚果云（坚果云只是默认示例地址）——满足 SPD §7
- `transports/oss_transport.dart`：OSS V1 签名（HMAC-SHA1）PUT/GET，Endpoint/Bucket/Key 全配置化，
  可对接 S3 风格服务——满足 SPD §8 的 V1
- `transports/server_transport.dart`：自建服务器（Bearer Token + GET/PUT /snapshot）
- `sync_engine.dart`：pull → 按 uuid 合并（updatedAt LWW，时间戳相等保留本地）→ 差异落库 →
  回传合并结果（内容指纹相同则跳过上传）；防抖自动同步；测试注入点 `transportOverride`
- 已实测：坚果云真实账号端到端同步成功

**与 SPD 的差距**：无 `SyncProvider/SyncManager` 命名抽象（§3/§4）、无 offline 状态、
无 cursor 增量（快照为全量）、模型缺 `deviceId/deletedAt/description/priority/dueAt`（§17/18/19）。

## 5. 当前状态管理是什么

`provider` 包 + `ChangeNotifier`（TaskListModel / MemoListModel / SyncSettingsModel /
SyncEngine / DesktopWidgetSettingsModel）。轻量够用，**保留**。

## 6. 哪些代码可以复用（升级地基）

| 模块 | 复用方式 |
| --- | --- |
| 数据层 + 迁移机制 | 直接扩展 v3（加列不动旧列） |
| SyncTransport + 三个传输 | 重命名包装为 SyncProvider，传输实现原样保留 |
| SyncEngine 合并逻辑 | LWW 纯函数保留，补 deviceId 决胜 |
| Windows 小组件骨架 | desktop_multi_window 0.2.1 + win32 加工路线保留 |
| 安卓小组件骨架 | home_widget 推送 + RemoteViews 路线保留 |
| 工具 | tool/push_snapshot.dart、tool/read_sync_state.dart 保留 |

## 7. 哪些地方需要扩展

1. **数据模型 v3**：`device_id / deleted_at / description / priority / due_at`
2. **SyncProvider + SyncManager** 命名抽象 + `offline` 状态（SPD §3/§4）
3. **Windows Widget V1 补全**：可缩放（保留 WS_THICKFRAME）、位置/尺寸持久化、透明度、打开 App、
   置顶改为可选默认关（SPD 禁止事项 #7）、桌面层模式（V2）+ fallback
4. **Android Widget 交互**：TOGGLE/ADD 原生直写库（路径由 Flutter 推送）、多尺寸、OPEN_APP/ADD_TODO
5. **自建服务器升级**：现 `server/`（Dart shelf + JSON 文件）→ SPD 要求的独立
   FastAPI + PostgreSQL + JWT + cursor 增量 + Docker 项目 `todo-server/`
6. **ServerSyncProvider**：JWT 登录/刷新、deviceId 注册、push/pull + cursor

## 8. 哪些地方不能修改（SPD 红线）

- SQLite 数据库选型（禁止事项 #2）
- 单一事实来源：Widget 不得建独立 Todo 库（#3/#4）
- 每种同步方式共享同一业务逻辑（#5）
- Android Widget 不得长驻 Flutter 引擎（#6）
- Windows Widget 不得强制置顶（#7）
- 核心同步逻辑的修改必须带测试（#8）
- 任何凭据不得入库（#9，.gitignore 已排除凭据文件并验证）

## 现有工程结构 → SPD §20 的映射

```text
lib/models  ≙ lib/core/models
lib/data    ≙ lib/core/database + lib/core/repositories
lib/sync    ≙ lib/core/sync
lib/pages   ≙ lib/features/{todo, memo, settings}
lib/desktop + lib/pages/widget_window_page.dart ≙ lib/widget
```
