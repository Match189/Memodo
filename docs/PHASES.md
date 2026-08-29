# PHASES — 阶段计划与完工记录

> SPD §18/§20 要求：按阶段推进，每阶段完成后在本文件记录
> 修改文件 / 原因 / 架构变化 / 测试结果 / 已知问题 / 下一步。

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| Phase 0 | 项目审计 | ✅ [ARCHITECTURE.md](ARCHITECTURE.md) |
| Phase 1 | 统一数据模型 v3 + SyncProvider/SyncManager + offline | ✅ 见下 |
| Phase 2 | Windows Widget V1（缩放/记忆/透明度/打开App/可选置顶） | ✅ 见下 |
| Phase 3 | Windows Desktop Layer（Progman/WorkerW）+ fallback | ✅ 见下 |
| Phase 4 | Android Widget 交互与多尺寸 | ✅ 见下 |
| Phase 5 | FastAPI 自建同步服务器（JWT/cursor/Docker） | ✅ 见下 |
| Phase 6 | Server 通道接入新协议 + Provider 命名规范 | ✅ 见下 |
| Phase 7 | 跨设备一致性验证 | ✅ 见下 |
| Phase 8 | 双端打包 + 文档收尾 | ✅ 见下 |

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

---

## Phase 2 完工记录（Windows Widget V1）

**修改**：`lib/desktop/win32_window_style.dart`（保留 WS_THICKFRAME → 无边框且可缩放；GetWindowRect 采样；SetWindowCompositionAttribute 透明度，手工 FFI 绑定；打开主窗口）、`widget_launcher.dart`（位置/尺寸记忆恢复 + 5s 周期采样保存 + 按字段响应设置变化）、`widget_settings.dart`（opacity/lockPosition/posX-Y-w-h）、`widget_window_page.dart`（透明度背景、锁定禁拖、打开主窗口按钮）、设置页（透明度滑杆/锁定/置顶默认关）。

**测试**：22 通过；analyze 0 error。**已知问题**：真·逐像素透明依赖窗口 accent（内容层半透明叠加实现，视觉近似）；置顶按 SPD 改为可选默认关。

## Phase 3 完工记录（Desktop Layer）

**修改**：`win32_window_style.dart`（attachToDesktop：Progman 发 `0x052C` 生成 WorkerW → 定位 SHELLDLL_DefView 之后的 WorkerW → SetParent；detachFromDesktop）、`widget_launcher.dart`（attach 状态跟随与失败回退）、`widget_settings.dart`（attachToDesktop 字段）、设置页"桌面层模式（实验）"。

**测试**：22 通过；失败自动回退普通窗口并复位开关（SPD §13 fallback 要求）。**已知问题**：桌面层模式下拖动/缩放行为依赖 Explorer，属实验特性。

## Phase 4 完工记录（Android Widget 交互）

**修改**：`TodayWidgetProvider.kt`（onReceive 处理 `TOGGLE_TODO` 广播 → 原生直写 SQLite → refreshAll；列表 PendingIntentTemplate + fill-in uuid）、`TodayWidgetService.kt`（行级 uuid + showCompleted/maxItems 过滤）、`home_widget_bridge.dart`（推送 db_path/uuid/显示设置）、`android_widget_settings.dart`（新）、设置页（最大条数/显示已完成）、main.dart 装配。

**架构**：小组件不依赖 Flutter 引擎常驻；勾选原生直写本地库，`updated_at` 推进使 LWW 在下次同步自然传播。**测试**：23 通过（载荷 2 例）。**已知问题**：原生勾选后正在运行的 App 端不即时感知（打开/同步时收敛）；ADD_TODO 以"打开应用并聚焦输入"形式实现（RemoteViews 无文本输入）。

## Phase 5 完工记录（FastAPI 同步服务器）

**新增**：`todo-server/`（app/{main,config,db,models,schemas,security,deps}.py + routers/{auth,devices,sync,data}.py、tests/×3、Dockerfile、docker-compose.yml、.env.example、README.md）。协议 = SPD §5-§9：JWT（access/refresh）、设备注册与心跳、`/sync/push`（逐条 LWW，rejected: stale）、`/sync/pull?cursor`（server_seq 单调序列增量分页）、软删除墓碑。

**测试**：pytest 6/6（注册/登录/刷新/401 保护/往返/LWW 拒绝旧版/墓碑+cursor 增量/设备自动注册）。**已知问题**：旧 `server/`（Dart shelf + 快照协议）已被本实现取代并删除；pull 的设备心跳暂记为 pull-caller 待改为携带 deviceId 的查询参数。

## Phase 6 完工记录（客户端接入）

**修改**：`lib/sync/server_sync_provider.dart`（新：JWT 登录/refresh 重试、push 全量变更、pull cursor 循环、远端应用带 LWW 守卫——本地离线新修改不被覆盖）、`sync_settings_model.dart`（ServerConfig 增 username/password/accessToken/refreshToken/cursor；通道命名规范化 WebDAV / OSS / S3 Compatible）、`sync_manager.dart`（server 通道走新 Provider）、设置页表单（邮箱+密码）。旧 `server/`、`server_transport.dart`、静态 token 协议移除。

**测试**：21 通过（ServerTransport 组测试随旧协议移除，E2E 在 Phase 7 补真栈验证）。

## Phase 7 完工记录（跨设备一致性 E2E）

**新增**：`test/e2e_server_test.dart`——真栈：测试内启动 uvicorn（SQLite，临时目录独立库），两台"设备"（独立 SQLite 库，deviceId 不同）走完整协议。验证：Windows→Android 传播、Android 勾选→Windows 传播、Windows 离线新增→Android、Windows 删除墓碑→Android 收敛、最终两端可见集合完全一致。

**测试**：E2E 1/1（~2s）+ 全套 22/22。**已知问题**：测试依赖 `todo-server/.venv`（缺失自动跳过）；曾出现 uvicorn 启动竞态导致的偶发挂起，已通过独立服务器库 + 干净进程管理消除。

## Phase 8 完工记录（打包与文档）

- `flutter build windows --release` ✅（`build/windows/x64/runner/Release/` 整文件夹分发）
- `flutter build apk --release` ✅（55M+，`build/app/outputs/flutter-apk/app-release.apk`，含 INTERNET 权限与小组件）
- README 更新（SPD 对齐）；本文件收尾；git 全程逐阶段提交，凭据文件未入库（已验证）

---

# Board 图钉板（v2 架构扩展）阶段记录

按用户 Pinboard 实施规格（docs/BOARD.md）分阶段执行。

## Board Phase 1-2 完工（审计 + BoardTheme）

- Phase 1 审计：见 ARCHITECTURE.md（Provider/SQLite v3/桌面 FFI 集成等已确认复用）
- `lib/board/board_theme.dart`：BoardThemeData + 软木板/毛玻璃主题（各含深浅态）
- 关键裁定：**Card 只引用实体 uuid（ref_type+ref_uuid），不复制内容**（SPD 禁止 #2/#3）

## Board Phase 3-4 完工（背景渲染）

- `lib/board/board_background.dart`：软木板=底色渐变+固定种子噪点+暗角（无图片资产）；
  毛玻璃=整板**唯一一层** BackdropFilter（规格 §11 禁止每卡 Blur）

## Board Phase 5-6 完工（PinWidget / BaseCard / 内容卡）

- `lib/board/pin_widget.dart`：CustomPaint 图钉（钉帽径向渐变+高光+投影+针杆），全本地渲染
- `lib/board/base_card.dart`：BaseCard（纸面+图钉手柄+右下缩放手柄+阴影三态）
- `lib/board/cards.dart`：TodoCardContent / MemoCardContent

## Board Phase 7 完工（交互与持久化）

- `lib/board/board_controller.dart`：BoardCardView（每卡独立 ValueNotifier，拖动不全局 rebuild）、
  dragBy/resizeBy（内存）、endGesture（吸附 8px + 落盘）、bringToFront（严格置顶）、
  布局持久化到 settings kv（key=board.layout.<boardId>，本机视觉状态不进同步协议）
- `lib/data/app_database.dart`：**DB v4**（boards/cards 表，Card 引用式模型）
- `lib/data/board_repository.dart`：默认板幂等创建、pin 去重、unpin 软删除墓碑

## Board Phase 8a 完工（应用内图钉板页）

- `lib/pages/board_page.dart`：第 4 个导航页——软木板/毛玻璃切换、网格吸附开关、
  钉待办/钉备忘选择器（去重）、拖动/缩放/置顶、源删除占位卡
- 导航加入第 4 目的地（侧栏+底栏）

**测试**：25/25（新增 board_test 3 用例：幂等/去重/软删除、布局持久化往返、z 序递增）。
**已知问题**：布局不跨设备同步（V1 设计，后续进快照 v4）；卡片编辑仍在主页面完成。
**下一阶段**：Phase 8b 小组件窗口 Board 渲染模式 → Phase 9 安卓视觉一致 → Phase 10 同步接入。

## 缺陷修复记录（真机验收）

- **手机启动卡系统启动屏（关键）**：`onConfigure` 里的 `PRAGMA busy_timeout` 在
  安卓平台版 sqflite 上不被 `execute` 支持 → 开库抛异常 → runApp 前死亡。
  Windows FFI 不受影响，因此只在真机暴露。修复：改用 `rawQuery`（双端通用）。
  已 adb 真机验证：release 包零错误、界面完整渲染、用户可继续 WebDAV 配置。
- **经验**：桌面端通过的代码不等于全端通过；涉及平台差异的 sqflite API
  （execute/PRAGMA）必须用 rawQuery/数据库选项表达，并优先真机验证。
