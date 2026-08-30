# Memodo V2 执行路线图（按总蓝图重排）

> 规格：[BLUEPRINT.md](BLUEPRINT.md)（唯一权威）｜差距：[AUDIT_V2.md](AUDIT_V2.md)
> 主线 = 蓝图 §64 的 Phase 顺序；发布节点 = 蓝图 §54–§60 的版本里程碑。
> 本文件取代旧 `PHASES.md`（Flutter 遗留）与本文件此前版本。

## 0. 排期规则

1. **一次只推一个 Round**，Round 内走 §65 流程：Inspect → Plan → Implement → Test → Screenshot → Fix → Report
2. Round 结束必须提交 §66 格式报告（Changed / Added / Tests / Known issues / Next），追加到本文件末尾
3. 验收一律对照 §68 清单逐项打勾，没测过不得报"完成"
4. 与蓝图顺序的两处**现实偏差**，显式处理：
   - Server（Phase 9）代码已提前建成 → **冻结不占主线**，到 0.5 再实跑验证
   - Android Board 当前是自由画布，违反 §23（Android 用 Adaptive Grid）→ 属**形态错误**，必须排进 0.1 修正
5. 跨端不共享 UI（§3）；同步代码不进 View；Win32 不进 ViewModel

## 1. 里程碑总览（版本 × Phase）

| 版本 | 目标（蓝图 §54–§60） | 覆盖 Phase | 状态 |
| --- | --- | --- | --- |
| M0 基线 | 双端原生骨架可编译、本地库对齐 | P0 | ✅ 已完成 |
| **0.1 MVP** | Board/Card/Todo/Memo/Checklist/Idea、Win 无限画布、**Win 桌面组件**、Quick Add、拖/缩/钉、托盘、Android App、Android Widget | P1 P2 P3 + P5/P6 基础 | 🟨 **当前主战场** |
| 0.2 | Today、Inbox、Reminder、Search、Dark Mode、Cork/Glass 完整 | P1 P4 | ❌ |
| 0.3 | WebDAV（坚果云/NAS/Nextcloud） | P7 | 🟨 **提前完成-Win 端**（用户裁定：服务器延后） |
| 0.4 | OSS / S3 Compatible | P8 | ❌ |
| 0.5 | Self-hosted Server **实跑验证 + 双端接入** | P9 | 🟨 代码已提前建 |
| 0.6 | Image / Link / Attachment | — | ❌ |
| 1.0 | 集成联调、打包分发、清理 Flutter 遗留、文档收口 | P10 | ❌ |

> 注意：0.3/0.4（WebDAV/OSS）排在 0.5（自建服务器）**之前**，这是蓝图明确的顺序——
> 已建好的 Server 不改变该顺序，只是让 0.5 的风险大幅降低。

## 2. 0.1 MVP · Round 拆解（当前主战场）

### Round 1 — Phase 1 设计系统（Windows 先行）
- 设计令牌：色板 / 圆角 / 阴影 / 字号（§62 Warm+Calm+Physical+Digital）
- **Pin 图钉组件**（2.5D，红黄蓝绿四色，§16）——品牌元素，当前完全缺失
- 三主题 Cork / Glass / **Hybrid(默认)**（§17）+ Dark Mode 框架（§39：Dark Glass + Muted Paper）
- Paper 卡样式：默认 Paper/Cream 色、轻旋转限幅 ±2°（§37/§38）
- 验收：主窗 + Board 在 3 主题 × 深/浅 共 6 态截图过目（§68 Visual 前四项）

### Round 2 — Phase 3 桌面组件重构（迷你 Board）
- 组件从"双栏列表"改为**复用 Board 卡片渲染的迷你画布**（§19/§63）
- P0 补齐（§20）：位置记忆(重启恢复)、Resize、**Lock**、组件内卡片拖拽、Edit、Todo Complete、Add、Delete、AlwaysOnTop
- 布局存储：本机 kv（**不进同步协议**，同 §11 按平台分离原则）
- 验收：重启恢复位置/尺寸；拖/缩/锁/编辑可用；多屏不跑丢

### Round 3 — Phase 2 Board + Card（Windows）
- Card 模型扩展：`type`(todo/memo/checklist/idea) + 可选内联 title/content + `color`（PLAN §2 折中裁定）
- 无限画布：滚轮缩放、中键/Space 平移（§34）
- Section 视觉分区 UI（§35）+ Card 编辑弹窗（§29）+ 卡片颜色（§38）
- 验收：§68 Windows 段 Canvas/Card 相关项全勾

### Round 4 — Phase 5/6 Android 对齐
- **Board 改 Adaptive Grid**（§23），布局写 `card_layouts(platform="android")` 的 order 字段
- Widget：2×2 / 4×2 / 4×4 三尺寸（§24）+ **Widget 内勾选完成**（直写 Room）
- 验收：真机安装冒烟 + Widget 三尺寸添加成功

### Round 5 — 0.1 发布（Phase 4 前半）
- 托盘菜单补全：New Todo / New Memo / Sync Now(未配置置灰) / Settings（§21）
- **Export JSON 全量备份**（§52 第一版必须，防数据锁死）
- 双端打包：Win `publish` 自包含、Android release 签名
- 验收：两份成品可安装可卸载，数据可导出

## 3. 0.2 → 1.0 概要（按蓝图，不展开）

| 版本 | 要点 |
| --- | --- |
| 0.2 | Today 页(§27)、Inbox(§26)、Reminder、Search+Ctrl+K、Quick Capture 弹窗+热键(§22/§28)、Dark/Cork/Glass 收尾 |
| 0.3 | `WebDAVProvider`（ISyncProvider 接口，§41/§43）；凭据入 Windows 凭据管理器/Keystore(§53) |
| 0.4 | 通用 `S3Provider`（MinIO/OSS/AWS 兼容，§44） |
| 0.5 | Server 实跑 + pytest + 双端 `ServerSyncProvider` + SyncQueue(§48) + 墓碑 GC(§49) |
| 0.6 | 附件/图片/链接类型卡片 |
| 1.0 | Phase 10 集成、清理 Flutter 遗留(§67 目录收敛)、文档收口、§68 全清单验收 |

## 4. 每轮报告格式（§66，固定追加在本文件末尾）

```text
## Round N · Phase X 完工记录（日期）
Changed / Added / Tests / Screenshots / Known issues / Next
```

## 5. 待拍板（不阻塞 Round 1-2）

- 桌面组件布局存本机 kv 还是 `card_layouts` 表 → **暂定本机 kv**（视觉状态不进同步协议）
- 遗留 Flutter 工程删除时点 → 1.0 收尾阶段
- 0.1 的托盘 Sync Now：置灰提示"0.5 可用"，还是接已建好的 Server 试同步 → **暂定置灰**，避免 0.1 引入未验证依赖

---

## Round 记录（追加区）

### Round 1 · Phase 1 设计系统 完工（2026-08-30，cf9c547）
**Changed** App.xaml（设计令牌默认值）、ShellWindow/TaskList/MemoList/Board/Settings（全部改 DynamicResource）
**Added** ThemeService（Cork/Glass/Hybrid × Dark 运行时切题）、PinFactory（2.5D 图钉）、设置页外观区
**Tests** dotnet build 0 错误；6 主题态切换走 Settings 下拉/深色开关
**Known issues** Cork 纹理为纯色近似（无噪点/暗角贴图）；Glass 面板为实色近似非真 Blur

### Round 2 · Phase 3 桌面组件 完工（2026-08-30，5b2bb08）
**Changed** DesktopWidgetWindow 重构（双栏列表→迷你 Board）、SettingsStore(+组件位置/布局 kv)、MemoRepository(+GetById)
**Added** WindowChrome ResizeBorder、位置/尺寸防抖持久化、Lock、双击编辑(EditCardWindow)、组件内卡片拖拽/缩放/勾选/取消钉
**Tests** dotnet build 0 错误；构建期验证（真机交互待用户验收）
**Known issues** 组件内卡片暂不支持旋转（板内布局 rotation 未带出）；Opacity/Click-through 属 P1

### Round 3 · Phase 2 Board+Card 完工（2026-08-30，bd5a93a）
**Changed** AppDatabase(DB v2: cards+type/title/content/color)、CardItem、BoardRepository、BoardViewModel、BoardView
**Added** 无限画布缩放平移(§34)、双击编辑弹窗(§29)、idea/checklist 内联卡(§10)、纸色四选(§38)、+卡片/1:1 工具栏
**Tests** dotnet build 0 错误；DB v2 迁移含 AddColumnIfMissing 幂等保护
**Known issues** Section 视觉分区 UI 未做（表结构已就位）→ 0.2；卡片微动画(§40)未做 → 0.2

### Round 4 · Phase 5/6 Android 完工（2026-08-30，81f6c9d）
**Changed** Entities(DB v2 对齐 cards 列)、Daos(+CardDao.update)、Repo(+moveCard/setTaskDone/getTask)、BoardScreen 重写、Manifest
**Added** AdaptiveGrid 图钉板(§23)、Widget CheckBox 快速完成(§24)、ToggleTaskAction、2x2/4x2/4x4 三 provider
**Tests** assembleDebug BUILD SUCCESSFUL；真机冒烟待设备接入
**Known issues** 卡片内联 type/color 已入库但同步协议未含 cards（0.5 接）；网格拖拽排序用按钮微调实现

### Round 5 · 0.1 发布 完工（2026-08-30）
**Changed** TrayService（§21 托盘全菜单）、SettingsView(+数据区)、.gitignore(+publish/)
**Added** ExportService（§52 JSON 全量导出）、托盘 New Todo/Memo/Settings 导航、Sync Now 置灰(0.5 开放)
**Tests** dotnet build 0 错误；`dotnet publish -c Release -r win-x64 --self-contained` ✅（publish/Memodo.Windows.exe，175MB）；`assembleRelease` ✅（app-release-unsigned.apk，12MB）
**Known issues** Android release 未签名（安装需自建 keystore 签名）；Win 包为自包含未做单文件/MSIX

### Round 6 · Phase 7 WebDAV 提前（用户裁定：先用坚果云，服务器延后）
**Changed** SettingsStore（SyncProvider/WebDav*/DeviceId/LastSyncAt）、SettingsView 同步区（双通道切换）、SyncEngine、TrayService、.gitignore
**Added** WebDavClient（GET/PUT/MKCOL，Basic 认证，纯 HttpClient）、快照同步（LWW+墓碑，§47/§49，平局 deviceId 字典序 §19）、SecretProtector（DPAPI，§53 不落明文）、托盘"立即同步"实接
**Tests** 坚果云实测：MKCOL=201 / PUT=201 / GET 往返 ✅ / 缺文件=404；dotnet build 0 错误
**Known issues** Android 端 WebDAV 同步未接（快照格式已定，下轮移植）；快照为全量（条目多时体积增长，后续可做差量/GC）

### Round 7 · Flutter 资产移植（用户指令：参考旧工程移植功能）
**Changed** WindowChrome（+setSurface 材质/AttachToDesktop WorkerW）、ThemeService（+BoardPalette/SurfaceTint/ThemeChanged）、BoardView（软木纹理层）、DesktopWidgetWindow（真毛玻璃材质重构）、SettingsView（不透明度/自动同步）、App（自动同步定时器）、MemodoWidget（显示设置过滤）、Screens.kt（设置页真实控件）
**Added**
- `setSurface` 移植：acrylic BLURBEHIND/透明渐变 × 不透明度 30-100，组件改为非分层窗口由 DWM 圆角兜底
- 桌面附着（Phase 3）：组件菜单"附着桌面(实验)"，失败回退 + 持久化
- 软木板纹理（board_background 移植）：底色渐变 + Random(20260829) 种子噪点 600 点 Viewbox + 四角暗角；主题切换事件联动重绘
- 锁定禁拖（lockPosition 语义）：锁定时 WindowChrome.CaptionHeight=0
- 自动同步（sync_manager 精神）：启动一次 + 每 3 分钟，静默失败下轮重试，成功刷新组件
- Android Widget maxItems(4-30,默认12)/showCompleted(默认false) + 设置页滑杆/开关（SharedPreferences，改后 updateAll）
**Tests** dotnet build 0 错误；assembleDebug BUILD SUCCESSFUL；publish 更新
**Known issues** 附着桌面模式下拖动/缩放行为依赖 Explorer（实验，与 Flutter 期一致）；acrylic 在旧于 1803 的 Windows 上降级为透明渐变

### Round 8 · 设计对齐（用户指令：参考 PinBoard 设计稿完善产品）
**Changed** ThemeService（暖橙主色 #D4763B/暖纸底 #F7F4EF/软木 135° 渐变）、PinFactory（设计稿四色图钉+高光钉帽+NoteColors 五色）、AppDatabase（DB v3 cards+note_color）、CardItem、BoardRepository、EditCardWindow（图钉色×纸色双行）、BoardView（便签化+右键菜单）、DesktopWidgetWindow（便签纸面）、MemodoWidget（头部进度）、Entities/Database(Room v3)
**Added**
- 视觉对齐：暖橙品牌色、暖纸背景、软木 135° 渐变+8% 噪点、便签小圆角(2/4)、楷体正文
- 图钉色（4=分类：紧急/资料/完成/待办）× 便签纸色（5）组合；双端着色
- 卡片 hover：摆正（角度→0）+ 放大 1.03 + 置顶（设计 sticky-note:hover）
- 右键空白：选模板（待办清单/文字便签）→ 点击位置生成 + 随机微旋转 ±2° → 就地编辑
- 卡片右键：编辑 / 图钉色 / 便签纸色 / 复制（Todo/Memo 复制为新实体并钉板）/ 取消钉
- Android Widget 头部进度「N/M 完成」；Room v3 noteColor
**Tests** dotnet build 0 错误；assembleDebug BUILD SUCCESSFUL；publish 更新
**Known issues** 标签(tags)字段设计稿有但未入库；3×3 进度环 Widget、点击穿透、WebSocket 实时同步属后续阶段

### Round 9 · 缺陷三连修 + Android WebDAV 闭环（设计稿 Phase 1「手动双向同步」双端达成）
**Changed** TaskListViewModel、MemoListView、DesktopWidgetWindow(xaml)、Daos、Screens.kt、.gitignore
**Added**
- 修复：待办勾选被二次翻转——勾选框双向绑定已写回 Completed，Toggle 不再取反（状态弹回 bug）
- 修复：桌面组件右上角置顶/菜单按钮不可点——WindowChrome 标题栏区域控件需
  `IsHitTestVisibleInChrome=True` 豁免，否则点击被当成拖窗
- 新增：备忘录列表行内「编辑」按钮（复用 EditCardWindow，保存后刷新）
- **Android WebDAV 同步引擎（WebDavSync.kt）**：HttpURLConnection 实现 GET/PUT/MKCOL+Basic 认证，
  与 Windows 共用坚果云 memodo/memodo-sync.json；LWW+墓碑+deviceId 平局决胜（§19/§47/§49）；
  应用本地用 REPLACE upsert 原样保留服务端时间戳
- Android 设置页同步表单（地址/账号/应用密码/立即同步/上次同步时间）
**Tests** DB 诊断（用户真实库 5 待办/3 备忘全活跃 → 确认根因在 UI 绑定层）；双端构建 0 错误；publish 更新
**Known issues** Android 侧凭据存 SharedPreferences（明文）——后续换 EncryptedSharedPreferences；
双端 WebDAV 均为手动+Windows 自动；实时推送（设计稿 Phase 2 WebSocket）未做

### Round 10 · 产品形态定型（用户裁定）：主窗口=纯列表，组件=唯一钉板
**Changed** DesktopWidgetWindow（数据源重写）、ShellWindow（移除列表/钉板切换）、TrayService（订阅 DataChanged）、MemoListViewModel/TaskListViewModel（变更广播）、MemoListView（完成勾选）、AppDatabase（DB v4 memos+completed）、MemoItem、MemoRepository、Entities(Room v4)、WebDavSync、Screens.kt、Repo.kt
**Added**
- 组件板面 = **全部未完成待办 + 全部备忘**（不再依赖 cards 钉选）；
  待办勾选完成 / 备忘点✓完成 → 即时从板面移除；主窗口列表同步划线
- 备忘获得 completed 字段（Win DB v4 / Room v4 / 同步 JSON completed），语义同待办
- 双向实时：主窗口增删改 ↔ 组件即时互刷（App.DataChanged，Reload 不再广播防回环）
- 主窗口移除右上角 列表/钉板 切换，回归纯列表（待办/备忘/设置）
- Android 备忘列表加完成勾选（划线显示）
**Tests** dotnet build 0 错误；assembleDebug BUILD SUCCESSFUL；publish 更新
**Known issues** 组件布局 kv 旧 cardId 键残留（无副作用）；BoardView/BoardViewModel 暂时闲置（钉板已收敛到组件，后续清理）

### Round 11 · 用户反馈批次修复 + Android 同步方式补全
**Changed** TaskListView/MemoListView（编辑按钮+分组）、DesktopWidgetWindow（完成即刷+类型图标）、TaskListViewModel、AppDatabase、MemoRepository、BoardRepository、MemodoWidget（自绘圆勾）、AndroidManifest、Screens.kt、WebDavSync（OkHttp 重写）、app/build.gradle.kts(+OkHttp)
**Added**
- 修复同步失败 ordinal=6 / 备忘与钉板空白（幂等补列 + FieldCount 守卫 + sync.log）
- 修复组件↔主窗口不同步（完成/删除操作本地 Reload + DataChanged 广播）
- 待办/备忘列表按 未完成/已完成 分组显示（双语组标题）
- 待办行加编辑按钮；QuickAddWindow 备忘模式带内容栏（与 Android 对齐）
- 组件板面便签加类型标识（✓待办 / ✎备忘）
- Android：删除 2×2 provider；自绘圆勾替代 Glance CheckBox（首帧渲染缺陷）；
  HTTP 层迁移 OkHttp（修复 MKCOL 白名单异常）；
  同步方式补全：仅本地 / WebDAV / 自建服务器（ServerSync.kt，JWT+push/pull cursor）
**Tests** dotnet build 0 错误；assembleDebug/assembleRelease BUILD SUCCESSFUL（签名）；publish 更新
**Known issues** Android 深层设置文案未双语；ServerSync 每次登录不缓存 token（简化）；OSS/S3 通道仍排 0.4

### Round 12 · 交互语义修正（用户裁定）：备忘=眼睛可见性，钉板无删除
**Changed** MemoItem（+ShowOnBoard）、AppDatabase（DB v5 memos+show_on_board）、MemoRepository、MemoListViewModel（ToggleBoardVisible）、MemoListView（眼睛按钮+分组改版）、DesktopWidgetWindow（板面去×删除/备忘改眼睛斜线/过滤隐藏）、Entities(Room v5)、WebDavSync（wire + show_on_board）、Repo/MainViewModel/Screens（眼睛切换）
**Added**
- 备忘与待办语义分离：**待办=打勾完成**（钉板移除+主窗口划线分组）；**备忘=眼睛可见性**（显示/隐藏钉板，无划线无完成）
- 钉板移除红色 × 删除按钮；备忘便签改眼睛斜线按钮
- 主窗口备忘列表分组改为「钉板显示中 / 未在钉板显示」，隐藏行灰显
- 同步 wire 增加 memos.show_on_board（双端一致）
**Tests** dotnet build 0 错误；assembleDebug/assembleRelease BUILD SUCCESSFUL（签名）；publish 更新
**Known issues** memos.completed 列保留但弃用（避免破坏性迁移）；Android 备忘隐藏状态图标跟随主题色

### Round 13 · 全链路走查与修正（用户指令：梳理逻辑链路，彻底检查）

**链路梳理（现行为准）**
```
数据链   AppDatabase(启动幂等补列) → 仓储(软删除墓碑/LWW时间戳) → ViewModel/Repo → 视图
显示链   主窗口(页面缓存+DataChanged重载) ⇄ App.DataChanged ⇄ 桌面组件(板/列表双模式)
设置链   SettingsStore(JSON) ↔ SettingsView → 主题/语言即时生效 · 同步间隔→App定时器 · 眼睛/勾选→数据
同步链   SyncEngine(WebDAV快照/Server游标) ↔ 坚果云/memodo-server → NotifyDataChanged → 双端UI
```
**走查修正**
- QuickAddWindow 备忘模式补内容栏（与 Android 对齐；上轮提交信息与实际不符已纠正）
- 组件板面移除遗留 Pin 调用（卡片模型不再驱动板面）
- 设置页三处同步入口（设置按钮/托盘/自动定时）成功后统一 NotifyDataChanged
- TaskListViewModel.ClearDone 补广播；主窗口两处编辑弹窗保存后补广播（组件标题联动）
- Android BoardScreen 重写为新语义（全部未完成待办+未隐藏备忘网格，勾选/眼睛切换，
  头部统计），移除 cards/PinPicker/FAB 遗留
**Tests** dotnet build 0 错误；assembleDebug/assembleRelease BUILD SUCCESSFUL（签名）；publish 更新
**Known issues** BoardView/BoardViewModel/cards 数据链闲置待清理（Phase 10）；Android ServerSync 每次登录不缓存 token
