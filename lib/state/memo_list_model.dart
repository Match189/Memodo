import 'package:flutter/foundation.dart';

import '../data/memo_repository.dart';
import '../models/memo.dart';

/// 备忘列表的状态：持有数据并在变更后通知界面刷新。
class MemoListModel extends ChangeNotifier {
  MemoListModel(this._repo);

  final MemoRepository _repo;

  List<Memo> _memos = const [];
  bool _loading = true;

  List<Memo> get memos => _memos;
  bool get loading => _loading;

  Future<void> load() async {
    _memos = await _repo.listAll();
    _loading = false;
    notifyListeners();
  }

  Future<void> add(String title, String content) async {
    final now = DateTime.now();
    await _repo.insert(Memo(
      title: title.trim().isEmpty ? '无标题' : title.trim(),
      content: content.trim(),
      createdAt: now,
      updatedAt: now,
    ));
    await load();
  }

  Future<void> update(Memo memo, {required String title, required String content}) async {
    final id = memo.id;
    if (id == null) return;
    await _repo.update(memo.copyWith(
      title: title.trim().isEmpty ? '无标题' : title.trim(),
      content: content.trim(),
      updatedAt: DateTime.now(),
    ));
    await load();
  }

  Future<void> remove(Memo memo) async {
    await _repo.delete(memo);
    await load();
  }
}
