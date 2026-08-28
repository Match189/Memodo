import 'package:desktop_multi_window/desktop_multi_window.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../desktop/widget_settings.dart';
import '../desktop/win32_window_style.dart';
import '../theme/app_theme.dart';
import '../theme/theme_settings.dart';
import '../models/task.dart';
import '../state/memo_list_model.dart';
import '../state/task_list_model.dart';

/// 小组件子窗口的原生窗口标题（win32 按它找 HWND）。
const widgetWindowTitle = '待办小组件';

/// 桌面小组件应用：独立 Flutter 引擎，读同一个 SQLite 库。
/// 主题色跟随主应用的外观设置。
class WidgetWindowApp extends StatelessWidget {
  const WidgetWindowApp({
    super.key,
    required this.windowId,
    required this.taskModel,
    required this.memoModel,
    required this.widgetSettings,
    required this.themeSettings,
  });

  /// 子窗口自己的 id（由 main() 的 multi_window 参数传入）。
  final int windowId;

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
          home: WidgetWindowPage(windowId: windowId),
        );
      }),
    );
  }
}

class WidgetWindowPage extends StatefulWidget {
  const WidgetWindowPage({super.key, required this.windowId});

  final int windowId;

  @override
  State<WidgetWindowPage> createState() => _WidgetWindowPageState();
}

class _WidgetWindowPageState extends State<WidgetWindowPage> {
  final _inputController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _listenToMainWindow();
  }

  void _listenToMainWindow() {
    // 提前取好模型引用，避免异步间隙再用 context。
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

  /// 小组件里的改动要通知主窗口：主窗口负责刷新界面和触发同步。
  Future<void> _notifyMainChanged() async {
    try {
      await DesktopMultiWindow.invokeMethod(0, 'dataChangedFromWidget');
    } catch (_) {
      // 主窗口可能没起来，忽略。
    }
  }

  Future<void> _add() async {
    final text = _inputController.text;
    _inputController.clear();
    await context.read<TaskListModel>().add(text);
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
    final widgetSettings = context.watch<DesktopWidgetSettingsModel>();
    final scheme = Theme.of(context).colorScheme;
    final openTasks = taskModel.tasks.where((t) => !t.done).length;

    // 内容层透明度（窗口层由原生 accent 处理，两层叠加）。
    final bg = scheme.surface.withOpacity(widgetSettings.opacity / 100 * 0.35 + 0.65);

    return Scaffold(
      backgroundColor: bg,
      body: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // 标题栏：按住拖动（锁定位置时禁止）
            GestureDetector(
              onPanStart: (_) {
                if (!widgetSettings.lockPosition) {
                  WidgetWindowNative.beginDrag();
                }
              },
              behavior: HitTestBehavior.opaque,
              child: Row(
                children: [
                  Icon(Icons.check_circle, size: 18, color: scheme.primary),
                  const SizedBox(width: 6),
                  Text('今日待办',
                      style: Theme.of(context).textTheme.titleSmall),
                  const Spacer(),
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
                  _HeaderIcon(
                    tooltip: '关闭',
                    icon: Icons.close,
                    onTap: _close,
                  ),
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
                    decoration: const InputDecoration(
                      hintText: '快速添加…',
                      isDense: true,
                      border: OutlineInputBorder(),
                      contentPadding:
                          EdgeInsets.symmetric(horizontal: 10, vertical: 8),
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
            Expanded(
              child: taskModel.loading
                  ? const Center(
                      child: SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2)))
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
                                padding:
                                    const EdgeInsets.symmetric(vertical: 3),
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
                                              : null,
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
