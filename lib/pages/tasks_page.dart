import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/task.dart';
import '../state/task_list_model.dart';

/// 待办列表页：卡片化列表、勾选完成、点击编辑、删除可撤销，底部输入框新增。
class TasksPage extends StatefulWidget {
  const TasksPage({super.key});

  @override
  State<TasksPage> createState() => _TasksPageState();
}

class _TasksPageState extends State<TasksPage> {
  final _inputController = TextEditingController();

  @override
  void dispose() {
    _inputController.dispose();
    super.dispose();
  }

  Future<void> _add() async {
    final text = _inputController.text;
    _inputController.clear();
    await context.read<TaskListModel>().add(text);
  }

  Future<void> _rename(Task task) async {
    final controller = TextEditingController(text: task.title);
    final newTitle = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('编辑待办'),
        content: TextField(
          controller: controller,
          autofocus: true,
          onSubmitted: (value) => Navigator.pop(context, value),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('取消'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, controller.text),
            child: const Text('保存'),
          ),
        ],
      ),
    );
    controller.dispose();
    if (newTitle != null && mounted) {
      await context.read<TaskListModel>().rename(task, newTitle);
    }
  }

  /// 删除 → Snackbar 撤销（软删除天然支持恢复，SPD §18）。
  Future<void> _remove(Task task) async {
    await context.read<TaskListModel>().remove(task);
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(
        content: Text('已删除「${task.title}」'),
        width: 380,
        behavior: SnackBarBehavior.floating,
        action: SnackBarAction(
          label: '撤销',
          onPressed: () => context.read<TaskListModel>().restore(task),
        ),
      ));
  }

  Future<void> _confirmClearDone() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('清除已完成'),
        content: const Text('确定要删除所有已完成的待办吗？'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('取消'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('删除'),
          ),
        ],
      ),
    );
    if (ok == true && mounted) {
      final model = context.read<TaskListModel>();
      final removed = model.tasks.where((t) => t.done).length;
      await model.clearDone();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(
        content: Text('已清除 $removed 条已完成'),
        width: 380,
        behavior: SnackBarBehavior.floating,
      ));
    }
  }

  @override
  Widget build(BuildContext context) {
    final model = context.watch<TaskListModel>();
    return Scaffold(
      appBar: AppBar(
        title: const Text('待办'),
        actions: [
          if (model.hasDoneTasks)
            IconButton(
              tooltip: '清除已完成',
              icon: const Icon(Icons.cleaning_services_outlined),
              onPressed: _confirmClearDone,
            ),
        ],
      ),
      body: model.loading
          ? const Center(child: CircularProgressIndicator())
          : model.tasks.isEmpty
              ? const _EmptyState()
              : ListView.builder(
                  padding: const EdgeInsets.fromLTRB(12, 4, 12, 96),
                  itemCount: model.tasks.length,
                  itemBuilder: (context, index) {
                    final task = model.tasks[index];
                    return _TaskCard(
                      task: task,
                      onToggle: () =>
                          context.read<TaskListModel>().toggle(task),
                      onTap: () => _rename(task),
                      onRemove: () => _remove(task),
                    );
                  },
                ),
      bottomSheet: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
          child: Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _inputController,
                  decoration: const InputDecoration(
                    hintText: '新增待办，回车确认',
                    isDense: true,
                    contentPadding:
                        EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                  ),
                  onSubmitted: (_) => _add(),
                ),
              ),
              const SizedBox(width: 8),
              IconButton.filled(
                tooltip: '添加',
                icon: const Icon(Icons.add),
                onPressed: _add,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// 单条待办：卡片容器 + 勾选 + 标题（完成划线动画）+ 删除。
class _TaskCard extends StatelessWidget {
  const _TaskCard({
    required this.task,
    required this.onToggle,
    required this.onTap,
    required this.onRemove,
  });

  final Task task;
  final VoidCallback onToggle;
  final VoidCallback onTap;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Card(
        color: task.done ? scheme.surfaceContainerHighest : null,
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(16),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
            child: Row(
              children: [
                Checkbox(value: task.done, onChanged: (_) => onToggle()),
                Expanded(
                  child: AnimatedDefaultTextStyle(
                    duration: const Duration(milliseconds: 200),
                    style: TextStyle(
                      fontSize: 15,
                      color:
                          task.done ? scheme.outline : scheme.onSurface,
                      decoration: task.done
                          ? TextDecoration.lineThrough
                          : TextDecoration.none,
                      decorationColor: scheme.outline,
                    ),
                    child: Text(
                      task.title,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ),
                IconButton(
                  tooltip: '删除',
                  icon: Icon(Icons.close_rounded,
                      size: 18, color: scheme.outline),
                  onPressed: onRemove,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState();

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.task_alt_rounded,
              size: 64, color: scheme.primary.withValues(alpha: 0.5)),
          const SizedBox(height: 12),
          Text('一切尽在掌握',
              style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 4),
          Text('在下方输入框添加第一条待办',
              style: TextStyle(fontSize: 13, color: scheme.outline)),
        ],
      ),
    );
  }
}
