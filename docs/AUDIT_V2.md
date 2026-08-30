# Memodo V2 · Phase 0 架构审计（2026-08-30）

对照 [BLUEPRINT.md](BLUEPRINT.md) 逐项盘点现有代码。标记：✅ 已达成 / 🟨 部分 / ❌ 缺失。
"Win"=`memodo-windows`，"And"=`memodo-android`，"Srv"=`memodo-server`。

## 1. 总体架构

| 蓝图要求 | 现状 | 判定 |
| --- | --- | --- |
| §3 数据同步、UI 不同步 | 双端独立 UI，仅 SQLite 列名对齐 | ✅ |
| §8 Local-first | 双端本地库直读写，无网可用 | ✅ |
| §4 Windows 原生（C#/WPF/SQLite/MVVM） | .NET 10 WPF + Microsoft.Data.Sqlite + MVVM | ✅ |
| §5 Android（Kotlin/Compose/Room/Glance） | 已就位 | ✅ |
| §6 Server 第一版从简 | FastAPI+PG，无多余组件 | ✅ |
| §47 LWW + Tombstone（id/updatedAt/deviceId/deletedAt） | 服务端已实现；客户端 push 含墓碑 | ✅ |
| §48 SyncQueue（离线队列） | 无队列，仅手动"立即同步" | ❌ P1 |
| §49 删除后 GC 清理 | 无 GC | ❌ P2 |

## 2. 数据模型（§1/§9-§12）

| 项 | 现状 | 判定 |
| --- | --- | --- |
| §2 Card 是核心（type: TODO/MEMO/CHECKLIST/IDEA） | cards 仅存 ref_type(todo/memo)+ref_uuid，无 title/content/type | ❌ → Phase 2 折中方案（见 PLAN.md §2） |
| §9 Board(name/description/theme) | boards 表只有 name；无 description/theme | 🟨 |
| §9 Section（视觉分区，x/y/w/h） | sections 表存在但无 UI、无布局列 | 🟨 |
| §10 Todo 的 dueAt/reminderAt/repeat | tasks 有 due_date；无 reminder/repeat | 🟨 |
| §11 CardLayout 与业务分离、按平台 | card_layouts(platform) 已实现 | ✅ |
| §12 DesktopWidget 配置持久化 | SettingsStore JSON（widget 开关/置顶），非 DB 实体 | 🟨（够用，V2 不动） |

## 3. Windows 主程序（§18/§26-§29/§33-§39）

| 项 | 判定 | 备注 |
| --- | --- | --- |
| 主窗口骨架（侧栏+内容区） | ✅ | |
| 无边框可拖动 + 最小化/最大化/关闭 | ✅ | 自绘标题栏（fd6d7dc 修复） |
| 多 Board 侧栏（My Boards 列表） | ❌ | 目前仅一块默认板 |
| Today 页（§27） | ❌ | |
| Inbox（§26） | ❌ | |
| Search（§22 Ctrl+K） | ❌ | |
| Board 无限画布：滚轮缩放/中键平移（§34） | ❌ | 目前固定 1:1 Canvas |
| Card 编辑弹窗（§29：标题/内容/类型/到期/提醒/颜色） | ❌ | |
| Checklist / Idea 类型（§32） | ❌ | |
| Card 颜色（§38，默认 Paper/Cream） | ❌ | |
| Card 旋转限幅 ±2°（§37） | ✅ | Win 旋转手柄自由角度，需加限幅 |
| 卡片微动画（§40） | ❌ | |
| Empty/Error/Loading 态（§68） | 🟨 | Board 有空态文案，列表无 |

## 4. Windows 桌面组件（§19/§20 —— 产品第一核心体验）

P0 清单：

| 项 | 判定 |
| --- | --- |
| 无边框 | ✅ |
| Move（拖标题栏） | ✅ |
| **Resize** | ❌ AllowsTransparency 下无缩放边，需 WindowChrome.ResizeBorderThickness |
| 组件内 Card Drag/Resize/Rotation | ❌（当前是固定双栏列表，非迷你 Board） |
| Todo Complete / Add / Delete | ✅ |
| **Edit** | ❌ |
| **Lock（锁定防误触）** | ❌ |
| Always On Top | ✅（设置项） |
| **Position Persistence**（§20 P0，重启恢复） | ❌ 必须补 |
| Opacity / Cork-Glass / Click-through / 多组件 / 热键（P1） | ❌ |

> 定位差异要拍板：蓝图 §19 的桌面组件是"**迷你 Board**"（卡片自由摆放）；
> 当前实现是"今日待办+备忘双栏列表"。按蓝图走 = Phase 3 把组件改成复用 Board 渲染的迷你画布。

## 5. 托盘 / 快捷键 / Quick Capture（§21/§22/§28）

| 项 | 判定 |
| --- | --- |
| 托盘菜单（Show/Hide Board、New Todo、New Memo、Sync Now、Settings、Exit） | 🟨（有显示主窗/组件/自启/退出；缺 New Todo/Memo、Sync Now、Settings 项） |
| Ctrl+Alt+M 显隐组件 / Ctrl+Alt+N New Todo / Ctrl+K Search | ❌（需 RegisterHotKey，放 Services 层） |
| Quick Capture 弹窗（§28） | ❌ |

## 6. Android（§23/§24/§26/§27）

| 项 | 判定 | 备注 |
| --- | --- | --- |
| App 骨架（Compose + Room） | ✅ | |
| Board = **Adaptive Grid**（§23：Android 不做无限画布） | ❌ | 当前 BoardScreen 是自由坐标画布，需改 Grid + 拖拽排序 |
| Home Widget 快速完成（勾选直写库） | ❌ | 当前只读展示 |
| Widget 尺寸 2x2/4x2/4x4（§24） | ❌ | 单一布局 |
| Today / Inbox / Search | ❌ | |

## 7. 同步矩阵（§41-§50）

| Provider | Win | And | Srv |
| --- | --- | --- | --- |
| Local Only | ✅ | ✅ | — |
| WebDAV（§43） | ❌ | ❌ | — |
| OSS/S3（§44，写通用 S3Provider） | ❌ | ❌ | — |
| Server（§45） | 🟨 tasks/memos 已通 | ❌ | ✅ 代码完成未实跑 |
| 同步范围含 Board/Section/Card（§25） | ❌ | ❌ | 协议本身实体无关 ✅ |

服务端缺口：`GET /boards`、`GET /cards`、`/sync/status`（§46）、备份（§46 Backup）、pytest。

## 8. 设置（§51/§52/§53）

| 分组 | 判定 |
| --- | --- |
| Appearance（Cork/Glass/Hybrid、Dark、Card Style、Animation） | ❌（仅色板，无主题切换） |
| Desktop（自启✅、Show Widget✅、AlwaysOnTop✅、Opacity/Lock/ClickThrough/默认尺寸） | 🟨 |
| Sync（Provider 选择、Sync Now✅、Last Sync、Conflict） | 🟨 |
| Notification（提醒/每日汇总/逾期） | ❌ |
| Data（Export JSON / Import / Backup / Clear） | ❌（§52 第一版必须有 Export JSON） |
| 安全：不落明文密码 | ✅（密码仅在内存，未持久化） |

## 9. 视觉系统（§13-§17/§62）

| 层 | 判定 |
| --- | --- |
| Cork 软木底 | 🟨（纯色 #D9B38C，无质感/噪点/暗角） |
| Glass 玻璃 | 🟨（主窗 Mica✅；组件是实色浅底非玻璃） |
| Paper 纸卡 | 🟨（白卡圆角；无纸纹/轻旋转默认值） |
| **Pin 图钉** | ❌ 品牌元素完全缺失 |
| 三主题 Cork/Glass/Hybrid | ❌ |
| Dark Mode（§39：Dark Glass + Muted Paper） | ❌ |

## 10. 仓库结构（§67）

现状 `memodo-windows/ memodo-android/ memodo-server/ docs/` ≈ 蓝图建议布局。
遗留 Flutter（`lib/ android/ windows/ pubspec.* todo-server/`）待 Phase 10 清理归档。

## 11. 审计结论（优先级排序）

1. **P0**：桌面组件补 P0（位置记忆、Resize、Lock、Edit）→ 蓝图 §64 也把它排在前
2. **P0**：Pin + 三主题 + Dark（设计系统是"丑"的根因）
3. **P0**：Android Board 改 Adaptive Grid（形态错误，越晚改代价越大）
4. **P1**：Card 模型扩展（type/内联内容/颜色）+ Board 缩放平移 + 编辑弹窗
5. **P1**：服务端实跑验证 + pytest；Android 接入 Server 同步
6. **P2**：Inbox/Today/Search/QuickCapture/热键、WebDAV/OSS、Export JSON
