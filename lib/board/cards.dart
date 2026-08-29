import 'package:flutter/material.dart';

import '../models/memo.dart';
import '../models/task.dart';
import 'board_theme.dart';

/// Todo 卡内容（规格 §13）：勾选 + 标题。业务数据来自 Task，卡面只做展示。
class TodoCardContent extends StatelessWidget {
  const TodoCardContent({
    super.key,
    required this.task,
    required this.theme,
    required this.onToggle,
  });

  final Task task;
  final BoardThemeData theme;
  final VoidCallback onToggle;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 20, 10, 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SizedBox(
                width: 22,
                height: 22,
                child: Checkbox(
                  value: task.done,
                  onChanged: (_) => onToggle(),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  task.title,
                  maxLines: 3,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 14,
                    height: 1.3,
                    color: task.done ? theme.sectionText : null,
                    decoration:
                        task.done ? TextDecoration.lineThrough : null,
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// Memo 卡内容（规格 §14）：标题 + 正文预览。
class MemoCardContent extends StatelessWidget {
  const MemoCardContent({
    super.key,
    required this.memo,
    required this.theme,
  });

  final Memo memo;
  final BoardThemeData theme;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 20, 10, 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            memo.title,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
                fontSize: 14, fontWeight: FontWeight.w600),
          ),
          if (memo.content.isNotEmpty) ...[
            const SizedBox(height: 6),
            Expanded(
              child: Text(
                memo.content,
                overflow: TextOverflow.fade,
                maxLines: 6,
                style: TextStyle(
                  fontSize: 12.5,
                  height: 1.35,
                  color: theme.sectionText,
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
