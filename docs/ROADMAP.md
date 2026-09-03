> **当前状态（2026-09-03）**：v0.2.0 已开源发布（Apache-2.0，tag v0.2.0）。
> 本文下半部分是 v1.0 时期的历史计划（多数已完成，冲突矩阵一节仍是协议事实来源，另见 docs/PROTOCOL.md）。
> 面向社区的前瞻路线见下节；历史计划保留在折叠区之后供追溯。

## 下一阶段（面向开源社区）

- 🔜 **GitHub Release 产物**：上传 Windows exe / Android APK（tag v0.2.0 已推）
- 🔜 **演示截图**：README 三端截图（干净演示数据）
- 🔜 **CI**：GitHub Actions —— Android `assembleDebug`、Windows `dotnet build`、服务端回归用例
- 🔜 **CONTRIBUTING.md + issue/PR 模板**
- 🔜 **服务端测试入库**：24 用例移植为 pytest 随 CI 跑
- 🔜 **口令轮换重加密**：换口令时用新口令重封服务端旧行（受控迁移）
- 🔜 **Windows UIA 可访问性**：自绘控件挂钩 UIA（无障碍 + 可测试性）
- 💡 iOS/macOS 客户端 · Web 只读看板 · 重复任务/提醒 · 附件 E2EE · WebDAV 连接测试向导

---
# ROADMAP — 综合工作计划（v1.0 发布与同步深化）

> 本文是 SPD 之后的滚动计划。每轮完成后在本文标注状态并回写 PHASES.md。

## 0. 同步架构现状与冲突处理基线（已实现）

用户提案的三点与现状对照：

| 提案 | 现状 | 结论 |
| --- | --- | --- |
| 每条事项/备忘一个唯一 ID 避免冲突 | ✅ 已实现：`uuid` 全局标识（v2 起），合并按 uuid 并集，本地自增 id 不参与同步 | 方向正确，已是现实 |
| WebDAV/OSS 拉到本地合并再上传 | ✅ 已实现：`SnapshotSyncProvider` = 拉远端快照 → 本地按 LWW 合并 → 差异落库 → 回传合并结果 | 已是现实 |
| 自建服务器云端仲裁后同步各端 | ✅ 已实现：todo-server `/sync/push` 逐条服务端 LWW（`rejected: stale`），`/sync/pull` 按 server_seq cursor 增量 | 已是现实 |

**冲突场景矩阵（当前行为）**：

| 场景 | 结果 |
| --- | --- |
| 两端同时改**不同**条目 | 合并为并集，无冲突 |
| 两端同时改**同一条**（如都改标题） | `updatedAt` 新者胜（LWW），旧端下次拉取被覆盖 |
| 时间戳完全相同 | `deviceId` 字典序决胜，两端结论一致（不打乒乓） |
| 一端删除、另一端修改 | 新时间戳者胜；删除=写墓碑，不会"复活" |
| 两端离线各自新增 | 合并为并集（uuid 保证互不覆盖） |
| 上传被服务器拒绝（stale） | 客户端拉取服务器权威版本，本地旧版被覆盖 |

## 1. 本提案下真正需要补强的两点（Round 2）

1. **上传冲突重检（防"整包覆盖"竞态）**：WebDAV/OSS 快照通道目前"合并结果直接 PUT"，
   理论上存在 A、B 同时基于旧快照各自合并上传、后传者覆盖先传者的窗口。
   方案：PUT 前重新 GET 校验远端 `exportedAt`/摘要，若已被他人更新则重跑合并再传（最多重试 2 次）。
   工作量：~0.5 天，纯客户端。
2. **归档（合并存档）**：已完成条目可一键归档/自动归档（如完成后 7 天），归档项从主列表
   隐藏、进"归档"页可查可搜索。归档=加 `archivedAt` 字段（DB v4），照常参与 LWW 同步。
   工作量：~1 天（模型+UI+同步字段）。

> 字段级合并（同一条目两端分别改标题和勾选）暂不做：个人使用下 LWW 足够，且会把
> 同步协议复杂度提高一个数量级（SPD §19 明确暂缓）。

## 2. 综合轮次计划

| 轮次 | 主题 | 内容 | 工作量 |
| --- | --- | --- | --- |
| **R1 v1.0 发布就绪** | 正式身份 | ① 定名 **Memodo** + 包名迁移 `app.memodo`（Dart/Android/Windows 全栈 + 旧数据自动迁移）② 应用图标（flutter_launcher_icons 全尺寸 + 托盘图标）③ Android 签名（PKCS12 keystore，CN=Memodo，密钥本地不入库）④ 系统托盘 + 开机自启 ⑤ 服务器公网准备（pull deviceId 心跳、Caddyfile 示例） | ✅ 完成 |
| **R2 同步深化** | 冲突重检 + 归档 | ① 上传冲突重检 ② 归档功能 ③ 数据导出/导入 JSON ④ ~~pull 携带 deviceId~~（已并入 R1） | 1 天 |
| **R3 提醒与详情** | 激活数据字段 | ① 任务详情页（description/dueAt/priority 编辑）② 到期本地通知（双端）③ 搜索过滤 | 1~2 天 |
| **R4 服务器正式化** | 公网测试（用户已确认会做） | 按 todo-server/README 部署：compose + Caddy HTTPS + 公网客户端连通测试 | 用户执行 |

| 决策项 | 决定 |
| --- | --- |
| 项目名 / 包名 | **Memodo** / `app.memodo` |
| 远程仓库 | 所有工作完工后统一上传（本地 git 逐阶段提交已就绪） |
| 服务器 | 将在公网测试 → HTTPS 部署按 todo-server/README + Caddyfile.example 执行 |

---

## R1 完工记录（Memodo v1.0 发布就绪）

**定名**：Memodo（Memo + Todo 合成词）。包名 `app.memodo`；Windows 产品名 Memodo、exe `memodo.exe`。

**修改文件**
- `pubspec.yaml`（name: memodo + tray 图标资产）、全部 `package:memodo/` 导入
- Android：`build.gradle.kts`（namespace/applicationId/签名配置）、Kotlin 包目录 `app/memodo/*`、key.properties（本地）、`memodo-release.jks`（本地）
- Windows：`CMakeLists.txt`（BINARY_NAME memodo）、`Runner.rc`（公司/产品/描述）、运行时窗口标题 `待办备忘`
- 新增：`lib/desktop/{autostart,tray_service,main_window}.dart`、`assets/icon/*`（GDI+ 绘制源图 + ico）
- `lib/main.dart`：旧数据迁移（`%APPDATA%\com.example\todolist` → 新支持目录，幂等）、托盘初始化
- 服务器：`/sync/pull` 可选 deviceId 心跳、`Caddyfile.example`、token 加 jti 防同秒重复

**验证**：analyze 0 error；Flutter 22/22、pytest 6/6；`memodo.exe` 启动 + 小组件自动恢复（位置记忆）+
旧库自动迁移实测（32KB 新库）；APK 签名证书 `CN=Memodo, OU=Personal, O=Memodo, C=CN` 验证通过。

**已知问题 / 用户须知**
- ⚠️ **务必备份 `android/app/memodo-release.jks` 和 `android/key.properties`**（丢失后无法再发布同应用更新）
- 安卓端包名变了 = 全新应用：手机重装后需重新登录坚果云/服务器，云端数据会自动拉回
- Windows 首次启动自动把旧库复制到 `%APPDATA%\app.memodo\Memodo\`（旧文件保留未删）
- 公网部署按 `todo-server/README.md` + `Caddyfile.example`；测试期也建议至少改默认 JWT 密钥

## 补充决策：中文产品名

定名 **「念念」**（Memodo）。取自"念念不忘，必有回响"——应用的职责正是替用户
念着那些待办与备忘。英文品牌 Memodo 不变；用于启动器名称、任务栏标题、托盘提示。

---

# 二次全面分析（2026-08-29）与下阶段计划（R2 确定版）

## 项目体检结论

- 规模：Flutter 客户端 4912 行 + 测试 822 行（22 用例）+ 服务器 880 行（6 用例）；15 个提交，工作树干净
- 质量：analyze 零 error（36 条 info）；代码内 TODO/FIXME 债务为 **0**
- 功能面：SPD 九阶段 + 深度美化 + R1 发布就绪全部落地；桌面端体验完整

## 未验证面与风险（按优先级）

1. 🔴 **手机端新包全链路未复验**——包名迁移后 `app.memodo` 是全新应用，
   "装 APK → 登录坚果云 → 同步拉回 → 添加小组件 → 卡片勾选回传"整条闭环
   需要真机跑一遍（此前仅桌面端验证）
2. 🟡 WebDAV/OSS 快照通道的双设备**上传竞态**（R2 冲突重检目标）
3. 🟡 完成条目只有手动清理，无归档/自动收纳
4. 🟢 `settings_page.dart` 已 668 行（外观/同步/小组件三块挤在一页），可维护性下降
5. 🟢 36 条 info lint；WebDAV 传输层无自动化测试

## R2 确定版：「同步深化 + 真机验收」（工作量 1.5~2 天）

1. **上传冲突重检**：快照 PUT 前重新 GET 校验远端 `exportedAt` 摘要；
   若已被其他设备更新 → 重跑合并再上传（最多重试 2 次）
2. **归档**：DB v4 增 `archived_at`；完成条目一键/自动归档（默认完成后 7 天）；
   归档页查看与搜索；照常参与 LWW 同步
3. **数据导出/导入**：JSON 快照导出到文件 + 导入恢复（换机与灾备双保险）
4. **手机真机验收**：执行下方清单并记录结果
5. **收尾**：settings_page 拆分为 appearance/sync/widgets 三个文件；lint 清零

### 手机真机验收清单

- [ ] 安装 `app.memodo` 新 APK（旧"待办备忘"可卸载）
- [ ] 设置 → WebDAV → 测试连接 → 立即同步 → 电脑数据拉回
- [ ] 手机新增一条 → 电脑 3 秒内出现
- [ ] 添加小组件 → 卡片显示 → 卡片上勾选 → 打开应用状态一致
- [ ] （可选）切自建服务器通道：公网部署后重复同步测试

## 后续队列（R2 之后）

R3 提醒与详情（dueAt 通知/详情页/搜索）→ R4 服务器公网部署支持 →
末轮：上传远程私有仓库（用户要求所有完工后统一执行）
