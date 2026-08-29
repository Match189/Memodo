# REFERENCE — 同类开源项目参考

> 与 Memodo（跨平台 Todo/Memo + 双端小组件 + 可插拔同步 + 自建服务器）相似或
> 架构可借鉴的开源项目。调研时间 2026-08-29。

## 第一梯队：架构最相似

| 项目 | 技术栈 | 同步方式 | 对 Memodo 的参考价值 |
| --- | --- | --- | --- |
| [Joplin](https://github.com/laurent22/joplin)（~55k⭐） | TypeScript/Electron + 移动端 | 本地 SQLite + 可插拔目标（WebDAV/Nextcloud/Dropbox/OneDrive/S3）+ 可选 E2EE | 同步目标抽象 ≈ 我们的 SyncProvider；E2EE 快照加密设计可对照；**其 WebDAV 假成功 bug（#12810）正是 R2 冲突重检要防的坑** |
| [Super Productivity](https://super-productivity.com/) | Angular + 桌面/安卓 | 本地优先 + WebDAV/Dropbox/Nextcloud/本地目录/可自建 SuperSync 服务器 | **概念最接近**：Todo + WebDAV + 可自建同步服务器；其 SuperSync 协议与冲突处理值得精读 |
| [Saber](https://github.com/saber-notes/saber) | **Flutter**（手写笔记） | E2EE + 官方服务器/Nextcloud/自建 | 单人开发的 Flutter 跨 6 端应用——**规模与团队最像 Memodo**，E2EE 与自建服务器实现可直接读 |
| [Ente Auth](https://ente.com/auth/) | **Flutter** + 自研服务器 | E2EE 多端同步，服务器可自托管（museum 开源） | Flutter 客户端 + 可自托管服务器 + 加密同步的完整工程范本 |

## 第二梯队：某一侧面的参照

| 项目 | 定位 | 参考点 |
| --- | --- | --- |
| [Vikunja](https://vikunja.io/) | 自托管任务服务器（Go）+ 多客户端，团队协作向 | 任务服务器 API 设计、分享/权限模型 |
| [AppFlowy](https://github.com/appflowy-io/appflowy)（30k+⭐） | Flutter+Rust 本地优先 Notion 替代 | Flutter 大型工程的架构（对我们的体量偏重） |
| [Tasks.org](https://github.com/tasks-tasks/tasks) | 安卓 Todo（CalDAV 同步） | **安卓小组件交互**（可直接勾选的复选框）是我们 Phase 4 同款能力的成熟参照 |
| Standard Notes / Notesnook | 加密笔记 + 可自建 | 默认加密（vs Joplin 的可选加密）的产品取舍 |
| Syncthing / CouchDB 复制协议 | 同步引擎 | 本地优先同步的经典协议设计（checkpoint/rev），若未来做字段级合并的理论起点 |
| [Automerge](https://github.com/automerge/automerge) / [Yjs](https://github.com/yjs/yjs) | CRDT 库 | SPD §19 明确暂缓的字段级合并，未来若做的现成轮子 |

## Memodo 的差异化定位

调研结论：**"桌面小组件优先 + 安卓可交互卡片 + BYOC 三通道 + 极简可自建服务器 + 中文优先"**
这个组合在开源界没有直接对标——现有项目要么重（Vikunja/AppFlowy 团队向）、
要么没有桌面小组件（Joplin）、要么同步只有 BYOC 没有轻服务器、要么不支持中文场景。
保持"个人尺度、小组件优先、零依赖自建"就是差异化本身。
