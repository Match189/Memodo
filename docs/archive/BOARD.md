# BOARD — 图钉板架构扩展规格（v2 方向，Memodo · 念念）

> 来源：用户的产品提案（2026-08-29），经架构评审后正式化。
> 性质：**在现有 SPD/SPEC 与已交付实现之上做增量演进**，不推翻、不重写。
> 状态：**Phase 0 规格评审完成，代码未动工**。动工顺序遵循本文件 Phase 1→6。

## 0. 核心概念升级

产品从 TodoList 升级为**个人图钉板（Personal Board）**：

```text
Board（板）
 └── Section（分区）
      └── Card（卡片）
           ├── Todo Card（引用已有 task uuid）
           └── Memo Card（引用已有 memo uuid）
```

## 1. 关键架构裁定（对原提案的修正）

原提案中 Card 含 title/content 字段。**裁定：Card 不复制内容，只引用实体**：

```text
Card = { uuid, boardUuid, sectionUuid, refType: todo|memo, refUuid, layout, ... }
```

理由：SPD 禁止事项 #2/#3（不建第二套数据、不做第二事实来源）。
Todo/Memo 实体继续是唯一内容事实来源；Board/Section/Card 是**组织与布局层**。
同步协议不变（实体照旧），Board/Section/Card 作为新增实体参与同一套 LWW 同步。

## 2. 必须共享 vs 平台适配

| 共享（同步） | 平台私有（不同步） |
| --- | --- |
| Board / Section / Card / 实体内容 / 卡片顺序 | Windows x/y/w/h/z；Android AppWidgetId、span、Launcher 摆放 |

Windows = 自由像素布局；Android = 系统摆 Widget + Widget 内部按 Board 渲染。

## 3. 数据模型（DB v5）

```sql
boards(uuid, name, created_at, updated_at, deleted_at, device_id)
sections(uuid, board_uuid, name, sort, ..., 同上)
cards(uuid, board_uuid, section_uuid, ref_type, ref_uuid,
      sort, ..., 同上)
-- 实体表 tasks/memos 不变
```

Windows 布局存本地 kv（按卡片 uuid 键），**不同步像素坐标**（SPD 精神：
同步语义与意图，不同步系统窗口）。

## 4. 分阶段

| Phase | 内容 | 预估 |
| --- | --- | --- |
| 1 | Flutter 数据层：boards/sections/cards 表 + Repository + 迁移 v5 + 测试 | 1~2 天 |
| 2 | 主应用 Board 管理 UI（建板/分区/把待办备忘钉上板） | 1~2 天 |
| 3 | Windows：Board 自由布局画布（拖动/缩放/层级/吸附）→ 桌面呈现 | 2~3 天 |
| 4 | 安卓：Widget 内按 Board 渲染（2×2/4×2/4×4 适配） | 1 天 |
| 5 | 服务器：boards/sections/cards 实体进入同步协议（结构同 todos/memos） | 1 天 |
| 6 | 跨端一致性测试（复用 E2E 框架） | 0.5 天 |

## 5. 禁止事项（沿用 SPD 并追加）

- Card 不得复制实体内容（只引用 uuid）——防第二事实来源
- Board 布局像素坐标不得进入同步协议
- Android AppWidgetId 不得作为业务 ID
- 不得一次性重构：Phase 1 数据层先行并测试通过后再动 UI

## 6. 与当前功能的关系

- 现有"桌面小组件"（单卡片）保留为 Board 模式之前的**经典模式**，
  两模式并存（设置可切换），Board 稳定后成为默认——与用户"保留现有模式、
  做成可切换"的要求一致。
- 小组件显示内容（待办/备忘/合并）设置在两种模式下都有效。
