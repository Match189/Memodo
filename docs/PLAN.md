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
| 0.3 | WebDAV（坚果云/NAS/Nextcloud） | P7 | ❌ |
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

（尚无 Round 完工记录；Round 1 = Phase 1 设计系统，进行中）
