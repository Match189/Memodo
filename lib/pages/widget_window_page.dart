import 'dart:async';

import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../board/base_card.dart';
import '../board/board_controller.dart';
import '../board/board_theme.dart';
import '../board/pin_widget.dart';
import '../desktop/widget_settings.dart';
import '../desktop/win32_window_style.dart';
import '../models/memo.dart';
import '../models/task.dart';
import '../state/memo_list_model.dart';
import '../state/task_list_model.dart';
import '../theme/app_theme.dart';
import '../theme/theme_settings.dart';

/// 单一桌面小组件窗口的原生窗口标题（win32 按它找 HWND）。
const widgetWindowTitle = '念念小组件';

/// 启动时传入的 kind 决定子窗口内部布局：
/// - 'todo'：单卡片显示待办
/// - 'memo'：单卡片显示备忘
/// - 'dual'：单窗口分两栏（待办左/备忘右）
typedef WidgetKind = String; // 'todo' | 'memo' | 'dual'

/// 桌面小组件应用：独立 Flutter 引擎，读同一个 SQLite 库。
/// 预创建模式：主进程启动时即唤起本进程，藏在任务栏后待命。
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
  const WidgetWindowPage({super.key, required this.windowId, required this.kind});

  final int windowId;
  final WidgetKind kind;

  @override
  State<WidgetWindowPage> createState() => _WidgetWindowPageState();
}

class _WidgetWindowPageState extends State<WidgetWindowPage> {
  final _todoInput = TextEditingController();
  final _memoInput = TextEditingController();
  Timer? _rectReportTimer;
  bool _startedHidden = false;

  String get _windowTitle => widgetWindowTitle;

  @override
  void initState() {
    super.initState();
    final taskModel = context.read<TaskListModel>();
    final memoModel = context.read<MemoListModel>();
    final boardController = context.read<BoardController>();

    DesktopMultiWindow.setMethodHandler((call, fromWindowId) async {
      final widgetSettings = context.read<DesktopWidgetSettingsModel>();
      final themeSettings = context.read<ThemeSettingsModel>();
      switch (call.method) {
        case 'dataChangedFromMain':
          await taskModel.load();
          await memoModel.load();
          await boardController.load();
        case 'layoutChanged':
        case 'themeChanged':
        case 'settingsChanged':
          await widgetSettings.reload();
          await themeSettings.load();
          break;
        case 'initHidden':
          // 预创建模式：本进程自套样式后立即隐藏，不绘制交互
          _startedHidden = true;
          break;
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
          await WidgetWindowNative.attachToDesktop(
              windowTitle: _windowTitle);
        case 'detach':
          await WidgetWindowNative.detachFromDesktop(
              windowTitle: _windowTitle);
      }
      return null;
    });

    WidgetsBinding.instance.addPostFrameCallback((_) {
      _applyStyle();
      _rectReportTimer = Timer.periodic(const Duration(seconds: 10), (_) {
        _reportRect();
      });
      debugPrint('[widget] first frame rendered ✓');
    });
    // 立即装载：data 一旦可用即刷新 UI
    unawaited(taskModel.load());
    unawaited(memoModel.load());
    unawaited(boardController.load());
  }

  void _applyStyle() {
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
      WidgetWindowNative.attachToDesktop(windowTitle: _windowTitle);
    }
  }

  void _reportRect() {
    if (_startedHidden) return; // 预创建模式不暴露
    final r = WidgetWindowNative.getRect(windowTitle: _windowTitle);
    if (r == null) return;
    unawaited(DesktopMultiWindow.invokeMethod(0, 'widgetRect', {
      'x': r.x,
      'y': r.y,
      'w': r.w,
      'h': r.h,
    }));
  }

  @override
  void dispose() {
    _rectReportTimer?.cancel();
    _todoInput.dispose();
    _memoInput.dispose();
    super.dispose();
  }

  Future<void> _notifyMainChanged() async {
    try {
      await DesktopMultiWindow.invokeMethod(0, 'dataChangedFromWidget');
    } catch (_) {}
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
    if (_startedHidden) {
      // 预创建模式：藏在后台，无可见 UI
      return const SizedBox.shrink();
    }
    final widgetSettings = context.watch<DesktopWidgetSettingsModel>();
    final scheme = Theme.of(context).colorScheme;
    final base = widgetSettings.opacity / 100;
    final bgAlpha = widgetSettings.material == WidgetMaterial.solid
        ? 1.0
        : (base * 0.7).clamp(0.3, 1.0);
    final bg = scheme.surface.withOpacity(bgAlpha);

    return Scaffold(
      backgroundColor: bg,
      body: widget.kind == 'dual'
          ? _DualPaneView(
              bg: bg,
              scheme: scheme,
              todoInput: _todoInput,
              memoInput: _memoInput,
              onToggle: _toggle,
              onClose: _close,
              widgetSettings: widgetSettings,
            )
          : _SinglePaneView(
              bg: bg,
              scheme: scheme,
              kind: widget.kind,
              todoInput: _todoInput,
              memoInput: _memoInput,
              onToggle: _toggle,
              onClose: _close,
              widgetSettings: widgetSettings,
            ),
    );
  }
}

/// 单卡片：根据 kind 显示待办或备忘。
class _SinglePaneView extends StatelessWidget {
  const _SinglePaneView({
    required this.bg,
    required this.scheme,
    required this.kind,
    required this.todoInput,
    required this.memoInput,
    required this.onToggle,
    required this.onClose,
    required this.widgetSettings,
  });

  final Color bg;
  final ColorScheme scheme;
  final String kind;
  final TextEditingController todoInput;
  final TextEditingController memoInput;
  final void Function(Task) onToggle;
  final VoidCallback onClose;
  final DesktopWidgetSettingsModel widgetSettings;

  @override
  Widget build(BuildContext context) {
    final taskModel = context.watch<TaskListModel>();
    final memoModel = context.watch<MemoListModel>();
    final isTodo = kind != 'memo';
    final openTasks = taskModel.tasks.where((t) => !t.done).length;

    return Padding(
      padding: const EdgeInsets.all(10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _Header(
            title: isTodo ? '今日待办' : '备忘',
            sub: isTodo ? '$openTasks 项未完成' : null,
            scheme: scheme,
            onClose: onClose,
            draggable: !widgetSettings.lockPosition,
            windowTitle: widgetWindowTitle,
          ),
          const SizedBox(height: 6),
          _QuickAdd(
            hint: isTodo ? '快速添加…' : '快速记一条备忘…',
            controller: isTodo ? todoInput : memoInput,
            onSubmit: () async {
              final text = (isTodo ? todoInput : memoInput).text;
              (isTodo ? todoInput : memoInput).clear();
              if (isTodo) {
                await context.read<TaskListModel>().add(text);
              } else {
                await context.read<MemoListModel>().add(text, '');
              }
              await DesktopMultiWindow.invokeMethod(0, 'dataChangedFromWidget');
            },
          ),
          const SizedBox(height: 6),
          Expanded(
            child: isTodo
                ? _TaskList(tasks: taskModel.tasks, scheme: scheme, onToggle: onToggle)
                : _MemoList(memos: memoModel.memos, scheme: scheme),
          ),
        ],
      ),
    );
  }
}

/// 单窗口分两栏：待办左、备忘右。
class _DualPaneView extends StatelessWidget {
  const _DualPaneView({
    required this.bg,
    required this.scheme,
    required this.todoInput,
    required this.memoInput,
    required this.onToggle,
    required this.onClose,
    required this.widgetSettings,
  });

  final Color bg;
  final ColorScheme scheme;
  final TextEditingController todoInput;
  final TextEditingController memoInput;
  final void Function(Task) onToggle;
  final VoidCallback onClose;
  final DesktopWidgetSettingsModel widgetSettings;

  @override
  Widget build(BuildContext context) {
    final taskModel = context.watch<TaskListModel>();
    final memoModel = context.watch<MemoListModel>();
    final openTasks = taskModel.tasks.where((t) => !t.done).length;

    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 8, 8, 8),
      child: Column(
        children: [
          _Header(
            title: '念念',
            sub: '今日 $openTasks 项 · ${memoModel.memos.length} 备忘',
            scheme: scheme,
            onClose: onClose,
            draggable: !widgetSettings.lockPosition,
            windowTitle: widgetWindowTitle,
          ),
          const SizedBox(height: 6),
          Expanded(
            child: Row(
              children: [
                Expanded(
                  child: _Pane(
                    title: '待办',
                    scheme: scheme,
                    child: Column(
                      children: [
                        _QuickAdd(
                          hint: '快速添加…',
                          controller: todoInput,
                          compact: true,
                          onSubmit: () async {
                            final text = todoInput.text;
                            todoInput.clear();
                            await context.read<TaskListModel>().add(text);
                            await DesktopMultiWindow.invokeMethod(
                                0, 'dataChangedFromWidget');
                          },
                        ),
                        const SizedBox(height: 4),
                        Expanded(
                          child: _TaskList(
                              tasks: taskModel.tasks,
                              scheme: scheme,
                              onToggle: onToggle,
                              dense: true),
                        ),
                      ],
                    ),
                  ),
                ),
                Container(width: 1, color: scheme.outlineVariant.withOpacity(0.5)),
                Expanded(
                  child: _Pane(
                    title: '备忘',
                    scheme: scheme,
                    child: Column(
                      children: [
                        _QuickAdd(
                          hint: '快速记一条…',
                          controller: memoInput,
                          compact: true,
                          onSubmit: () async {
                            final text = memoInput.text;
                            memoInput.clear();
                            await context.read<MemoListModel>().add(text, '');
                            await DesktopMultiWindow.invokeMethod(
                                0, 'dataChangedFromWidget');
                          },
                        ),
                        const SizedBox(height: 4),
                        Expanded(
                          child: _MemoList(
                              memos: memoModel.memos,
                              scheme: scheme,
                              dense: true),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({
    required this.title,
    required this.sub,
    required this.scheme,
    required this.onClose,
    required this.draggable,
    required this.windowTitle,
  });

  final String title;
  final String? sub;
  final ColorScheme scheme;
  final VoidCallback onClose;
  final bool draggable;
  final String windowTitle;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onPanStart: draggable ? (_) => WidgetWindowNative.beginDrag(windowTitle: windowTitle) : null,
      child: Row(
        children: [
          Icon(Icons.push_pin_rounded, size: 16, color: scheme.primary),
          const SizedBox(width: 6),
          Text(title,
              style: Theme.of(context)
                  .textTheme
                  .titleSmall
                  ?.copyWith(fontWeight: FontWeight.w600)),
          if (sub != null) ...[
            const SizedBox(width: 8),
            Expanded(
                child: Text(sub!,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context)
                        .textTheme
                        .labelSmall
                        ?.copyWith(color: scheme.outline))),
          ] else
            const Spacer(),
          _IconBtn(
              icon: Icons.open_in_new, onTap: () => WidgetWindowNative.openMainWindow()),
          _IconBtn(icon: Icons.close_rounded, onTap: onClose),
        ],
      ),
    );
  }
}

class _IconBtn extends StatelessWidget {
  const _IconBtn({required this.icon, required this.onTap});

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkResponse(
      onTap: onTap,
      radius: 14,
      child: Padding(
        padding: const EdgeInsets.all(4),
        child: Icon(icon, size: 15, color: Theme.of(context).colorScheme.outline),
      ),
    );
  }
}

class _Pane extends StatelessWidget {
  const _Pane({required this.title, required this.scheme, required this.child});

  final String title;
  final ColorScheme scheme;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
            child: Text(
              title,
              style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: scheme.outline,
                    fontWeight: FontWeight.w600,
                  ),
            ),
          ),
          Expanded(child: child),
        ],
      ),
    );
  }
}

class _QuickAdd extends StatelessWidget {
  const _QuickAdd({
    required this.hint,
    required this.controller,
    required this.onSubmit,
    this.compact = false,
  });

  final String hint;
  final TextEditingController controller;
  final VoidCallback onSubmit;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return TextField(
      controller: controller,
      style: TextStyle(fontSize: compact ? 12 : 13),
      decoration: InputDecoration(
        hintText: hint,
        isDense: true,
        contentPadding: EdgeInsets.symmetric(
          horizontal: 10,
          vertical: compact ? 6 : 8,
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(8),
          borderSide: BorderSide(color: scheme.outlineVariant),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(8),
          borderSide: BorderSide(color: scheme.outlineVariant),
        ),
      ),
      onSubmitted: (_) => onSubmit(),
      textInputAction: TextInputAction.done,
    );
  }
}

class _TaskList extends StatelessWidget {
  const _TaskList({
    required this.tasks,
    required this.scheme,
    required this.onToggle,
    this.dense = false,
  });

  final List tasks;
  final ColorScheme scheme;
  final void Function(Task) onToggle;
  final bool dense;

  @override
  Widget build(BuildContext context) {
    if (tasks.isEmpty) {
      return Center(
        child: Text('还没有待办',
            style: TextStyle(fontSize: 12, color: scheme.outline)),
      );
    }
    return ListView.builder(
      padding: EdgeInsets.zero,
      itemCount: tasks.length,
      itemBuilder: (context, i) {
        final task = tasks[i] as Task;
        return InkWell(
          onTap: () => onToggle(task),
          child: Padding(
            padding: EdgeInsets.symmetric(vertical: dense ? 1 : 2),
            child: Row(
              children: [
                SizedBox(
                  width: dense ? 18 : 20,
                  height: dense ? 18 : 20,
                  child: Checkbox(
                    value: task.done,
                    onChanged: (_) => onToggle(task),
                    materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  ),
                ),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(
                    task.title,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: dense ? 12 : 13,
                      decoration: task.done
                          ? TextDecoration.lineThrough
                          : TextDecoration.none,
                      color: task.done ? scheme.outline : scheme.onSurface,
                    ),
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _MemoList extends StatelessWidget {
  const _MemoList({
    required this.memos,
    required this.scheme,
    this.dense = false,
  });

  final List memos;
  final ColorScheme scheme;
  final bool dense;

  @override
  Widget build(BuildContext context) {
    if (memos.isEmpty) {
      return Center(
        child: Text('还没有备忘',
            style: TextStyle(fontSize: 12, color: scheme.outline)),
      );
    }
    return ListView.builder(
      padding: EdgeInsets.zero,
      itemCount: memos.length,
      itemBuilder: (context, i) {
        final memo = memos[i] as Memo;
        return Padding(
          padding: EdgeInsets.symmetric(vertical: dense ? 1 : 2),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                memo.title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: dense ? 12 : 12.5,
                  fontWeight: FontWeight.w600,
                ),
              ),
              if (memo.content.isNotEmpty)
                Text(
                  memo.content,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                      fontSize: dense ? 11 : 11.5, color: scheme.outline),
                ),
            ],
          ),
        );
      },
    );
  }
}
