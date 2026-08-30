# Memodo V2 开发计划（对齐总蓝图）

> **唯一权威规格：[BLUEPRINT.md](BLUEPRINT.md)**（2026-08-30 用户定稿）。
> 本文件只做两件事：把蓝图 §64 的 Phase 顺序映射到当前代码现状；约定执行纪律。
> 旧 `PHASES.md` 为 Flutter 遗留，仅作历史记录。

## 0. 执行纪律（蓝图 §65，逐条遵守）

- 不为改 UI 随便改数据库；不为 Widget 重写整个项目
- Windows / Android **不强行统一 UI**（数据同步，UI 不同步 §3）
- 同步代码不进 View；Win32 API 不进 ViewModel；服务器与客户端不耦合
- **一次只做一个 Phase**，不一次修改几十个模块
- 每个 Phase 走：`Inspect → Plan → Implement → Test → Screenshot → Fix → Report`
- 每阶段完工按 §66 格式记录到本文件末尾（Changed / Added / Tests / Known issues）

## 1. Phase 状态总览（2026-08-30）

| Phase | 内容（§64） | 状态 | 说明 |
| --- | --- | --- | --- |
| 0 Architecture | 代码审计 | ✅ 本次完成 | 见 [AUDIT_V2.md](AUDIT_V2.md) |
| 1 Design System | Cork/Glass/Paper/Pin + 主题 | 🟨 部分 | 有色板/卡片/图标；缺 Pin 视觉、三主题、Dark Mode |
| 2 Board + Card | Card 核心 + Canvas + Section | 🟨 部分 | Win 有拖/缩/旋/钉；缺缩放平移/Section UI/卡片类型/编辑弹窗/颜色 |
| 3 Windows Desktop Widget | P0 交互 | 🟨 部分 | 缺：Resize、位置记忆、组件内卡片拖拽/编辑、Lock |
| 4 Windows Polish | 托盘/快捷键/Quick Capture | 🟨 部分 | 托盘√ 自启√；快捷键、Quick Capture 缺 |
| 5 Android App | Grid Board / Today / Inbox / Search | 🟨 部分 | 列表√；Board 需从自由画布改为 **Adaptive Grid**(§23) |
| 6 Android Widget | 2x2/4x2/4x4 + 快速完成 | 🟨 部分 | 有列表式小组件；缺多尺寸、缺 Widget 内勾选 |
| 7 WebDAV | Provider | ❌ 未开始 | 原生端无 |
| 8 OSS/S3 | Provider | ❌ 未开始 | |
| 9 Server | FastAPI+PG | 🟨 代码完成 | 语法过、Docker 就绪、未实跑 |
| 10 Integration | 联调 + 打包 | ❌ 未开始 | |

## 2. 关键架构裁定（Phase 2 前必须定）

蓝图 §2/§10 说 **Card 是核心**（含 title/content/type）；
但 §25 又要求同步 `Board / Section / Card / Todo / Memo` 五类实体（Todo/Memo 独立存在）。
当前实现：`tasks`/`memos` 存内容，`cards` 只存引用（ref_type+ref_uuid），服务端实体无关。

**采用方案（折中，不推翻现有三端）：**
- `tasks`/`memos` 继续作为 Todo/Memo 的内容存储（同步协议照旧，服务端零改动）
- `cards` 扩展：新增 `type`（todo/memo/checklist/idea）与**可选内联内容** `title/content`，
  用于 IDEA / CHECKLIST 这类没有独立实体表的卡片
- TODO/MEMO 卡片仍引用实体表，避免双数据源（SPD 禁止项）

## 3. 下一步顺序

1. **Phase 1 + Phase 3 并行推进**（互不依赖）：设计系统补 Pin/三主题/Dark；桌面组件补 P0 缺口
2. Phase 2（Card 模型扩展 + Canvas 缩放平移 + Android 改 Grid）
3. Phase 4/5/6
4. Phase 7-9 同步矩阵，Phase 10 联调打包

---

## Phase 0 完工记录（2026-08-30）

**Changed**: docs/BLUEPRINT.md（新增，蓝图原文）、docs/PLAN.md（本文件，重写）、docs/AUDIT_V2.md（新增）
**Added**: 蓝图 Phase 0-10 状态映射；数据模型折中方案
**Tests**: 文档类变更，无运行时改动
**Known issues**: 桌面组件 Resize/位置记忆未实现；Android Board 形态与蓝图 §23 不符
**Next**: Phase 1（设计系统）+ Phase 3（桌面组件 P0 补齐）
