# MIGRATION — 架构迁移评估与方案

> 任务书：放弃 Flutter 跨端 UI 共享；Windows/Android 改原生双客户端 + 统一同步协议。
> 评估时间：2026-08-29。

## 1. 现有代码盘点

| 维度 | 现状 | 迁移后命运 |
| --- | --- | --- |
| Flutter 客户端代码 | 6990 行；7 个模块 | 全部废弃（除数据/同步协议文档与脚本） |
| Flutter 测试 | 7 个 dart 测试，约 822 行 | 废弃，移植到 C#/Kotlin 各自重建 |
| 同步服务器（FastAPI + SQLite） | 887 行，含 6 pytest | **完整保留 + 增强**（PostgreSQL + Alembic 迁移） |
| 同步协议设计（`docs/BOARD.md`、`docs/MULTIUSER.md`、`docs/SPD.md`） | 已成型 | 保留，作为协议规范 |
| 同步真机 E2E 脚本 | Flutter 版本，依赖 Flutter 测试框架 | 重写为 Python 端到端（requests + 数据库断言） |
| 用户数据 | 5 条待办 + 3 条备忘（坚果云 WebDAV 也存了一份） | 切到新方案时不动；服务器同步从坚果云取回即可 |

## 2. 迁移策略（按任务书 46 阶段分三批）

### 第一批：协议与服务器（P0，先做）
- PostgreSQL 切换（保留 FastAPI 业务代码）
- Alembic 迁移
- Boards/Sections/Cards 数据模型入服务器（按任务书 §12-15）
- Sync Protocol 增强：增 entityType 增加 board/section/card，cursor 增量，分页（已具备骨架）
- 多用户 M1-lite（邀请码注册，按 `docs/MULTIUSER.md`）

### 第二批：Windows C#/WPF
- 全新工程（不用 Flutter 跨平台代码）
- 复用：server 端 `todo-server/` + 业务文档
- 组件：MVVM + Microsoft.Data.Sqlite + WPF Canvas 图钉板
- 同步：Kotlin/Flutter 之前已稳定使用的**三通道 Provider 协议**（用 C# 重写，逻辑一致）
- 关键镜像点（按任务书 §16-22）：
  - Board 自由布局（Canvas + ZIndex）
  - Cork 板（small tile 噪点纹理）
  - Glass 板（Win10+ 用 Win32 DWM / 旧版降级到半透明边框）
  - PinWidget 矢量绘制
  - Desktop Widget Window（无边框/可缩放/位置记忆）
  - System Tray + 开机自启（HKCU Run）

### 第三批：Android Kotlin/Compose
- 与第二批并行（不同团队/不同目录）
- 复用：server 端、Board 数据模型、Widget 卡片选择逻辑
- 新增：Room、Glance/AppWidget、Compose UI

## 3. 现有 Flutter 代码的去留判断

| 模块 | 命运 | 理由 |
| --- | --- | --- |
| `lib/data/` 仓储、DB 迁移 | 重写 | sqlite-flutter → Room/Microsoft.Data.Sqlite |
| `lib/state/` ChangeNotifier | 废弃 | 改为 ViewModel+StateFlow |
| `lib/sync/` 协议实现 | **抽协议、抽业务，重写** | Provider 接口完全保留（按任务书 §29），但 C#/Kotlin 重写 |
| `lib/pages/` UI | 废弃 | 两端各自原生重写 |
| `lib/desktop/` Windows 原生 | 废弃 | C#/WPF 重写 |
| `lib/home_widget_bridge.dart` 安卓小组件推送 | 重写为 Android 原生端 | 同样推送 db_path/uuid 模式 |
| `lib/board/` 数据模型与控制器 | **设计保留 + 抽到协议** | Card 只引用实体 uuid 等关键裁定已写入 `docs/BOARD.md`，新实现照搬 |
| `lib/theme/` 主题系统 | 借鉴设计 | C#/Kotlin 各自实现 |
| `tool/push_snapshot.dart`、`tool/read_sync_state.dart` | 重写为 Python | 服务器侧可读 |
| `flutter_launcher_icons.yaml` | 废弃 | 各自原生图标 |
| `docs/SPD.md` `docs/PHASES.md` `docs/MULTIUSER.md` `docs/BOARD.md` `docs/ROADMAP.md` `docs/REFERENCE.md` `docs/ARCHITECTURE.md` | **完整保留** | 全部文档是协议与设计的唯一来源 |

## 4. 仓库结构调整（推荐）

保持 `D:\work\todolist` 作为项目根，**新开两个并列子工程**，**不删除** Flutter 代码（作为协议参考 + 回滚保险）：

```text
D:\work\todolist
├── memodo-client/        ← 旧 Flutter 客户端（保留）
├── memodo-windows/       ← 新 C#/WPF 工程
├── memodo-android/       ← 新 Kotlin/Compose 工程
├── memodo-server/        ← 在原 todo-server 改名/升级
├── docs/                 ← 协议与设计文档（保留）
└── .git/                 ← 整体一个仓库（推荐 monorepo）
```

## 5. 数据一致性保证（迁移期关键）

- 迁移期间**服务器切到 PostgreSQL 完成后**，Flutter 客户端的坚果云 WebDAV 仍可继续工作（与新方案解耦）
- 新 C# 客户端首次连接服务器时走**全量同步**（cursor 起始 = 0），旧数据照常拉到
- 用一个 release tag 标记 Flutter 客户端的“最终版本”，后续不再向 Flutter 仓库推功能

## 6. 风险与控制

| 风险 | 缓解 |
| --- | --- |
| Flutter 客户端还在跑（你电脑有数据） | 新客户端用同一个坚果云 WebDAV 账号即可拉回，无须手工迁移 |
| 切换技术栈的工作量被低估 | 严格按任务书 46 阶段（先 P0 协议 + 服务器，再并行 Windows/Android），不跨阶段 |
| 服务器 Postgres 切换可能丢数据 | 双轨：旧服务保留 + 新服务并行运行，导出/导入脚本 |
| 缺少 Android/iOS 测试 | Android 端只能本地真机测；Windows 端 WPF 平台限定需在 Windows 跑 |

## 7. 需要你拍板

1. **C# 工程目录名 / 解决方案结构**：我建议 `memodo-windows/`（WPF .NET 9 单工程 + 一个测试工程），风格上保持轻量
2. **Android 工程目录名 / 包名**：建议 `memodo-android/`，包名沿用 `app.memodo`
3. **是否切 PostgreSQL**：建议切（任务书 §33）。SQLite 也能跑但与"自建服务器公网部署"目标不匹配
4. **是否仍保留 Flutter 客户端作过渡**（推荐保留 1~2 周，等新客户端稳定再卸载）
5. **M1 多用户（邀请码注册）这次一起做**还是留到 Windows/Android 端都稳了再做

## 8. 我建议的开工顺序

1. **本轮 P0**：升级 `todo-server/` 为 FastAPI + PostgreSQL + Alembic，boards/sections/cards 进数据模型 + 协议（数据/协议先行）
2. **下一轮**：开 `memodo-windows/`，C#/WPF 原生 + SQLite 镜像同样的 Todo/Memo；先打通 Local First + 三通道 Sync
3. **再下一轮**：开 `memodo-android/`，Kotlin/Compose + Room + Glance

这样每轮都能独立验收、不阻塞。
