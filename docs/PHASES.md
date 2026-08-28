# PHASES — 阶段计划与完工记录

> SPD §18/§20 要求：按阶段推进，每阶段完成后在本文件记录
> 修改文件 / 原因 / 架构变化 / 测试结果 / 已知问题 / 下一步。

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| Phase 0 | 项目审计 | ✅ [ARCHITECTURE.md](ARCHITECTURE.md) |
| Phase 1 | 统一数据模型 v3 + SyncProvider/SyncManager + offline | ✅ 见下 |
| Phase 2 | Windows Widget V1（缩放/记忆/透明度/打开App/可选置顶） | ⬜ |
| Phase 3 | Windows Desktop Layer（Progman/WorkerW）+ fallback | ⬜ |
| Phase 4 | Android Widget 交互与多尺寸 | ⬜ |
| Phase 5 | FastAPI 自建同步服务器（JWT/cursor/Docker） | ⬜ |
| Phase 6 | Server 通道接入新协议 + Provider 命名规范 | ⬜ |
| Phase 7 | 跨设备一致性验证 | ⬜ |
| Phase 8 | 双端打包 + 文档收尾 | ⬜ |

（各阶段完工记录在下方按时间追加。）

---

## Phase 1 完工记录（统一数据 / Sync Provider）

**修改文件**
- `lib/models/task.dart`、`lib/models/memo.dart`：+description/priority/dueAt/deletedAt/deviceId（SPD §17），软删除墓碑改为时间戳（§18）
- `lib/data/app_database.dart`：**DB v3**（新列迁移 + 布尔墓碑→时间戳回填）
- `lib/data/device_identity.dart`（新）：本机 deviceId（`windows-xxxxxxxx`），存 settings 表
- `lib/data/task_repository.dart`、`memo_repository.dart`：注入 deviceId；软删除写墓碑；v3 upsert
- `lib/sync/merge.dart`（新）：LWW 纯函数库；平局用 deviceId 字典序决胜（§19）
- `lib/sync/snapshot_codec.dart`：快照 **format 2**（新字段 + device 元信息，兼容读 v1）
- `lib/sync/sync_provider.dart`（新）：`SyncProvider`/`SyncContext`/`SyncResult` 抽象 + `SnapshotSyncProvider`（WebDAV/OSS 共用）
- `lib/sync/sync_manager.dart`（新，替代 sync_engine.dart）：状态机 `idle/syncing/success/failed/offline`（§4），委托 Provider 执行协议
- `lib/sync/sync_transport.dart`：新增 `isNetworkError`（offline 判定）
- `lib/main.dart`、`lib/pages/settings_page.dart`：装配 deviceId、SyncManager、offline 文案
- `test/migration_test.dart`（新）、`test/sync_logic_test.dart`（重写适配）

**原因**：SPD Phase 1 要求统一数据模型与 SyncProvider 抽象，且冲突策略需要 deviceId。

**架构变化**：SyncManager 只管状态与调度，协议下沉到 Provider；为 Phase 6 的
ServerSyncProvider（cursor 协议）留好了插入点。

**测试结果**：22/22 通过（迁移回归 ×2、合并 ×4、编解码 ×2、整链路 ×3、传输 ×4、
载荷 ×1、仓储 ×6）；`flutter analyze` 无 error/warning；真实库副本手动迁移演练通过。

**已知问题**：14 条 info 级风格提示（initializing formal 等），不影响功能，Phase 8 收尾处理。

**下一步**：Phase 2 — Windows Widget V1 补全（可缩放/位置尺寸记忆/透明度/打开 App/置顶改可选）。
