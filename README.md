# Memodo 念念 — Native Cross-platform Todo & Memo with Widgets and Pluggable Sync

跨平台待办与备忘应用（Windows 桌面 + Android 手机 + 自建同步服务器）。
**2026-08 起已放弃 Flutter 跨端 UI 共享，改为原生双客户端**（评估与方案见 [docs/MIGRATION.md](docs/MIGRATION.md)）。
中文名「念念」（念念不忘，必有回响）。

## 仓库结构（现役）

```
memodo-windows/    # Windows 原生客户端：C# / WPF / MVVM / Microsoft.Data.Sqlite
memodo-android/    # Android 原生客户端：Kotlin / Compose / Room / Glance
memodo-server/     # 自建同步服务器：FastAPI（部署版，含 Dockerfile / docker-compose）
todo-server/       # 同步服务器开发版（含 pytest，见其 README）
docs/              # 协议与设计文档（唯一事实来源）：BLUEPRINT / SPD / BOARD / ROADMAP / PHASES ...
locales/           # 共享语言文件源（zh.json / en.json），两端各自复制运行时副本
legacy/flutter/    # 已废弃的 Flutter 代码（仅存档；实现仍在 git 历史中可考）
```

## 客户端功能现状

### memodo-windows（WPF）
- 待办 / 备忘 CRUD，软删除墓碑，uuid 全局标识
- 图钉板（Canvas 自由布局：拖动/缩放/旋转/图钉色×纸色/网格吸附/右键菜单/悬停动画）
- 桌面小组件窗口：板式（可拖动便签）与列表双视图、置顶、锁定、不透明度、背景图、附着桌面（实验）
- 系统托盘（全菜单）、开机自启、关闭到托盘
- 同步：WebDAV 快照（协议 v3）+ 自建服务器（JWT + cursor 增量）；自动同步可配
- 主题：Cork / Glass / Hybrid × 深浅色；双语（zh/en，locales/*.json 热切换）

### memodo-android（Kotlin/Compose）
- 待办 / 备忘 CRUD，软删除墓碑；Room 数据库（表结构逐列对齐 Windows DDL）
- 板页：由未完成待办 + 可见备忘派生的自适应网格（软木背景）
- 3 个 Glance 小组件：待办（快捷勾选）/ 备忘 / 钉板混合卡；尺寸自适应条数上限
- 同步：WebDAV 快照（协议 v3，与 Windows 字节兼容）+ 自建服务器（JWT + cursor）
- 设置：同步通道、小组件条数/显示已完成

### 同步协议要点
- 快照 v3：`{ format, device_id, exported_at, tasks[], memos[] }`，按 `uuid + updated_at` LWW，`device_id` 平局决胜
- 服务器通道：JWT 登录 → push 全量（含墓碑）→ cursor 分页增量拉取
- 板/卡片布局（boards/sections/cards/card_layouts）为**本机视觉状态，不进同步协议**（BLUEPRINT §11）

## 开发环境

| 端 | 要求 |
| --- | --- |
| Windows | .NET 10 SDK（`dotnet build`，工程 memodo-windows/Memodo.Windows.csproj） |
| Android | JDK 17 + Android SDK 35（`gradlew assembleRelease`，工程 memodo-android/） |
| 服务器 | Python + FastAPI（见 todo-server/README.md，uv 管理虚拟环境） |

## 已知待办（截至 2026-08-29）

- 两端均缺：JSON 导出/导入、归档、WebDAV 上传冲突重检（盲写 PUT）、快照加密（AES）
- Android：无自动/后台同步；DB 迁移为 destructive（升级清库）；密码明文存 SharedPreferences
- Android 板页为派生网格，boards/cards 表尚无 UI 调用（待与 Windows Canvas 对齐）
- 国际化：Windows 已双语；Android 仅中文（~64 处硬编码），待接入 locales
- 详细滚动计划见 [docs/ROADMAP.md](docs/ROADMAP.md)

## 同步使用说明（WebDAV / 坚果云）

1. 两端装好应用后，设置页选同一通道、填同样配置（加密口令启用时须一致）。
2. 坚果云：网页端「账户信息 → 安全选项」添加**应用密码**（勿用登录密码），
   地址 `https://dav.jianguoyun.com/dav/`，账户填注册邮箱。
3. 先「测试连接」再「立即同步」。快照 + 按条合并，个人量级每次几十 KB。
4. 自建服务器部署见 [todo-server/README.md](todo-server/README.md)（`docker compose up -d`）。
