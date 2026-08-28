import 'package:sqflite/sqflite.dart';
import 'package:uuid/uuid.dart';

import '../models/memo.dart';

/// memos 表的增删改查。删除一律软删除，语义同任务表。
class MemoRepository {
  MemoRepository(this._db);

  final Database _db;

  Future<List<Memo>> listAll() async {
    final rows =
        await _db.query('memos', where: 'deleted = 0', orderBy: 'updated_at DESC');
    return [for (final row in rows) Memo.fromMap(row)];
  }

  /// 同步用全量：包含墓碑行。
  Future<List<Memo>> listForSync() async {
    final rows = await _db.query('memos');
    return [for (final row in rows) Memo.fromMap(row)];
  }

  /// 新增。uuid 为空时自动生成；返回带自增 id 的完整对象。
  Future<Memo> insert(Memo memo) async {
    final withUuid =
        memo.uuid == null ? memo.copyWith(uuid: const Uuid().v4()) : memo;
    final id = await _db.insert('memos', withUuid.toMap());
    return withUuid.withId(id);
  }

  Future<void> update(Memo memo) =>
      _db.update('memos', memo.toMap(), where: 'id = ?', whereArgs: [memo.id]);

  /// 软删除。
  Future<void> delete(Memo memo) async {
    final id = memo.id;
    if (id == null) return;
    await _db.update('memos',
        {'deleted': 1, 'updated_at': DateTime.now().millisecondsSinceEpoch},
        where: 'id = ?',
        whereArgs: [id]);
  }

  /// 同步合并写入：按 uuid upsert（语义同 TaskRepository）。
  Future<void> upsertByUuid(Memo memo) async {
    final uuid = memo.uuid;
    assert(uuid != null, '同步行必须有 uuid');
    await _db.rawInsert('''
      INSERT INTO memos (uuid, title, content, created_at, updated_at, deleted)
      VALUES (?, ?, ?, ?, ?, ?)
      ON CONFLICT(uuid) DO UPDATE SET
        title = excluded.title,
        content = excluded.content,
        updated_at = excluded.updated_at,
        deleted = excluded.deleted
    ''', [
      uuid,
      memo.title,
      memo.content,
      memo.createdAt.millisecondsSinceEpoch,
      memo.updatedAt.millisecondsSinceEpoch,
      memo.deleted ? 1 : 0,
    ]);
  }
}
