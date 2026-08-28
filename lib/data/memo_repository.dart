import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:uuid/uuid.dart';

import '../models/memo.dart';

/// memos 表的增删改查。删除一律软删除，语义同任务表。
class MemoRepository {
  MemoRepository(this._db, {String deviceId = ''}) : _deviceId = deviceId;

  final Database _db;
  final String _deviceId;

  Future<List<Memo>> listAll() async {
    final rows = await _db
        .query('memos', where: 'deleted = 0', orderBy: 'updated_at DESC');
    return [for (final row in rows) Memo.fromMap(row)];
  }

  /// 同步用全量：包含墓碑行。
  Future<List<Memo>> listForSync() async {
    final rows = await _db.query('memos');
    return [for (final row in rows) Memo.fromMap(row)];
  }

  /// 新增。uuid / deviceId 为空时自动补齐；返回带自增 id 的完整对象。
  Future<Memo> insert(Memo memo) async {
    var withMeta = memo.uuid == null
        ? memo.copyWith(uuid: const Uuid().v4())
        : memo;
    if (_deviceId.isNotEmpty &&
        (withMeta.deviceId == null || withMeta.deviceId!.isEmpty)) {
      withMeta = withMeta.copyWith(deviceId: _deviceId);
    }
    final id = await _db.insert('memos', withMeta.toMap());
    return withMeta.withId(id);
  }

  Future<void> update(Memo memo) async {
    final map = memo.toMap();
    if (_deviceId.isNotEmpty) map['device_id'] = _deviceId;
    await _db.update('memos', map, where: 'id = ?', whereArgs: [memo.id]);
  }

  /// 软删除：写时间戳墓碑。
  Future<void> delete(Memo memo) async {
    final id = memo.id;
    if (id == null) return;
    final now = DateTime.now().millisecondsSinceEpoch;
    await _db.update(
        'memos',
        {
          'deleted': 1,
          'deleted_at': now,
          'updated_at': now,
          if (_deviceId.isNotEmpty) 'device_id': _deviceId,
        },
        where: 'id = ?',
        whereArgs: [id]);
  }

  /// 同步合并写入：按 uuid upsert（语义同 TaskRepository）。
  Future<void> upsertByUuid(Memo memo) async {
    final uuid = memo.uuid;
    assert(uuid != null, '同步行必须有 uuid');
    await _db.rawInsert('''
      INSERT INTO memos (uuid, title, content, created_at, updated_at,
                         deleted, deleted_at, device_id)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?)
      ON CONFLICT(uuid) DO UPDATE SET
        title = excluded.title,
        content = excluded.content,
        updated_at = excluded.updated_at,
        deleted = excluded.deleted,
        deleted_at = excluded.deleted_at,
        device_id = excluded.device_id
    ''', [
      uuid,
      memo.title,
      memo.content,
      memo.createdAt.millisecondsSinceEpoch,
      memo.updatedAt.millisecondsSinceEpoch,
      memo.deleted ? 1 : 0,
      memo.deletedAt?.millisecondsSinceEpoch,
      memo.deviceId,
    ]);
  }
}
