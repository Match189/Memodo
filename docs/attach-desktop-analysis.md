# 「附着桌面」功能 — 结论与移除记录

日期：2026-09-01　|　平台：Windows 11 25H2（Build 26200）　|　**最终结论：功能已整体移除，组件保持普通窗口方案**

用户裁定：经过 v1-v4 四轮方案迭代与实机实验，确认该功能在 WPF 上无法可靠实现，按用户决定移除附着桌面功能，钉板组件保持普通窗口（置顶开关照常可用）。

## 已证伪的方案（勿再尝试）

| 版本 | 方案 | 结果 | 根因 |
|---|---|---|---|
| v1 | WorkerW 挂载（Flutter 移植） | 必失败 | `GetParent` 对非 WS_CHILD 窗口返回 owner 而非 parent，验证恒假 |
| v2 | GetAncestor 验证 + 轮询重试 | `err=87` | 壁纸类软件占用的顶层 WorkerW 拒绝一切外部 SetParent（本机 24 个 WorkerW 中 Z 序最顶 2 个全拒） |
| v3 | 清 owner/Topmost + WS_CHILD 重试 | 仍 87 | 与子窗口风格无关，目标窗口无条件拒绝 |
| v3.5 | 只挂"可见且覆盖屏幕"的 WorkerW | 窗口消失 | 本机 24 个 WorkerW 全部不可见（136×39 残留层），挂进不可见父窗子窗口跟着不可见 |
| v3.9 | 兜底挂 Progman 本体 | 内容透明 | 截屏实证：**WPF 窗口挂为其他进程窗口的子窗口后，D3D 内容不再呈现**——WPF 表现层依赖顶层重定向表面 |
| v4 | 底层模式（HWND_BOTTOM + 失焦沉底） | 技术可用，体验不达标 | Win+D 会最小化组件；用户裁定放弃 |

核心教训：**SetParent 跨进程挂载对 WPF 不可行**（内容不渲染 + 目标 WorkerW 被壁纸软件占用/不可见）。
若未来重启此功能，需换非 WPF 表现层（Win32/Qt/CEF 独立进程）或改用 Renderer 类方案。

## 移除清单（本次）

- `DesktopWidgetWindow`：附着菜单项、TryAttachDesktop/ToggleAttachDesktop、失焦沉底逻辑全部移除；
- `WindowChrome`：AttachToDesktop/SinkToBottom/诊断日志全部移除，仅保留 `DetachFromDesktop`
  ——用于把旧版本可能挂在 Progman/WorkerW 下的窗口解回顶层（升级兼容）；
- `SettingsStore.WidgetAttachDesktop` 属性移除（旧 settings.json 中的残留键会被 JSON 反序列化自动忽略）；
- locales 的 `widget_attach`、`attach_desktop_fail` 键移除；
- 诊断脚本 test-attach.ps1 / run-test.cmd 删除。

## 诊断日志通道（历史）

`crash.log` 的 `[attach-desktop]` 行不再产生，历史行可删。
