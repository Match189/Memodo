import 'dart:async';

import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../board/base_card.dart';
import '../board/board_controller.dart';
import '../board/board_theme.dart';
import '../board/pin_widget.dart';
import '../models/memo.dart';
import '../desktop/widget_settings.dart';
import '../desktop/win32_window_style.dart';
import '../models/task.dart';
import '../state/memo_list_model.dart';
import '../state/task_list_model.dart';
import '../theme/app_theme.dart';
import '../theme/theme_settings.dart';

/// 待办卡片窗口的原生窗口标题（win32 按它找 HWND）。
const widgetWindowTitle = '待办小组件';

/// 备忘卡片窗口（双卡片布局）的原生窗口标题。
const memoWidgetWindowTitle = '备忘小组件';

/// 子窗口内容种类：todo=待办卡片；memo=备忘卡片；both=单卡片合并显示。
typedef WidgetKind = String; // 'todo' | 'memo' | 'both'

/// 桌面小组件应用：独立 Flutter 引擎，读同一个 SQLite 库。
///
/// 架构约定（防跨进程崩溃）：对本窗口的原生操作（无边框/置顶/材质/附着桌面）
/// 一律在本进程内执行；主进程只通过 desktop_multi_window 发命令。
class WidgetWindowApp extends StatelessWidget {
  const WidgetWindowApp({
    super.key,
    required this.windowId,
    required this.kind,
    required this.taskModel,
    required this.memoModel,
    required this.widgetSettings,
    required this.themeSettings,
    required this.boardController,
  });

  /// 子窗口自己的 id（由 main() 的 multi_window 参数传入）。
  final int windowId;
  final WidgetKind kind;

  final TaskListModel taskModel;
  final MemoListModel memoModel;
  final DesktopWidgetSettingsModel widgetSettings;
  final ThemeSettingsModel themeSettings;
  final BoardController boardController;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: taskModel),
        ChangeNotifierProvider.value(value: memoModel),
        ChangeNotifierProvider.value(value: widgetSettings),
        ChangeNotifierProvider.value(value: themeSettings),
        ChangeNotifierProvider.value(value: boardController),
      ],
      child: Builder(builder: (context) {
        final appearance = context.watch<ThemeSettingsModel>();
        return MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: AppTheme.light(appearance.seedColor),
          darkTheme: AppTheme.dark(
            appearance.seedColor,
            amoled: appearance.amoledBlack,
          ),
          themeMode: appearance.themeMode,
          home: WidgetWindowPage(windowId: windowId, kind: kind),
        );
      }),
    );
  }
}

class WidgetWindowPage extends StatefulWidget {
  const WidgetWindowPage({
    super.key,
    required this.windowId,
    required this.kind,
  });

  final int windowId;
  final WidgetKind kind;

  @override
  State<WidgetWindowPage> createState() => _WidgetWindowPageState();
}

class _WidgetWindowPageState extends State<WidgetWindowPage> {
  final _inputController = TextEditingController();
  Timer? _rectReportTimer;
  bool _attachedToDesktop = false;

  String get _windowTitle =>
      widget.kind == 'memo' ? memoWidgetWindowTitle : widgetWindowTitle;

  @override
  void initState() {
    super.initState();
    final taskModel = context.read<TaskListModel>();
    final memoModel = context.read<MemoListModel>();
    final boardController = context.read<BoardController>();

    // 主窗口广播的数据变化 → 重载列表与板卡。
    DesktopMultiWindow.setMethodHandler((call, fromWindowId) async {
      switch (call.method) {
        case 'dataChangedFromMain':
          await taskModel.load();
          await memoModel.load();
          await boardController.load();
        case 'applySurface':
          final args = (call.arguments as Map?)?.cast<String, Object?>();
          await WidgetWindowNative.setSurface(
            windowTitle: _windowTitle,
            acrylic: args?['acrylic'] == true,
            opacity: (args?['opacity'] as num?)?.toInt() ?? 90,
          );
        case 'setTopmost':
          await WidgetWindowNative.setTopmost(
            call.arguments == true,
            windowTitle: _windowTitle,
          );
        case 'attach':
          final ok = await WidgetWindowNative.attachToDesktop(
              windowTitle: _windowTitle);
          _attachedToDesktop = ok;
        case 'detach':
          await WidgetWindowNative.detachFromDesktop(
              windowTitle: _windowTitle);
          _attachedToDesktop = false;
      }
      return null;
    });

    WidgetsBinding.instance.addPostFrameCallback((_) {
      // 自套样式：在本进程内操作自己的 HWND（安全；主进程跨进程操作曾致崩溃）。
      final ws = context.read<DesktopWidgetSettingsModel>();
      WidgetWindowNative.applyFramelessAndTopmost(
        windowTitle: _windowTitle,
        alwaysOnTop: ws.alwaysOnTop,
      );
      WidgetWindowNative.setSurface(
        windowTitle: _windowTitle,
        acrylic: ws.material == WidgetMaterial.acrylic,
        opacity: ws.opacity,
      );
      if (ws.attachToDesktop) {
        unawaited(WidgetWindowNative.attachToDesktop(
            windowTitle: _windowTitle));
        _attachedToDesktop = true;
      }
      debugPrint('[widget:${widget.kind}] self style applied');
      // 位置回报（本进程 GetWindowRect 只读安全；主进程负责持久化）
      _rectReportTimer = Timer.periodic(const Duration(seconds: 10), (_) {
        _reportRect();
      });
      debugPrint('[widget:${widget.kind}] first frame rendered ✓');
      unawaited(boardController.load());
    });
  }

  void _reportRect() {
    if (!mounted || _attachedToDesktop) return;
    final r = WidgetWindowNative.getRect(windowTitle: _windowTitle);
    if (r == null) return;
    unawaited(DesktopMultiWindow.invokeMethod(0, 'widgetRect', {
      'kind': widget.kind,
      'x': r.x,
      'y': r.y,
      'w': r.w,
      'h': r.h,
    }));
  }

  @override
  void dispose() {
    _rectReportTimer?.cancel();
    _inputController.dispose();
    super.dispose();
  }

  Future<void> _notifyMainChanged() async {
    try {
      await DesktopMultiWindow.invokeMethod(0, 'dataChangedFromWidget');
    } catch (_) {}
  }

  Future<void> _add() async {
    final text = _inputController.text;
    _inputController.clear();
    if (widget.kind == 'memo') {
      await context.read<MemoListModel>().add(text, '');
    } else {
      await context.read<TaskListModel>().add(text);
    }
    await _notifyMainChanged();
  }

  Future<void> _toggle(Task task) async {
    await context.read<TaskListModel>().toggle(task);
    await _notifyMainChanged();
  }

  Future<void> _close() async {
    await _notifyMainChanged();
    try {
      await DesktopMultiWindow.invokeMethod(0, 'widgetClosed', widget.windowId);
    } catch (_) {}
    await WindowController.fromWindowId(widget.windowId).close();
  }

  @override
  Widget build(BuildContext context) {
    final taskModel = context.watch<TaskListModel>();
    final memoModel = context.watch<MemoListModel>();
    final widgetSettings = context.watch<DesktopWidgetSettingsModel>();
    final boardController = context.watch<BoardController>();
    final scheme = Theme.of(context).colorScheme;
    final showTasks = widget.kind != 'memo';
    final showMemos = widget.kind != 'todo';

    final base = widgetSettings.opacity / 100;
    final bgAlpha = widgetSettings.material == WidgetMaterial.solid
        ? 1.0
        : (base * 0.7).clamp(0.3, 1.0);
    final bg = scheme.surface.withOpacity(bgAlpha);

    final openTasks = taskModel.tasks.where((t) => !t.done).length;
    final boardMode = widgetSettings.cardStyle == 'board';

    return Scaffold(
      backgroundColor: bg,
      body: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _buildHeader(context, widgetSettings, scheme, openTasks, showMemos),
            const SizedBox(height: 8),
            if (boardMode)
              Expanded(
                child: _WidgetBoardView(
                  kind: widget.kind,
                  controller: boardController,
                  boardThemeId: widgetSettings.boardThemeId,
                  bright: Theme.of(context).brightness,
                ),
              )
            else ...[
              Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _inputController,
                      style: const TextStyle(fontSize: 13),
                      decoration: InputDecoration(
                        hintText:
                            widget.kind == 'memo' ? '快速记一条备忘…' : '快速添加…',
                        isDense: true,
                        border: const OutlineInputBorder(),
                        contentPadding: const EdgeInsets.symmetric(
                            horizontal: 10, vertical: 8),
                      ),
                      onSubmitted: (_) => _add(),
                    ),
                  ),
                  const SizedBox(width: 6),
                  SizedBox(
                    height: 34,
                    width: 34,
                    child: IconButton.filled(
                      padding: EdgeInsets.zero,
                      iconSize: 18,
                      icon: const Icon(Icons.add),
                      onPressed: _add,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              if (showTasks)
                _taskList(context, taskModel, scheme, showMemos),
              if (showMemos) _memoList(context, memoModel, scheme),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildHeader(BuildContext context, DesktopWidgetSettingsModel ws,
      ColorScheme scheme, int openTasks, bool showMemos) {
    return GestureDetector(
      onPanStart: (_) {
        if (!ws.lockPosition) {
          WidgetWindowNative.beginDrag(windowTitle: _windowTitle);
        }
      },
      behavior: HitTestBehavior.opaque,
      child: Row(
        children: [
          Icon(
            widget.kind == 'memo'
                ? Icons.sticky_note_2
                : Icons.check_circle,
            size: 18,
            color: scheme.primary,
          ),
          const SizedBox(width: 6),
          Text(
            widget.kind == 'memo' ? '备忘' : '今日待办',
            style: Theme.of(context).textTheme.titleSmall,
          ),
          const Spacer(),
          if (widget.kind != 'memo')
            Text('$openTasks 项未完成',
                style: Theme.of(context)
                    .textTheme
                    .labelSmall
                    ?.copyWith(color: scheme.outline)),
          _HeaderIcon(
            tooltip: '打开主窗口',
            icon: Icons.open_in_new,
            onTap: () => WidgetWindowNative.openMainWindow(),
          ),
          _HeaderIcon(tooltip: '关闭', icon: Icons.close, onTap: _close),
        ],
      ),
    );
  }

  Widget _taskList(BuildContext context, TaskListModel taskModel,
      ColorScheme scheme, bool shrink) {
    return Expanded(
      flex: shrink ? 3 : 1,
      child: taskModel.loading
          ? const Center(
              child: SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2)))
          : taskModel.tasks.isEmpty
              ? Center(
                  child: Text('还没有待办',
                      style: TextStyle(fontSize: 12, color: scheme.outline)))
              : ListView.builder(
                  itemCount: taskModel.tasks.length,
                  padding: EdgeInsets.zero,
                  itemBuilder: (context, index) {
                    final task = taskModel.tasks[index];
                    return InkWell(
                      onTap: () => _toggle(task),
                      child: Padding(
                        padding: const EdgeInsets.symmetric(vertical: 3),
                        child: Row(
                          children: [
                            SizedBox(
                              width: 22,
                              height: 22,
                              child: Checkbox(
                                value: task.done,
                                onChanged: (_) => _toggle(task),
                              ),
                            ),
                            const SizedBox(width: 6),
                            Expanded(
                              child: Text(
                                task.title,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(
                                  fontSize: 13,
                                  decoration: task.done
                                      ? TextDecoration.lineThrough
                                      : TextDecoration.none,
                                  color: task.done
                                      ? scheme.outline
                                      : scheme.onSurface,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
    );
  }

  Widget _memoList(BuildContext context, MemoListModel memoModel,
      ColorScheme scheme) {
    return Expanded(
      child: memoModel.memos.isEmpty
          ? Center(
              child: Text('还没有备忘',
                  style: TextStyle(fontSize: 12, color: scheme.outline)))
          : ListView.builder(
              itemCount: memoModel.memos.length,
              padding: EdgeInsets.zero,
              itemBuilder: (context, index) {
                final memo = memoModel.memos[index];
                return Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Text(
                    '📝 ${memo.title}${memo.content.isEmpty ? '' : '：${memo.content}'}',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 12),
                  ),
                );
              },
            ),
    );
  }
}

/// 图钉板模式：板卡在小组件窗口内的自由布局渲染。
/// 布局与主应用共享（同一 kv）；超出窗口的卡片会钳制在窗口内显示。
class _WidgetBoardView extends StatefulWidget {
  const _WidgetBoardView({
    required this.kind,
    required this.controller,
    required this.boardThemeId,
    required this.bright,
  });

  final WidgetKind kind;
  final BoardController controller;
  final String boardThemeId;
  final Brightness bright;

  @override
  State<_WidgetBoardView> createState() => _WidgetBoardViewState();
}

class _WidgetBoardViewState extends State<_WidgetBoardView> {
  @override
  Widget build(BuildContext context) {
    final controller = widget.controller;
    final theme = BoardThemes.resolve(widget.boardThemeId, widget.bright);
    final views = [...controller.cards]..sort(
        (a, b) => a.layout.z.compareTo(b.layout.z));

    if (views.isEmpty) {
      return Center(
        child: Text('在应用「图钉板」页钉上卡片后会显示在这里',
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 12, color: theme.sectionText)),
      );
    }

    return LayoutBuilder(builder: (context, constraints) {
      return Stack(
        clipBehavior: Clip.hardEdge,
        children: [
          for (final view in views)
            if (widget.kind == 'both' ||
                (widget.kind == 'todo' && view.record.refType == 'todo') ||
                (widget.kind == 'memo' && view.record.refType == 'memo'))
              ValueListenableBuilder<BoardCardLayout>(
                key: ValueKey(view.record.uuid),
                valueListenable: view.layoutNotifier,
                builder: (context, layout, _) {
                  // 钳制在窗口内（平台各自布局：不与主应用共享像素坐标）
                  final x = layout.x.clamp(0.0, (constraints.maxWidth - layout.w).clamp(0.0, double.infinity));
                  final y = layout.y.clamp(0.0, (constraints.maxHeight - layout.h).clamp(0.0, double.infinity));
                  return Positioned(
                    left: x,
                    top: y,
                    child: Transform.rotate(
                      angle: layout.rotationDegrees * 3.14159265 / 180,
                      child: GestureDetector(
                        onTap: () => controller.bringToFront(view),
                        child: _MiniCard(
                          layout: layout,
                          theme: theme,
                          child: view.record.refType == 'todo'
                              ? _MiniTodo(controller: controller, view: view)
                              : _MiniMemo(controller: controller, view: view),
                        ),
                      ),
                    ),
                  );
                },
              ),
        ],
      );
    });
  }
}

class _MiniCard extends StatelessWidget {
  const _MiniCard({
    required this.layout,
    required this.theme,
    required this.child,
  });

  final BoardCardLayout layout;
  final BoardThemeData theme;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: layout.w.clamp(120.0, 400.0),
      height: layout.h.clamp(80.0, 400.0),
      decoration: BoxDecoration(
        color: theme.cardSurface,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: theme.cardBorder),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: theme.dark ? 0.45 : 0.18),
            blurRadius: 8,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: child,
    );
  }
}

class _MiniTodo extends StatelessWidget {
  const _MiniTodo({required this.controller, required this.view});

  final BoardController controller;
  final BoardCardView view;

  @override
  Widget build(BuildContext context) {
    final taskModel = context.watch<TaskListModel>();
    Task? task;
    for (final t in taskModel.tasks) {
      if (t.uuid == view.record.refUuid) {
        task = t;
        break;
      }
    }
    final scheme = Theme.of(context).colorScheme;
    return InkWell(
      onTap: task == null
          ? null
          : () async {
              await taskModel.toggle(task!);
              await DesktopMultiWindow.invokeMethod(0, 'dataChangedFromWidget');
            },
      child: Padding(
        padding: const EdgeInsets.all(8),
        child: Row(
          children: [
            Icon(
              task?.done == true ? Icons.check_box : Icons.check_box_outline_blank,
              size: 18,
              color: task?.done == true ? scheme.primary : scheme.onSurface,
            ),
            const SizedBox(width: 6),
            Expanded(
              child: Text(
                task?.title ?? '（源待办已删除，可取下）',
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: 12.5,
                  decoration: task?.done == true
                      ? TextDecoration.lineThrough
                      : TextDecoration.none,
                  color: task?.done == true ? scheme.outline : scheme.onSurface,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MiniMemo extends StatelessWidget {
  const _MiniMemo({required this.controller, required this.view});

  final BoardController controller;
  final BoardCardView view;

  @override
  Widget build(BuildContext context) {
    final memoModel = context.watch<MemoListModel>();
    Memo? memo;
    for (final m in memoModel.memos) {
      if (m.uuid == view.record.refUuid) {
        memo = m;
        break;
      }
    }
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.all(8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            memo?.title ?? '（源备忘已删除）',
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(fontSize: 12.5, fontWeight: FontWeight.w600),
          ),
          if (memo != null && memo.content.isNotEmpty) ...[
            const SizedBox(height: 4),
            Expanded(
              child: Text(
                memo.content,
                overflow: TextOverflow.fade,
                style: TextStyle(
                    fontSize: 11.5, color: scheme.onSurfaceVariant),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _HeaderIcon extends StatelessWidget {
  const _HeaderIcon({
    required this.tooltip,
    required this.icon,
    required this.onTap,
  });

  final String tooltip;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Tooltip(
      message: tooltip,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(4),
          child: Icon(icon, size: 15, color: scheme.outline),
        ),
      ),
    );
  }
}
