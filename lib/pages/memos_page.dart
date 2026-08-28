import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/memo.dart';
import '../state/memo_list_model.dart';
import '../utils/dates.dart';
import 'memo_edit_page.dart';

/// 备忘列表页：卡片网格布局，宽度自适应，点卡片进入编辑。
class MemosPage extends StatelessWidget {
  const MemosPage({super.key});

  @override
  Widget build(BuildContext context) {
    final model = context.watch<MemoListModel>();
    return Scaffold(
      appBar: AppBar(title: const Text('备忘')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => Navigator.of(context).push(
          MaterialPageRoute(builder: (_) => const MemoEditPage()),
        ),
        icon: const Icon(Icons.add),
        label: const Text('新建'),
      ),
      body: model.loading
          ? const Center(child: CircularProgressIndicator())
          : model.memos.isEmpty
              ? const Center(child: Text('还没有备忘，点右下角新建一个吧'))
              : GridView.builder(
                  padding: const EdgeInsets.fromLTRB(12, 4, 12, 88),
                  gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
                    maxCrossAxisExtent: 260,
                    mainAxisSpacing: 10,
                    crossAxisSpacing: 10,
                    childAspectRatio: 0.95,
                  ),
                  itemCount: model.memos.length,
                  itemBuilder: (context, index) =>
                      _MemoCard(memo: model.memos[index]),
                ),
    );
  }
}

class _MemoCard extends StatelessWidget {
  const _MemoCard({required this.memo});

  final Memo memo;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final updated = memo.updatedAt;
    return Card(
      margin: EdgeInsets.zero,
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute(builder: (_) => MemoEditPage(existing: memo)),
        ),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                memo.title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 6),
              Expanded(
                child: Text(
                  memo.content,
                  maxLines: 6,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context)
                      .textTheme
                      .bodySmall
                      ?.copyWith(color: scheme.onSurfaceVariant),
                ),
              ),
              Align(
                alignment: Alignment.bottomRight,
                child: Text(
                  relativeDate(updated),
                  style: Theme.of(context)
                      .textTheme
                      .labelSmall
                      ?.copyWith(color: scheme.outline),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
