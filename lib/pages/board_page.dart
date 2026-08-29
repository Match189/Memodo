import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../board/base_card.dart';
import '../board/board_background.dart';
import '../board/board_controller.dart';
import '../board/board_theme.dart';
import '../board/cards.dart';
import '../state/memo_list_model.dart';
import '../state/task_list_model.dart';

/// 图钉板页（SPD Board 扩展 Phase 8a）：
/// 软木板/毛玻璃两套主题；卡片可拖动、缩放、置顶，位置本机记忆。
class BoardPage extends StatefulWidget {
  const BoardPage({super.key});

  @override
  State<BoardPage> createState() => _BoardPageState();
}

class _BoardPageState extends State<BoardPage> {
  String _themeId = BoardThemes.corkId;
  bool _themeLoaded = false;

  @override
  void initState() {
    super.initState();
    // 板卡装载
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<BoardController>().load();
    });
  }

  Future<void> _pickAndPin(String refType) async {
    final controller = context.read<BoardController>();
    final tasks = context.read<TaskListModel>().tasks;
    final memos = context.read<MemoListModel>().memos;

    final pinned = controller.cards.map((c) => c.record.refUuid).toSet();
    final choices = <(String, String, String)>[]; // (refUuid, 标题, 副文本)
    if (refType == 'todo') {
      for (final t in tasks) {
        if (t.uuid != null && !pinned.contains(t.uuid)) {
          choices.add((t.uuid!, t.title, t.done ? '已完成' : '未完成'));
        }
      }
    } else {
      for (final m in memos) {
        if (m.uuid != null && !pinned.contains(m.uuid)) {
          choices.add((m.uuid!, m.title, m.content));
        }
      }
    }
    if (choices.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
        content: Text(refType == 'todo' ? '没有可钉的待办' : '没有可钉的备忘'),
        width: 300,
        behavior: SnackBarBehavior.floating,
      ));
      return;
    }

    final picked = await showDialog<(String, String, String)>(
      context: context,
      builder: (context) => SimpleDialog(
        title: Text(refType == 'todo' ? '选择待办' : '选择备忘'),
        children: [
          for (final c in choices)
            SimpleDialogOption(
              onPressed: () => Navigator.pop(context, c),
              child: ListTile(
                title: Text(c.$2, maxLines: 1, overflow: TextOverflow.ellipsis),
                subtitle: c.$3.isEmpty ? null : Text(c.$3, maxLines: 1),
              ),
            ),
        ],
      ),
    );
    if (picked == null || !mounted) return;
    await controller.pinCard(refType: refType, refUuid: picked.$1);
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<BoardController>();
    final brightness = Theme.of(context).brightness;
    final theme = BoardThemes.resolve(_themeId, brightness);

    return Scaffold(
      appBar: AppBar(
        title: const Text('图钉板'),
        actions: [
          IconButton(
            tooltip: _themeId == BoardThemes.corkId ? '切换毛玻璃' : '切换软木板',
            icon: Icon(_themeId == BoardThemes.corkId
                ?Icons.blur_on_rounded
                : Icons.content_paste_rounded),
            onPressed: () => setState(() =>
                _themeId = _themeId == BoardThemes.corkId
                    ? BoardThemes.glassId
                    : BoardThemes.corkId),
          ),
          IconButton(
            tooltip: '网格吸附',
            icon: const Icon(Icons.grid_4x4_rounded),
            onPressed: () => setState(() => controller.snapToGrid =
                !controller.snapToGrid),
          ),
        ],
      ),
      body: controller.boardUuid == null && !controller.cards.isNotEmpty
          ? const Center(child: CircularProgressIndicator())
          : ClipRect(
              child: Stack(
                fit: StackFit.expand,
                children: [
                  BoardBackground(
                    theme: theme,
                    enableBlur: _themeId == BoardThemes.glassId,
                  ),
                  ..._buildCards(controller, theme),
                ],
              ),
            ),
      bottomNavigationBar: BottomAppBar(
        height: 64,
        child: Row(
          children: [
            const SizedBox(width: 8),
            Expanded(
              child: FilledButton.tonalIcon(
                onPressed: () => _pickAndPin('todo'),
                icon: const Icon(Icons.check_circle_outline),
                label: const Text('钉待办'),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: FilledButton.tonalIcon(
                onPressed: () => _pickAndPin('memo'),
                icon: const Icon(Icons.sticky_note_2_outlined),
                label: const Text('钉备忘'),
              ),
            ),
            const SizedBox(width: 8),
          ],
        ),
      ),
    );
  }

  List<Widget> _buildCards(
      BoardController controller, BoardThemeData theme) {
    final taskModel = context.read<TaskListModel>();
    final memoModel = context.read<MemoListModel>();
    final sorted = [...controller.cards]..sort(
        (a, b) => a.layout.z.compareTo(b.layout.z));

    return [
      for (final view in sorted)
        ValueListenableBuilder<BoardCardLayout>(
          key: ValueKey(view.record.uuid),
          valueListenable: view.layoutNotifier,
          builder: (context, layout, _) {
            Widget content;
            var exists = true;
            if (view.record.refType == 'todo') {
              final task = taskModel.tasks
                  .where((t) => t.uuid == view.record.refUuid)
                  .firstOrNull;
              if (task == null) {
                exists = false;
                content = _MissingContent(onUnpin: () => controller.unpin(view));
              } else {
                content = TodoCardContent(
                  task: task,
                  theme: theme,
                  onToggle: () =>
                      context.read<TaskListModel>().toggle(task),
                );
              }
            } else {
              final memo = memoModel.memos
                  .where((m) => m.uuid == view.record.refUuid)
                  .firstOrNull;
              if (memo == null) {
                exists = false;
                content = _MissingContent(onUnpin: () => controller.unpin(view));
              } else {
                content = MemoCardContent(memo: memo, theme: theme);
              }
            }
            if (!exists) {
              // 实体已被删除：卡片仍显示占位并可取下
            }
            return Positioned(
              left: layout.x,
              top: layout.y,
              child: BaseCard(
                theme: theme,
                layout: layout,
                dragging: false,
                onDrag: (dx, dy) => controller.dragBy(view, dx, dy),
                onDragEnd: () => controller.endGesture(view),
                onResize: (dw, dh) => controller.resizeBy(view, dw, dh),
                onResizeEnd: () => controller.endGesture(view),
                onTap: () => controller.bringToFront(view),
                child: content,
              ),
            );
          },
        ),
    ];
  }
}

class _MissingContent extends StatelessWidget {
  const _MissingContent({required this.onUnpin});

  final VoidCallback onUnpin;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.all(12),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text('源内容已删除',
              style: TextStyle(fontSize: 13, color: scheme.outline)),
          const SizedBox(height: 8),
          TextButton.icon(
            onPressed: onUnpin,
            icon: const Icon(Icons.push_pin_outlined, size: 16),
            label: const Text('从板上取下'),
          ),
        ],
      ),
    );
  }
}
