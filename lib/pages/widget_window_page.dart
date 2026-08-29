import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../desktop/widget_settings.dart';
import '../desktop/win32_window_style.dart';
import '../models/memo.dart';
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
/// 主题色跟随主应用的外观设置。
class WidgetWindowApp extends StatelessWidget {
  const WidgetWindowApp({
    super.key,
    required this.windowId,
    required this.kind,
    required this.taskModel,
    required this.memoModel,
    required this.widgetSettings,
    required this.themeSettings,
  });

  /// 子窗口自己的 id（由 main() 的 multi_window 参数传入）。
  final int windowId;
  final WidgetKind kind;

  final TaskListModel taskModel;
  final MemoListModel memoModel;
  final DesktopWidgetSettingsModel widgetSettings;
  final ThemeSettingsModel themeSettings;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: taskModel),
        ChangeNotifierProvider.value(value: memoModel),
        ChangeNotifierProvider.value(value: widgetSettings),
        ChangeNotifierProvider.value(value: themeSettings),
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
  final _inputController = TextEditingController();

  String get _windowTitle =>
      widget.kind == 'memo' ? memoWidgetWindowTitle : widgetWindowTitle;

  @override
  void initState() {
    super.initState();
    final taskModel = context.read<TaskListModel>();
    final memoModel = context.read<MemoListModel>();
    // 主窗口广播的数据变化 → 重载列表。
    DesktopMultiWindow.setMethodHandler((call, fromWindowId) async {
      if (call.method == 'dataChangedFromMain') {
        await taskModel.load();
        await memoModel.load();
      }
      return null;
    });
  }

  @override
  void dispose() {
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
      // 快捷备忘：标题即内容
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
    final scheme = Theme.of(context).colorScheme;
    final showTasks = widget.kind != 'memo';
    final showMemos = widget.kind != 'todo';

    // 材质与不透明度 → 内容层底色（窗口层由原生 accent 处理）。
    final base = widgetSettings.opacity / 100;
    final bgAlpha = widgetSettings.material == WidgetMaterial.solid
        ? 1.0
        : (base * 0.7).clamp(0.3, 1.0);
    final bg = scheme.surface.withOpacity(bgAlpha);

    final openTasks = taskModel.tasks.where((t) => !t.done).length;

    return Scaffold(
      backgroundColor: bg,
      body: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            GestureDetector(
              onPanStart: (_) {
                if (!widgetSettings.lockPosition) {
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
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _inputController,
                    style: const TextStyle(fontSize: 13),
                    decoration: InputDecoration(
                      hintText: widget.kind == 'memo' ? '快速记一条备忘…' : '快速添加…',
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
              Expanded(
                flex: showMemos ? 3 : 1,
                child: taskModel.loading
                    ? const Center(
                        child: SizedBox(
                            width: 18,
                            height: 18,
                            child:
                                CircularProgressIndicator(strokeWidth: 2)))
                    : taskModel.tasks.isEmpty
                        ? Center(
                            child: Text('还没有待办',
                                style: TextStyle(
                                    fontSize: 12, color: scheme.outline)))
                        : ListView.builder(
                            itemCount: taskModel.tasks.length,
                            padding: EdgeInsets.zero,
                            itemBuilder: (context, index) {
                              final task = taskModel.tasks[index];
                              return InkWell(
                                onTap: () => _toggle(task),
                                child: Padding(
                                  padding: const EdgeInsets.symmetric(
                                      vertical: 3),
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
              ),
            if (showMemos && widget.kind == 'both')
              Padding(
                padding: const EdgeInsets.only(top: 8, bottom: 2),
                child: Row(
                  children: [
                    Icon(Icons.sticky_note_2_outlined,
                        size: 14, color: scheme.outline),
                    const SizedBox(width: 4),
                    Text('备忘',
                        style: Theme.of(context)
                            .textTheme
                            .labelSmall
                            ?.copyWith(color: scheme.outline)),
                  ],
                ),
              ),
            if (showMemos)
              Expanded(
                flex: widget.kind == 'both' ? 2 : 1,
                child: memoModel.memos.isEmpty
                    ? Center(
                        child: Text('还没有备忘',
                            style: TextStyle(
                                fontSize: 12, color: scheme.outline)))
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
              ),
          ],
        ),
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
