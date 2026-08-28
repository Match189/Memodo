import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/memo.dart';
import '../state/memo_list_model.dart';

/// 新建/编辑备忘页。[existing] 为空时是新建。
class MemoEditPage extends StatefulWidget {
  const MemoEditPage({super.key, this.existing});

  final Memo? existing;

  @override
  State<MemoEditPage> createState() => _MemoEditPageState();
}

class _MemoEditPageState extends State<MemoEditPage> {
  late final _titleController =
      TextEditingController(text: widget.existing?.title ?? '');
  late final _contentController =
      TextEditingController(text: widget.existing?.content ?? '');

  @override
  void dispose() {
    _titleController.dispose();
    _contentController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final model = context.read<MemoListModel>();
    final navigator = Navigator.of(context);
    final title = _titleController.text.trim();
    final content = _contentController.text.trim();
    if (title.isEmpty && content.isEmpty) {
      navigator.pop();
      return;
    }
    if (widget.existing == null) {
      await model.add(title, content);
    } else {
      await model.update(widget.existing!, title: title, content: content);
    }
    navigator.pop();
  }

  Future<void> _delete() async {
    final model = context.read<MemoListModel>();
    final navigator = Navigator.of(context);
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('删除备忘'),
        content: const Text('确定要删除这条备忘吗？'),
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
    if (ok == true) {
      await model.remove(widget.existing!);
      navigator.pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final isNew = widget.existing == null;
    return Scaffold(
      appBar: AppBar(
        title: Text(isNew ? '新建备忘' : '编辑备忘'),
        actions: [
          if (!isNew)
            IconButton(
              tooltip: '删除',
              icon: const Icon(Icons.delete_outline),
              onPressed: _delete,
            ),
          TextButton(onPressed: _save, child: const Text('保存')),
        ],
      ),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            TextField(
              controller: _titleController,
              style: Theme.of(context).textTheme.titleLarge,
              decoration: const InputDecoration(
                hintText: '标题',
                border: InputBorder.none,
              ),
            ),
            const Divider(height: 24),
            Expanded(
              child: TextField(
                controller: _contentController,
                maxLines: null,
                expands: true,
                textAlignVertical: TextAlignVertical.top,
                keyboardType: TextInputType.multiline,
                decoration: const InputDecoration(
                  hintText: '写点什么…',
                  border: InputBorder.none,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
