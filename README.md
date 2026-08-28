# 待办备忘 (todolist)

跨平台（Windows 桌面 + Android 手机）的待办清单与备忘录应用，使用 Flutter 一套代码构建。

## 当前功能

- **待办**：新增、勾选完成、点击改标题、删除、一键清除已完成
- **备忘**：新建、编辑、删除，卡片式网格浏览（宽度自适应）
- **本地存储**：SQLite（安卓走平台内置实现，Windows 走 FFI 驱动）
- **多端同步**：三条通道任选，可在设置页随时切换
  - **WebDAV**（坚果云免费账号即可，零成本零运维）
  - **对象存储**（阿里云 OSS / 腾讯云 COS，手写 V1 签名，每月几分钱）
  - **自建服务器**（参考实现在 [server/](server/README.md)，单文件部署）
  - 合并策略：按条目 uuid + updatedAt 的 last-write-wins；删除走软删除墓碑，
    不会出现"删掉又复活"
  - 可选 AES-256-GCM 加密：设置里填同步口令，云端只存密文
  - 自动同步：启动时 + 数据变化后 3 秒防抖触发，也可以手动"立即同步"
- **自适应界面**：宽屏（Windows）用侧边导航栏，窄屏（手机）用底部导航栏
- **Windows 桌面小组件**：设置页开启后，桌面角落常驻一张无边框置顶小卡片——
  可勾选完成、快速添加、按住标题栏拖动，与主窗口和同步实时联动
- **安卓桌面小组件**：应用数据变化后自动推送到「今日待办」小组件
  （长按桌面 → 小组件 → 待办备忘 添加；设置页有"添加到桌面"快捷入口）
- 中文界面，支持深浅色主题

## 环境要求

| 工具 | 说明 |
| --- | --- |
| Flutter SDK | 已安装在 `D:\flutter`（3.47.2 stable），已加入用户 PATH |
| Visual Studio | 已装 VS 2019 Community（含 C++ 桌面开发负载），用于 Windows 构建 |
| Android SDK | 已装在 `D:\android-sdk`（cmdline-tools、platform-tools、platform 35、CMake 3.22.1；缺什么 Gradle 构建时会自动补装） |
| JDK | 已装在 `D:\jdk-17`（Temurin 17，已通过 `flutter config --jdk-dir` 指定） |
| Gradle 代理 | 已写入 `~\.gradle\gradle.properties`，指向本地代理 127.0.0.1:7897 |
| Windows 开发者模式 | 已开启（构建带插件的 Windows 应用需要，用于创建符号链接） |

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

## 三期路线图（可选）

1. 系统托盘（最小化到托盘、快捷菜单）
2. Windows 开机自启
3. 任务提醒通知（截止时间/提醒时间）
4. 小组件真透明背景（当前是主题色卡片）
5. 安卓小组件上直接勾选任务（需要交互式 RemoteViews 广播）
6. 发布准备：替换 `com.example` 包名、应用图标、签名

## 已知限制

- 同步是"整包快照 + last-write-wins"，没有字段级合并与操作回放；两端同时改同一条目时
  时间戳新的一方获胜（个人使用足够，专业同步再演进）
- 加密口令只存在本地设置里，忘了就无法解开云端快照（重新同步会覆盖）
- Windows 窗口标题暂为 "todolist"（Runner 模板限制，二期一并处理）
- 自建服务器公网部署请务必加 HTTPS（见 server/README.md）
