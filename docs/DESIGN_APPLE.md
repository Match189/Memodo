# Memodo · Apple 风格全局美化计划

> 目标：在保留「软木+便签+图钉」品牌的前提下，整体交互质感对齐 Apple HIG：
> 克制、留白、层级清晰、动效轻。适用于 Windows 主窗口、桌面组件、Android App。
> 执行顺序：P1 令牌 → P2 列表 → P3 外壳 → P4 组件 → P5 Android。每阶段构建+截图验收。

---

## 0. 设计原则（HIG 三条）

1. **Clarity 清晰**：内容优先，字号层级分明，色不过三
2. **Deference 退让**：界面元素退后（细边线、无重底色），让便签和文字站到前面
3. **Depth 层级**：用「毛玻璃 + 轻投影 + 圆角」表达层级，不用重描边

## 1. 设计令牌（全局统一，替换现有散落色值）

### 1.1 颜色（iOS 系统灰阶 + Memodo 暖橙为唯一 tint）

| 令牌 | 浅色 | 深色 | 用途 |
| --- | --- | --- | --- |
| Background | `#F2F2F7`（iOS 分组灰） | `#1C1C1E` | 窗口底 |
| Surface（卡片/列表容器） | `#FFFFFF` | `#2C2C2E` | 列表容器、便签缺省纸 |
| SurfaceElevated | `#FFFFFF` | `#3A3A3C` | 弹窗/浮层 |
| Separator（发丝线） | `1px #3C3C43 @12%` | `1px #545458 @60%` | 行分隔、细边框 |
| Label（主文字） | `#1C1C1E` | `#FFFFFF` | 标题/正文 |
| SecondaryLabel | `#3C3C43 @60%` | `#EBEBF5 @60%` | 副文字/说明 |
| TertiaryLabel | `#3C3C43 @30%` | `#EBEBF5 @30%` | 占位符 |
| Tint（Memodo 品牌橙） | `#D4763B` | `#E89A62` | 勾选/主按钮/选中态 |
| Danger | `#FF3B30`（iOS 红） | `#FF453A` | 删除 |
| Fill（输入框/次级填充） | `#767680 @12%` | `#767680 @24%` | 输入框底、次级按钮底 |

### 1.2 字体（Windows 全用系统字体栈，不引外部字体）

| 层级 | 字体/字号/字重 |
| --- | --- |
| LargeTitle（页面大标题） | Segoe UI Variable / Segoe UI · 28px · Semibold |
| Title（分区标题） | 20px · Semibold |
| Body（正文/列表行） | 14px · Regular |
| Subhead（副文字） | 13px · Regular |
| Footnote（脚注/时间） | 12px · Regular · 次要色 |
| 便签正文（品牌保留） | 楷体 13.5px（仅在便签上，列表内不用） |

### 1.3 圆角 / 间距 / 投影

- 圆角阶：**8**（按钮/输入框）、**12**（列表容器/卡片）、**16**（弹窗/组件窗）、**20**（组件整体）
- 间距阶：4 / 8 / 12 / 16 / 20 / 24 / 32
- 投影（唯一三档）：
  - E1 悬停：`Y2 B8 12%`
  - E2 卡片：`Y3 B12 10%`
  - E3 弹窗：`Y8 B28 18%`
- 动效：150–250ms，ease-out；按压 scale 0.98；悬停卡片仅投影+0.5% 上移

---

## 2. P1 · 令牌落地（Windows ThemeService/App.xaml）

- ThemeService 增加 `Label / SecondaryLabel / TertiaryLabel / Separator / Fill / SurfaceElevated`，
  替换现有 `TextPrimary/TextSecondary/CardBorder/SubtleText` 语义（保留旧键别名一个版本）
- Background 切 `#F2F2F7`；Surface 白；边线全部换发丝线（1px 12% 黑），删除所有 `#FF……` 实色描边
- Accent 保持暖橙（品牌），但选中态从「实色填充」改为「**tint 12% 圆角块 + tint 图标**」（macOS 侧栏样式）

## 3. P2 · 列表页 → iOS Inset Grouped（待办/备忘/设置主战场）

- 每页列表改为 **单个大圆角容器（12）**，行与行之间用发丝线分隔（不再是每行独立卡片）
- 行规范：高 44–48、左勾选右操作、内边距 16；勾选完成 → 行文字 SecondaryLabel + 划线
- 勾选框换成 **Apple 圆形勾**：20px 圆环，选中=tint 实心圆+白色对勾（自绘模板，双端同形）
- 输入行：填充式输入框（Fill 底、无描边、圆角 8、聚焦 tint 2px 内环）
- 空状态：居中 SF 风格线性图形 + 两行文案（无数据/全部完成）
- 设置页：iOS「分组设置」式——组标题（Footnote 大写灰）在容器外，设置行在圆角容器内

## 4. P3 · 外壳（标题栏/侧栏/按钮/弹窗）

- 标题栏：40px、标题 13px Subhead、按钮 hover 灰 8%；关闭钮 hover 变 iOS 红（macOS 红绿灯语感）
- 侧栏：64px 栏保留；选中=tint 12% 圆角块 + tint 图标；hover=8% 黑
- 按钮体系三档：**Filled**（tint 底白字）、**Plain**（tint 文字）、**Gray**（Fill 底 Label 字）；
  高 32、圆角 8；按压 scale 0.98
- 弹窗（编辑/快速添加/确认）：圆角 16 + SurfaceElevated + E3 投影 + 背景压暗 20%；
  动作改为右下「取消(Plain) / 确定(Filled)」
- 页面切换：150ms 淡入+8px 上移

## 5. P4 · 桌面组件精修

- 便签投影统一 E2；圆角 16；便签正文字号 13.5 → 13，行距 1.5
- 列表模式：行间发丝线、勾选框换圆形勾（同 P2）、行高 40
- 板面：软木噪点透明度 8%→6%（更 Apple 的克制）、便签 hover 去缩放只留投影+摆正
- 头部：标题 13 Semibold、进度「N/M」Footnote、按钮图标 12px、间距 8

## 6. P5 · Android 对齐

- Material3 动态色关闭，锁定 seed = Memodo 橙（与 Windows tint 一致）
- 列表容器圆角 16、行高 52、分隔线用 12% 黑发丝线（与 Windows 同形）
- 深色模式：跟随系统（已是）；主题色随 seed 自动生成深浅

## 7. 验收清单（每阶段截图对照）

- [ ] 浅/深两态下：主窗口三页 + 组件两模式 + 三类弹窗，共 10+ 张截图
- [ ] 全应用只出现：灰阶 + 橙 + 红（删除）三系颜色
- [ ] 所有圆角 ∈ {8,12,16,20}；所有间距 ∈ 间距阶；投影只有三档
- [ ] 勾选/完成交互在 浅色+深色 下同样清晰
- [ ] Windows/Android 勾选框同形（圆形勾）

## 8. 风险与说明

- 「便签楷体」是品牌资产，保留在**便签上**；列表页一律系统字体（Apple Clarity 优先）
- 软木纹理保留但降噪声（6%），避免与 Apple 克制感冲突
- 不引第三方 UI 库；全部自绘模板/样式，构建零新依赖
