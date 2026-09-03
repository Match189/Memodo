对，这次我建议**不要再拆着讨论了**。我们把 **Memodo** 从产品定位、功能、UI、Windows 桌面浮窗、Android Widget、数据模型、三种同步方式、服务器、开发阶段、技术栈、验收标准，一次性定下来。

你可以把下面这份作为 **Memodo 的总开发蓝图**，然后再拆成 `PRD / SPD / SPEC / UI_SPEC / ARCHITECTURE / ROADMAP` 给 Coding Agent。

---

# Memodo 完整产品开发计划

## 0. 产品定义

**产品名：Memodo**

> **Memo it. Do it.**

中文定位：

> **Memodo · 数字备忘与待办图钉板**

核心不是传统 Todo List，而是：

> **把“我要记住的东西”和“我要做的事情”，像纸条一样钉在自己的数字空间里。**

核心体验：

```text
想到
 ↓
快速记录
 ↓
变成 Card
 ↓
钉到 Board
 ↓
一直可见
 ↓
完成 / 归档
```

---

# 1. 产品的四个核心概念

Memodo 不要设计成“Todo + Note 两个软件拼在一起”。

核心数据模型应该是：

```text
                 Memodo
                    │
             ┌──────┴──────┐
             │             │
           Remember       Do
             │             │
          Memo / Idea    Todo
             │             │
             └──────┬──────┘
                    │
                  Card
                    │
                  Board
```

也就是说：

### Card 是核心

而不是 Todo。

Card 可以是：

* Todo
* Memo
* Checklist
* Idea

以后还能扩展：

* Link
* Image
* Quote
* Bookmark
* AI Note

---

# 2. 产品整体形态

最终 Memodo 应该有：

```text
Memodo
│
├── Windows App
│
│   ├── Main Window
│   ├── Board
│   ├── Search
│   ├── Today
│   └── Settings
│
├── Windows Desktop Widget
│
│   ├── Floating Board
│   ├── Desktop Board
│   └── System Tray
│
├── Android App
│
│   ├── Board
│   ├── Today
│   ├── Inbox
│   └── Search
│
├── Android Home Widget
│
└── Sync System
    │
    ├── Local
    ├── WebDAV
    ├── OSS / S3
    └── Self-hosted Server
```

---

# 3. 三端原则

这里必须特别明确：

> **数据同步，UI 不同步。**

Windows 和 Android 可以完全采用不同技术。

```text
                Shared Data Model
                       │
             ┌─────────┴─────────┐
             │                   │
          Windows              Android
             │                   │
       自由 Canvas              Grid
       桌面浮窗                Home Widget
       Mouse Drag             Touch
       Win32                  Android API
```

不要为了“跨平台”强行让 Flutter 管所有东西。

---

# 4. 推荐技术栈

## Windows

如果现有 Windows 端已经是 Flutter，可以继续使用现有业务逻辑。

但**桌面浮窗强烈建议原生实现**：

```text
C#
.NET 8/9
WPF
Win32 API
SQLite
MVVM
```

如果现有 Windows App 已经比较成熟：

```text
Existing Flutter App
        +
Native Windows Widget
```

也是可以的。

不要为了架构洁癖全部重写。

---

# 5. Android

推荐：

```text
Kotlin
Jetpack Compose
Room
Jetpack Glance
WorkManager
```

其中：

### Compose

负责 App。

### Glance

负责 Android Home Screen Widget。

---

# 6. Server

第一版：

```text
FastAPI
PostgreSQL
Docker
Nginx
```

以后需要再增加：

```text
Redis
Object Storage
Background Worker
```

第一版**不要为了所谓高并发搞复杂架构**。

Memodo 是个人生产力工具。

---

# 7. Local Database

必须有数据库。

## Windows

```text
SQLite
```

## Android

```text
Room
```

## Server

```text
PostgreSQL
```

---

# 8. 为什么必须 Local-first

Memodo 必须做到：

> **没有网络也可以正常使用。**

架构：

```text
User
 ↓
UI
 ↓
Repository
 ↓
Local DB
 ↓
Sync Queue
 ↓
Sync Provider
 ↓
Remote
```

而不是：

```text
UI
 ↓
HTTP
 ↓
Server
 ↓
Database
```

否则网络稍微有问题 Todo 就不能用了。

---

# 9. 核心数据模型

## User

```text
User
- id
- createdAt
- updatedAt
```

---

## Board

```text
Board
- id
- name
- description
- theme
- createdAt
- updatedAt
- deletedAt
```

---

## Section

```text
Section
- id
- boardId
- name
- x
- y
- width
- height
- createdAt
- updatedAt
```

Section 是视觉分区。

不是传统数据库意义上的分类。

---

# 10. Card

核心：

```text
Card
- id
- boardId
- sectionId
- type

- title
- content

- status
- priority

- dueAt
- reminderAt

- createdAt
- updatedAt
- deletedAt
```

type：

```text
TODO
MEMO
CHECKLIST
IDEA
```

---

# 11. Card Layout

一定独立：

```text
CardLayout
- cardId
- x
- y
- width
- height
- rotation
- zIndex
```

这样：

```text
Todo
     ↓
业务数据

CardLayout
     ↓
Windows / Android
各自布局
```

Windows：

```text
x=500
y=300
rotation=-2
```

Android：

```text
gridPosition=4
```

两者互不影响。

---

# 12. Desktop Widget

增加：

```text
DesktopWidget
- id
- boardId

- x
- y
- width
- height

- alwaysOnTop
- locked
- clickThrough
- opacity
- theme

- visible
- createdAt
- updatedAt
```

以后可以：

```text
一个 Board
 ↓
多个 Widget
```

例如：

```text
Work
Ideas
Today
```

同时显示。

---

# 13. 最核心的视觉方案

Memodo 最终采用：

# **Cork + Glass + Paper + Pin**

四层结构。

![Image](https://images.openai.com/static-rsc-4/vsVIF361Fw38kZCecN9RMzGjap0hSBnnLrBiyfCU6Jcl79-5ajtS_8Shp1vrcyJKWiBStCNGfPNYCyQ9MY5oXTe74NiWjCAY0YZ4dlTUYz_G5Yu23Ul5vju3TqKhG5hoMw-aHlf5oWBzgfbHJ2031bhukozEfq-qFcZdQekRZP6jMjIPzky9zLo2tZIpZux2?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/PqtRVbTRwfugphiC9VUBk0JeVOEcUBdf5seoPxYXx0bhfECE1EDUrPLMaOrp_1vrLNf7dknp78Z-WJay302bZhb4aSXsMocMQmA9wMD2yefP6UygvQ7khCqpRT7QCmTsG3srOfQ9iSlYtiVFXHn9Rz4KcfMVfhhivGq35scRw_ovwVdh67Lw7kUpOvL4RNLP?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/hXc7a3wgSA88cOxTDFhq3t-rdq5GYutzmjMunwkMUURqeSBwq3i0c1kkh3te77QnZgLwlEW9TvI1Ftsoc1BVTfv6zgH-gzEhbnwwUqWoA3YOAH--nAqS4T5G-z6w5n6dd4oY9JaJP98-CqmA1f6uqvWS1xIOfmrXfO0V9SsbI2SudlGZDT9qlyW1cE2Bo8WK?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/ccvMP59ZeMF6bLgI4zs1BpXJeAS97kBiyvhDjoRJpAD8KrPyvA9-ONDWmbwVPX1GwSg_F4UgUuKxoKRwfUKSyInh_w_sUPZ1vJnGgKD-A2FgDeOeJOEZ-2LVNp9slWImbkgMxoiA1BoChklCrHp-XRz9waVmU4q0lh3vCSJMB9HB6gyPe3vL5kdiQF22lWvD?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/YaT7rsOPb_nV14XKEFFBNuAVOxPykCd-sP8Ey53Xv7IfVLoABNJeWomhw3rcdAcyLmFMSsvaQzpvES72oAVA9BuvksPpifuKBygLA-lNoKShOhuKV8dJ0WLMM1jZSaZqv8P0H2zbi59MAmPDyyzCjuSxiUUS6g5GKaEkq3VHb0cqNhLhaCvnfXsnOK9qtEsG?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/g-MwVs3Hb0qB5QXj58ylVJDayQ9iWWfN3X7iMyU_Di52b77WZSxvT5UJPorXyrEYEEmvirFbqlgNR_4_LoVU4nWiyaP3VW83ShN6QVvafx_2tHV-PVYCXdM_qSwM9XYcj5aqSCbtep_LDR4lbvBNXJkcEr5XTvAgodOkEisatAk7dQMRzpU_x-z2g2d4lm_1?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/nHt7pON1TfcMnyhMtEQ5uqAJdGG8Q709mCCsS1uLN-yHR34VJS0UTygr0O6sTIMJaej9jQldhFBZ-zn_qTd0RePRiy7ewmp1w6oc0eGdBZJoz71N7-BZIdwo2FhjpqjTt5677UcKlg-ZS1McelPBJFTtIbr4OvaAz6YBGytLGQmmPKRdTYEPg_QGhOgDbWs6?purpose=fullsize)

---

## 第一层：Cork

负责：

> **空间感**

用于：

* Board 背景
* Section
* Desktop Pinboard

风格：

```text
真实软木板
     ↓
降低写实程度
     ↓
现代扁平化
```

不要做成：

❌ 老式办公室软木板。

应该是：

✅ 高级数字产品。

---

# 14. 第二层：Glass

负责：

> **现代感**

用于：

* Toolbar
* Header
* Sidebar
* Settings
* Quick Add
* Windows Floating Widget

例如：

```text
╭───────────────────────────╮
│ 📌 My Board          ⋮    │
│                           │
│                           │
╰───────────────────────────╯
```

玻璃：

```text
Blur
Transparency
Subtle Border
Soft Shadow
```

---

# 15. 第三层：Paper Card

这是最重要的。

Card 应该像：

> **真的有一张纸钉在软木板上。**

例如：

```text
       📌
   ╭───────────────╮
   │               │
   │ 修改个人简历   │
   │               │
   │ ○ Today       │
   │               │
   ╰───────────────╯
```

允许轻微：

```text
rotation
shadow
texture
```

但不要过度拟物。

---

# 16. 第四层：Pin

图钉是 Memodo 非常重要的品牌元素。

建议固定视觉：

```text
📌
```

但实际 UI 用真正的 3D/2.5D Pin。

不同 Card 可以：

```text
Red
Yellow
Blue
Green
```

但颜色不要太多。

---

# 17. 三种视觉主题

不要让用户只能选一种。

设置：

```text
Appearance

○ Cork
○ Glass
○ Hybrid
```

---

## Cork

```text
Cork Board
+
Paper Card
+
Pin
```

适合：

> 桌面 Widget。

---

## Glass

```text
Glass Board
+
Paper Card
+
Pin
```

适合：

> Windows / Android App。

---

## Hybrid

推荐默认。

```text
Glass Container
       ↓
Subtle Cork Texture
       ↓
Paper Card
       ↓
Pin
```

这是 Memodo 的**品牌视觉**。

---

# 18. Windows Main App

整体：

```text
╭────────────────────────────────────────────╮
│ 📌 Memodo                         🔍  ⚙   │
├───────────────┬────────────────────────────┤
│               │                            │
│ Boards        │                            │
│               │        BOARD               │
│ My Boards     │                            │
│               │    📌          📌          │
│ Work          │                            │
│ Personal      │  ┌────────┐               │
│ Projects      │  │ Todo   │        📌     │
│ Ideas         │  └────────┘               │
│               │               ┌─────────┐ │
│ Today         │               │ Memo    │ │
│ Inbox         │               └─────────┘ │
│               │                            │
└───────────────┴────────────────────────────┘
```

---

# 19. Windows Desktop Widget

这是产品第一核心体验。

![Image](https://images.openai.com/static-rsc-4/9WdPyk5gEwhr5SJv-VcU6s6GQryslekjmysmNvlLWJHqksmU7ISgK014AtQijKmUxl1ZEq8x0WbSeqLM71rgiGhy2VVVeS1Sl9PhV6abZ6P8AAg5e8yQ0b43iVTg94ojG0CBDsa5flKp6YJzRjuqLX0NS0uLyyvQzTGjKrP4zh13lXYvcl2MX2bSdLRDcV6t?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/ccvMP59ZeMF6bLgI4zs1BpXJeAS97kBiyvhDjoRJpAD8KrPyvA9-ONDWmbwVPX1GwSg_F4UgUuKxoKRwfUKSyInh_w_sUPZ1vJnGgKD-A2FgDeOeJOEZ-2LVNp9slWImbkgMxoiA1BoChklCrHp-XRz9waVmU4q0lh3vCSJMB9HB6gyPe3vL5kdiQF22lWvD?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/dHr9-9i27DnP6-fmgLb_HLSOZoBLM6YLnxg0MVPP2Sfkepyr0GV7IPAgPwZb0e3XqK8L5uOS1e16temeEAPG-aKoMbdH5A-rZ9McO55wzrQwzu5YzN1EnZugT1F7SYEJHxnCUngaHFSxJq7jAseeQLkPb_-XUi9ZwaoXWPgmP0TiCd3Ls9kQcYXmeXiPDw1o?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/vsVIF361Fw38kZCecN9RMzGjap0hSBnnLrBiyfCU6Jcl79-5ajtS_8Shp1vrcyJKWiBStCNGfPNYCyQ9MY5oXTe74NiWjCAY0YZ4dlTUYz_G5Yu23Ul5vju3TqKhG5hoMw-aHlf5oWBzgfbHJ2031bhukozEfq-qFcZdQekRZP6jMjIPzky9zLo2tZIpZux2?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/FY9RH8Eh-KKnOJ09TtXZ0r7ZH4caoXwggy3S8iaAoBVHMvGB_cu7DBlvDAfX1B9IIU5BEJ2NIVFcm3RWLsUdfHIGCFCT2STjDT4GUYychRGTVnTVZxUbVBtFT-bXgYxOohKzhKBILx5WRrgy40VYSCd1hut0Na2d1T11yWlaiiDwKVhlr8P2eBn5hC1yJRZP?purpose=fullsize)

![Image](https://images.openai.com/static-rsc-4/RsfIkgJZe1RcSvGZ_sJ0i8NvGh4eYr6VIlhF-x6CqOgKLrFMPGGJKLD-_32_PnVAc3XV6lA7hmTATF7HfWJojwuGmPCjbyyxxIS_LATbLWMFTt-6pqFx8B53H3mD26PZkwulA1jgoKhuddrrkHPWmTkkt5PSp8nJi-NbD06UrnZ26E6xL3_HqxEb4fLJ_4uk?purpose=fullsize)

启动 Windows：

```text
Wallpaper
──────────────────────────────

       ╭─────────────────────╮
       │ 📌 WORK          ⋮  │
       │                     │
       │    📌              │
       │  ╭─────────────╮   │
       │  │ 修改简历     │   │
       │  │             │   │
       │  │ ○ Today     │   │
       │  ╰─────────────╯   │
       │                    │
       │          📌        │
       │       ╭─────────╮ │
       │       │ 更新网站 │ │
       │       ╰─────────╯ │
       ╰─────────────────────╯
```

---

# 20. Windows Widget 必须支持

### P0

* 无边框
* Move
* Resize
* Card Drag
* Card Resize
* Card Rotation
* Todo Complete
* Add
* Edit
* Delete
* Lock
* Always On Top
* Position Persistence

### P1

* Cork / Glass
* Opacity
* Click-through
* Multiple Widgets
* Hotkey
* Tray
* Startup
* Notification

### P2

* Image
* Screenshot
* Link
* AI
* OCR

---

# 21. Windows Tray

```text
📌 Memodo
```

菜单：

```text
Show Board
Hide Board
New Todo
New Memo
Sync Now
Settings
Exit
```

---

# 22. Windows 快捷键

推荐：

```text
Ctrl + Alt + M
```

显示 / 隐藏 Widget。

```text
Ctrl + Alt + N
```

New Todo。

```text
Ctrl + Alt + Shift + M
```

New Memo。

```text
Ctrl + K
```

Search。

快捷键必须可以设置。

---

# 23. Android App

Android 不做无限 Canvas。

使用：

```text
Adaptive Grid
+
Card
```

例如：

```text
┌─────────────────────────┐
│ 📌 My Board         ⋮   │
│                         │
│ ┌─────────┐ ┌─────────┐ │
│ │ 📌      │ │ 📌      │ │
│ │ 修改简历 │ │ 更新网站 │ │
│ │         │ │         │ │
│ │ ○ Today │ │ ○ 18:00 │ │
│ └─────────┘ └─────────┘ │
│                         │
│ ┌─────────────────────┐ │
│ │ 📌 面试准备          │ │
│ └─────────────────────┘ │
│                         │
│                      ＋  │
└─────────────────────────┘
```

---

# 24. Android Home Widget

重点：

```text
2×2
4×2
4×4
```

例如：

```text
┌──────────────────────────┐
│ 📌 TODAY                 │
│                          │
│ ☐ 修改个人简历            │
│ ☐ 更新网站                │
│ ☐ 面试准备                │
│                          │
│                  ＋       │
└──────────────────────────┘
```

Widget 不需要完整 Board。

重点是：

> **快速查看 + 快速完成。**

---

# 25. Android 与 Windows 如何同步“图钉板”

这是一个非常关键的问题。

不要同步：

```text
Windows:
x=513
y=273
```

到 Android。

同步：

```text
Board
Section
Card
Todo
Memo
```

Windows：

```text
Free Canvas
```

Android：

```text
Adaptive Grid
```

例如：

```text
              Card A
                 │
        ┌────────┴────────┐
        │                 │
     Windows           Android
        │                 │
    x/y/size          grid/order
```

---

# 26. Inbox

这是必须有的功能。

用户不应该每次记录东西都选择 Board。

流程：

```text
Quick Add
 ↓
Inbox
 ↓
以后整理
 ↓
拖到 Board
```

这会大幅提升使用体验。

---

# 27. Today

专门展示：

```text
Today
```

包括：

* 今天 Todo
* 逾期
* Reminder
* 最近 Memo

但不要破坏 Board 模式。

---

# 28. Quick Capture

这是第二个核心体验。

Windows：

```text
Ctrl + Alt + N
```

弹：

```text
┌─────────────────────────────┐
│ What do you want to remember?│
│                             │
│ >                             │
│                             │
│ Todo   Memo   Idea           │
└─────────────────────────────┘
```

Enter：

> 立即保存。

---

# 29. Card 编辑

点击：

```text
Card
 ↓
Edit
```

右侧或弹出：

```text
Title
Content

Type
Todo / Memo / Idea

Due
Reminder

Board
Section

Color
Pin
```

---

# 30. Todo 功能

Todo 最基础：

```text
○ Todo
```

完成：

```text
☑ Todo
```

支持：

* Priority
* Due Date
* Reminder
* Repeat
* Completed

---

# 31. Memo 功能

Memo 不需要复杂 Markdown。

第一版：

```text
Title
Body
Tags
```

以后再：

```text
Markdown
Images
Links
Attachments
```

---

# 32. Checklist

例如：

```text
网站发布

☑ 首页
☑ Blog
☐ SEO
☐ Analytics
```

Checklist 本质上还是 Card。

---

# 33. Board 操作

支持：

```text
Create Board
Rename
Delete
Duplicate
Archive
Change Theme
```

---

# 34. Board Canvas

Windows：

```text
Infinite Canvas
```

操作：

```text
Wheel
→ Zoom

Middle Drag
→ Pan

Space + Drag
→ Pan

Card Drag
→ Move
```

---

# 35. Section

例如：

```text
WORK

┌────────────────────────────┐
│                            │
│  📌 Todo        📌 Memo     │
│                            │
└────────────────────────────┘
```

Section 可以：

* Rename
* Move
* Resize
* Collapse
* Delete

---

# 36. Card 拖拽体验

这是 UI 的灵魂之一。

正常：

```text
Card
```

Hover：

```text
scale 1.01
shadow ↑
```

Drag：

```text
scale 1.03
shadow ↑↑
zIndex ↑
```

Drop：

```text
spring animation
```

用户应该感觉：

> **拿起一张纸，然后把它钉到另一个地方。**

---

# 37. Card 旋转

随机轻微：

```text
-2°
-1°
0°
+1°
+2°
```

绝对不要：

```text
-10°
+15°
```

否则很快变成杂乱的 Pinterest 风格。

---

# 38. Card 颜色

第一版：

```text
Paper
Cream
Yellow
Blue
Green
Pink
```

建议默认：

```text
Paper / Cream
```

颜色是辅助分类，不是视觉噪音。

---

# 39. Dark Mode

必须支持。

但不要简单：

```text
黑色背景
+
白色 Card
```

应该：

```text
Dark Glass
+
Dark Cork
+
Muted Paper
```

Card 仍然保持一定纸张质感。

---

# 40. Animation

只需要微动画。

### Add Card

```text
scale 0.95
→
1
```

### Complete

```text
checkbox
→
strike
→
subtle fade
```

### Delete

```text
slight shrink
→
fade
```

### Drag

```text
lift
```

原则：

> **有质感，不要炫技。**

---

# 41. Sync 架构

统一：

```text
SyncManager
      │
      ├── LocalOnly
      ├── WebDAV
      ├── OSS
      └── Server
```

接口：

```text
ISyncProvider
```

---

# 42. 第一种：Local Only

```text
SQLite
```

完全离线。

适合：

> 单设备用户。

---

# 43. 第二种：WebDAV

用户可以填：

```text
URL
Username
Password
```

支持：

* 坚果云
* NAS
* Nextcloud
* 其他 WebDAV

这个非常适合 Memodo。

---

# 44. 第三种：OSS

支持：

```text
S3 compatible
```

因此不要把代码写死成：

```text
Alibaba OSS
```

而是：

```text
S3Provider
```

可以兼容：

* 阿里云 OSS
* MinIO
* AWS S3
* 其他 S3 Compatible Storage

---

# 45. 第四种：Self-hosted Server

用户自己部署：

```text
Memodo Server
```

服务器：

```text
FastAPI
PostgreSQL
Docker
```

---

# 46. Server 功能

第一版：

```text
Authentication
Device
Board
Card
Sync
Backup
```

API：

```text
POST /auth/login

GET /boards

GET /cards

POST /sync/push

POST /sync/pull

GET /sync/status
```

以后再做：

```text
Share
Collaboration
Team
```

---

# 47. 同步机制

不要第一版做 CRDT。

采用：

```text
id
updatedAt
revision
deviceId
deletedAt
```

策略：

```text
Last Write Wins
+
Tombstone
```

以后用户量大了，再升级 CRDT。

---

# 48. Sync Queue

本地：

```text
SyncQueue
```

例如：

```text
CREATE Card A
UPDATE Card B
DELETE Card C
```

网络恢复：

```text
Queue
 ↓
Sync
 ↓
Server
```

---

# 49. 删除机制

不要立即物理删除。

使用：

```text
deletedAt
```

同步完成以后：

```text
Garbage Collection
```

再清理。

否则：

```text
Windows 删除
Android 不知道
```

会导致数据重新出现。

---

# 50. 服务器是不是必须？

**不是。**

这是 Memodo 很大的优势。

用户可以：

```text
Local
```

也可以：

```text
WebDAV
```

也可以：

```text
OSS
```

也可以：

```text
Self-hosted
```

甚至未来：

```text
Memodo Cloud
```

---

# 51. Settings

设置必须分组。

## Appearance

```text
Theme
Cork / Glass / Hybrid

Dark Mode

Card Style

Animation
```

---

## Desktop

```text
Start with Windows

Show Widget

Always on Top

Opacity

Lock Board

Click-through

Default Widget Size
```

---

## Sync

```text
Sync Provider

Local
WebDAV
OSS
Server

Sync Now

Last Sync

Conflict
```

---

## Notification

```text
Todo Reminder
Daily Summary
Overdue
```

---

## Data

```text
Export
Import
Backup
Restore
Clear Local Data
```

---

# 52. 数据导出

必须支持：

```text
Export JSON
```

以后：

```text
Markdown
CSV
```

至少第一版：

> JSON 全量备份。

这样即使 Memodo 未来停止开发，用户的数据也不会被锁死。

---

# 53. 安全

密码：

> 不允许明文存储。

Windows：

```text
Windows Credential Manager
```

Android：

```text
Android Keystore
```

服务器：

```text
HTTPS
Password Hash
Token
```

---

# 54. 产品 MVP

我建议第一版不要贪。

## Memodo 0.1

必须：

```text
Board
Card
Todo
Memo
Checklist
Idea

Windows Canvas

Windows Desktop Widget

SQLite

Quick Add

Drag
Resize
Pin

Tray

Android App

Android Widget
```

---

# 55. Memodo 0.2

加入：

```text
Today
Inbox
Reminder
Search
Dark Mode
Cork
Glass
```

---

# 56. Memodo 0.3

加入：

```text
WebDAV
```

---

# 57. Memodo 0.4

加入：

```text
OSS / S3
```

---

# 58. Memodo 0.5

加入：

```text
Self-hosted Server
```

---

# 59. Memodo 0.6

加入：

```text
Image
Link
Attachment
```

---

# 60. Memodo 1.0

最终：

```text
Memodo
│
├── Windows
│   ├── App
│   ├── Desktop Widget
│   └── Tray
│
├── Android
│   ├── App
│   └── Home Widget
│
├── Local-first
│
├── WebDAV
│
├── S3 / OSS
│
└── Self-hosted Server
```

---

# 61. AI 放在哪里？

**不要第一版加入 AI。**

但架构预留：

```text
AIService
```

以后可以：

### AI 自动分类

```text
“明天提醒我给客户发邮件”
          ↓
Todo
Due = tomorrow
```

### AI 摘要

Memo：

```text
长文本
 ↓
Summary
```

### AI 整理 Board

```text
Inbox
 ↓
AI
 ↓
Work
Personal
Ideas
```

### AI Todo

```text
“帮我安排一下这个项目”
```

自动拆成：

```text
☐ PRD
☐ UI
☐ Backend
☐ Testing
```

这些都应该是 **Memodo 未来的 AI 增长点**。

---

# 62. 最终产品视觉

我建议把 Memodo 的品牌视觉定成：

## Primary

**Warm Cork**

## Secondary

**Frosted Glass**

## Content

**Paper Card**

## Brand Element

**Push Pin**

## UI Philosophy

> **Warm + Calm + Physical + Digital**

不要走：

```text
Notion
Linear
Todoist
```

那种纯 SaaS 风格。

也不要走：

```text
老式便签软件
```

而是：

> **Physical Memory Objects × Modern Digital UI**

---

# 63. 最终桌面效果

我希望最终用户看到的是：

```text
                 Windows Desktop

        ┌──────────────────────────────────┐
        │                                  │
        │       📌 WORK                    │
        │                                  │
        │   📌                             │
        │  ┌───────────────┐               │
        │  │ 修改简历       │               │
        │  │               │        📌     │
        │  │ ○ Today       │      ┌──────┐ │
        │  └───────────────┘      │网站  │ │
        │                         └──────┘ │
        │                                  │
        │                📌                │
        │           ┌───────────────┐      │
        │           │ AI Course     │      │
        │           │ Generator     │      │
        │           └───────────────┘      │
        │                                  │
        └──────────────────────────────────┘
```

它不是“一个打开的软件”。

而是：

> **电脑桌面上的第二层信息空间。**

---

# 64. 开发顺序——这个非常重要

你现在已经有基本版，所以**不要让 Coding Agent 直接大改全部代码**。

按照这个顺序：

```text
                 Existing Memodo
                       │
                       ▼
              Phase 0 Architecture
                       │
                       ▼
              Phase 1 Design System
                       │
                       ▼
              Phase 2 Board + Card
                       │
                       ▼
           Phase 3 Windows Desktop Widget
                       │
                       ▼
              Phase 4 Windows Polish
                       │
                       ▼
                 Phase 5 Android
                       │
                       ▼
              Phase 6 Android Widget
                       │
                       ▼
                Phase 7 WebDAV
                       │
                       ▼
                 Phase 8 OSS
                       │
                       ▼
              Phase 9 Server
                       │
                       ▼
              Phase 10 Integration
                       │
                       ▼
                  Memodo 1.0
```

---

# 65. Coding Agent 的工作纪律

这一点我特别建议加入你的开发规范。

Agent **不能**：

* 为了改 UI 随便改数据库
* 为了 Widget 重写整个项目
* 把 Windows 和 Android 强行统一 UI
* 把同步代码写进 View
* 把 Win32 API 写进 ViewModel
* 把服务器代码和客户端耦合
* 一次修改几十个模块
* 没测试就说完成

每个 Phase 必须：

```text
1. Inspect
2. Plan
3. Implement
4. Test
5. Screenshot
6. Fix
7. Report
```

---

# 66. 每个阶段 Agent 必须提交

例如：

```text
Phase 3 completed

Changed:
- DesktopWidgetWindow
- WidgetViewModel
- WidgetService

Added:
- AlwaysOnTop
- Resize
- Drag
- Persistence

Tests:
- Restart persistence ✓
- Drag ✓
- Resize ✓
- Multi-monitor ✓

Known issues:
- ...
```

这样你就不会再次出现：

> “Agent 好像做了很多，但是我不知道它到底做到哪里了。”

---

# 67. 最终项目目录建议

如果逐渐拆成多端：

```text
memodo/
│
├── docs/
│   ├── PRD.md
│   ├── SPD.md
│   ├── SPEC.md
│   ├── UI_SPEC.md
│   ├── ARCHITECTURE.md
│   ├── SYNC_SPEC.md
│   ├── SERVER_SPEC.md
│   └── ROADMAP.md
│
├── windows/
│
├── android/
│
├── server/
│
├── shared/
│   ├── schema/
│   └── api/
│
└── assets/
    ├── icons/
    ├── pins/
    ├── textures/
    └── illustrations/
```

---

# 68. 最终验收标准

### Windows

* [ ] Main App
* [ ] Board
* [ ] Infinite Canvas
* [ ] Card
* [ ] Drag
* [ ] Resize
* [ ] Rotation
* [ ] Pin
* [ ] Desktop Widget
* [ ] Always On Top
* [ ] Lock
* [ ] Click-through
* [ ] Tray
* [ ] Startup
* [ ] Hotkey
* [ ] Persistence

### Android

* [ ] App
* [ ] Board
* [ ] Card
* [ ] Todo
* [ ] Memo
* [ ] Checklist
* [ ] Idea
* [ ] Home Widget
* [ ] Quick Complete

### Sync

* [ ] Local
* [ ] WebDAV
* [ ] OSS/S3
* [ ] Server
* [ ] Offline
* [ ] Queue
* [ ] Conflict
* [ ] Delete Sync

### Server

* [ ] Auth
* [ ] Device
* [ ] Board
* [ ] Card
* [ ] Sync
* [ ] Backup
* [ ] Docker Deployment

### Visual

* [ ] Cork
* [ ] Glass
* [ ] Paper
* [ ] Pin
* [ ] Dark Mode
* [ ] Animation
* [ ] Empty State
* [ ] Error State
* [Loading State

---

# 69. 我最建议你现在确定的最终产品模型

最后把整个 Memodo 压缩成一句话：

> **Memodo 是一个 Local-first 的个人数字图钉板，把 Todo、Memo、Checklist 和 Idea 变成可以自由摆放的纸质 Card，并通过 Windows Desktop Widget 和 Android Home Widget 让这些信息始终出现在用户身边。**

技术上：

```text
                 ┌─────────────┐
                 │   Memodo    │
                 └──────┬──────┘
                        │
                Local-first Core
                        │
             ┌──────────┴──────────┐
             │                     │
         Windows                 Android
             │                     │
        WPF / Win32          Kotlin / Compose
             │                     │
       Desktop Widget         Home Widget
             │                     │
             └──────────┬──────────┘
                        │
                   Sync Layer
                        │
          ┌─────────────┼─────────────┐
          │             │             │
       WebDAV          S3          Server
          │             │             │
          └─────────────┴─────────────┘
```

视觉上：

```text
                  MEMODO
                     │
              ┌──────┴──────┐
              │             │
           CORK           GLASS
              │             │
              └──────┬──────┘
                     │
                  PAPER
                     │
                    📌
                     │
                   CARD
```

**我认为这套方案比“做一个漂亮的 TodoList”更有产品辨识度。**尤其是 **Windows Desktop Widget + 自由排列 Card + Cork/Glass/Paper/Pin**，这是整个 Memodo 最值得投入精力的地方。

你现在既然已经有一个基本版，**下一步不要继续零散地给 Agent 发需求**。最合理的是把这套方案正式整理成一套开发文档，然后给 Coding Agent 一个**“Memodo V2 产品重构总提示词”**，要求它先审计现有代码，再严格按 Phase 执行。这样可以最大限度避免 Vibecoding 越改越乱。
