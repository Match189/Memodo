import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/task.dart';
import '../state/task_list_model.dart';

/// 待办列表页：勾选完成、点击编辑、删除、清除已完成，底部输入框新增。
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
      await context.read<TaskListModel>().clearDone();
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
              ? const Center(
                  child: Text('还没有待办，在下方输入框添加一条吧'),
                )
              : ListView.builder(
                  padding: const EdgeInsets.only(bottom: 96),
                  itemCount: model.tasks.length,
                  itemBuilder: (context, index) {
                    final task = model.tasks[index];
                    return ListTile(
                      leading: Checkbox(
                        value: task.done,
                        onChanged: (_) =>
                            context.read<TaskListModel>().toggle(task),
                      ),
                      onTap: () => _rename(task),
                      title: Text(
                        task.title,
                        style: TextStyle(
                          decoration: task.done
                              ? TextDecoration.lineThrough
                              : null,
                          color: task.done
                              ? Theme.of(context).colorScheme.outline
                              : null,
                        ),
                      ),
                      trailing: IconButton(
                        icon: const Icon(Icons.delete_outline),
                        onPressed: () =>
                            context.read<TaskListModel>().remove(task),
                      ),
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
                    border: OutlineInputBorder(),
                    isDense: true,
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
