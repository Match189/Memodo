# TodoList — Cross-platform Todo & Memo with Desktop/Mobile Widgets and Pluggable Sync

跨平台（Windows 桌面 + Android 手机）的待办清单与备忘录应用，Flutter 一套代码构建。
开发标准见 [docs/SPD.md](docs/SPD.md)，架构审计见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)，
中文名「念念」（念念不忘，必有回响）。阶段完工记录见 [docs/PHASES.md](docs/PHASES.md)；滚动计划见 [docs/ROADMAP.md](docs/ROADMAP.md)；多用户开放方案见 [docs/MULTIUSER.md](docs/MULTIUSER.md)。

## 当前功能

- **待办**：新增、勾选完成、点击改标题、删除、一键清除已完成
- **备忘**：新建、编辑、删除，卡片式网格浏览（宽度自适应）
- **本地优先（Local First）**：SQLite 单一事实来源；断网完全可用，恢复后继续同步
- **多端同步**：三条通道任选，设置页随时切换（SPD §5-§6 SyncProvider 架构）
  - **WebDAV**（坚果云/Nextcloud/群晖等标准服务）
  - **OSS / S3 Compatible**（阿里云 OSS、腾讯云 COS、AWS S3、Cloudflare R2、MinIO）
  - **自建服务器**（[todo-server/](todo-server/README.md)：FastAPI + PostgreSQL + JWT + cursor 增量 + Docker）
  - 合并策略：按 uuid + updatedAt 的 Last Write Wins，deviceId 平局决胜；软删除墓碑
  - 可选 AES-256-GCM 加密（WebDAV/OSS 快照）
  - 状态机：idle / syncing / success / failed / **offline**（断网不影响本地使用）
- **Windows 桌面小组件**：无边框可缩放卡片——勾选、快速添加、拖动、位置尺寸记忆、
  透明度、可选置顶、锁定位置、桌面层模式（实验，Progman/WorkerW + 自动回退）、打开主窗口
- **安卓桌面小组件**：2×2~4×4 可拉伸；应用变化自动推送；卡片上直接勾选（原生直写）；
  最大条数/显示已完成可配；"添加到桌面"快捷入口
- **自建服务器**：用户注册/登录、设备管理、增量同步（cursor）、软删除墓碑、
  `docker compose up -d` 一键部署
- 自适应界面（宽屏侧边导航 / 窄屏底部导航）、中文界面、深浅色主题

## 环境要求

| 工具 | 说明 |
| --- | --- |
| Flutter SDK | `D:\flutter`（3.47.2 stable），已加入用户 PATH |
| Visual Studio | VS 2019 Community（C++ 桌面开发负载），用于 Windows 构建 |
| Android SDK | `D:\android-sdk`（缺什么 Gradle 自动补装） |
| JDK | `D:\jdk-17`（Temurin 17，已 `flutter config --jdk-dir` 指定） |
| Gradle 代理 | `~\.gradle\gradle.properties` 指向本地代理 127.0.0.1:7897 |
| Python（服务器开发） | `todo-server/.venv`（uv 创建，见 todo-server/README.md） |
| Windows 开发者模式 | 已开启（构建带插件的 Windows 应用需要） |

## 常用命令

```bash
# Windows 上运行/调试
flutter run -d windows

# 打 Windows 发布包（产物在 build/windows/x64/runner/Release/）
flutter build windows --release

# 安卓：连接手机或启动模拟器后
flutter devices
flutter run            # 自动选择可用设备
flutter build apk --release   # 产物在 build/app/outputs/flutter-apk/

# 测试与静态检查
flutter test
flutter analyze
```

网络提示：本机走本地代理（127.0.0.1:7897）访问 pub.dev / Google Maven 更快。
Gradle 的代理已配置好；命令行直接用 flutter/dart 时若下载慢，在 PowerShell 先执行：

```powershell
$env:HTTPS_PROXY = "http://127.0.0.1:7897"
```

## 代码结构

```
lib/
├── main.dart               # 入口：主窗口装配 + 小组件子窗口分发（multi_window 参数）
├── models/
│   ├── task.dart           # 任务模型（uuid 全局标识 + deleted 软删除墓碑 + 时间戳）
│   └── memo.dart           # 备忘模型（同上）
├── data/
│   ├── app_database.dart   # 建库建表 + v1→v2 迁移（回填 uuid）+ 并发 busy_timeout
│   ├── settings_store.dart # settings 键值表（存同步配置、小组件开关）
│   ├── task_repository.dart
│   └── memo_repository.dart
├── state/
│   ├── task_list_model.dart
│   └── memo_list_model.dart
├── desktop/
│   ├── widget_launcher.dart    # 主窗口侧：创建/关闭小组件窗口，跟随设置
│   ├── widget_settings.dart    # 小组件开关与置顶设置
│   └── win32_window_style.dart # user32 加工：去边框、置顶、右下角定位、拖拽
├── home_widget_bridge.dart # 安卓小组件数据推送（home_widget 共享存储）
├── sync/
│   ├── snapshot_codec.dart     # 快照 JSON 编解码 + 可选 AES-GCM 加密（PBKDF2 派生密钥）
│   ├── sync_transport.dart     # 传输抽象接口
│   ├── sync_engine.dart        # 拉取→按 uuid 合并(LWW)→落库→回传
│   ├── sync_settings_model.dart# 通道配置、口令、自动同步开关
│   └── transports/
│       ├── webdav_transport.dart  # 任意 WebDAV（坚果云等）
│       ├── oss_transport.dart     # 阿里云 OSS / 腾讯云 COS（手写 V1 签名）
│       └── server_transport.dart  # 自建服务器
└── pages/
    ├── home_page.dart      # 自适应导航壳（待办 / 备忘 / 设置）
    ├── tasks_page.dart
    ├── memos_page.dart
    ├── memo_edit_page.dart
    ├── settings_page.dart  # 同步通道、桌面小组件开关
    └── widget_window_page.dart # Windows 小组件子窗口的界面
server/                     # 自建同步服务器参考实现（独立 Dart 包，见其 README）
tool/                       # 配置写入/状态查看的本地小工具
test/                       # 数据层、合并、加密、传输、小组件载荷共 19 个测试
```

## 同步使用说明

1. 两端装好应用后，在「设置」页选同一个通道，填同样的配置和加密口令（若启用）。
2. **坚果云**：注册后在网页端「账户信息 → 安全选项」添加**应用密码**（不要用登录密码），
   服务地址填 `https://dav.jianguoyun.com/dav/`，账户填注册邮箱。
3. 先点「测试连接」确认通畅，再「立即同步」。之后改动会自动同步。
4. 同步是全量快照 + 按条合并，个人使用量级是每次几十 KB，坚果云免费额度绰绰有余。

## 后续路线图（可选）

1. 系统托盘（最小化到托盘、快捷菜单）与 Windows 开机自启
2. 任务提醒通知（数据模型已含 dueAt/priority）
3. 小组件逐像素透明背景（当前为窗口 accent + 内容层半透明叠加）
4. 安卓小组件"添加待办"输入框（RemoteViews 文本输入限制，现为打开应用快捷方式）
5. 服务器 Alembic 迁移与多用户并发优化
6. 发布准备：替换 `com.example` 包名、应用图标、签名

## 已知限制

- WebDAV/OSS 通道为整包快照 + LWW（自建服务器通道已是 cursor 增量）；个人使用足够
- 加密口令只存在本地设置里，忘了无法解开云端快照（重新同步会覆盖）
- Windows 窗口标题暂为 "todolist"（Runner 模板限制）
- 自建服务器公网部署务必加 HTTPS（见 todo-server/README.md）
