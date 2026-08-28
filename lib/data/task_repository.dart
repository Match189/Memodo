import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:uuid/uuid.dart';

import '../models/task.dart';

/// tasks 表的增删改查。
///
/// 删除一律走软删除（写 deleted_at 墓碑），物理行保留以参与同步。
/// [deviceId] 标记本机写入来源（SPD §9/§19）。
class TaskRepository {
  TaskRepository(this._db, {String deviceId = ''}) : _deviceId = deviceId;

  final Database _db;
  final String _deviceId;

  /// 未完成的在前，其余按最近更新时间倒序；不含已删除。
  Future<List<Task>> listAll() async {
    final rows = await _db.query('tasks',
        where: 'deleted = 0', orderBy: 'done ASC, updated_at DESC');
    return [for (final row in rows) Task.fromMap(row)];
  }

  /// 同步用全量：包含墓碑行。
  Future<List<Task>> listForSync() async {
    final rows = await _db.query('tasks');
    return [for (final row in rows) Task.fromMap(row)];
  }

  /// 新增。uuid / deviceId 为空时自动补齐；返回带自增 id 的完整对象。
  Future<Task> insert(Task task) async {
    var withMeta = task.uuid == null
        ? task.copyWith(uuid: const Uuid().v4())
        : task;
    if (_deviceId.isNotEmpty && (withMeta.deviceId == null || withMeta.deviceId!.isEmpty)) {
      withMeta = withMeta.copyWith(deviceId: _deviceId);
    }
    final id = await _db.insert('tasks', withMeta.toMap());
    return withMeta.withId(id);
  }

  /// 本机修改：覆盖业务字段并标记来源设备。
  Future<void> update(Task task) async {
    final map = task.toMap();
    if (_deviceId.isNotEmpty) map['device_id'] = _deviceId;
    await _db.update('tasks', map, where: 'id = ?', whereArgs: [task.id]);
  }

  /// 软删除：写时间戳墓碑。
  Future<void> delete(Task task) async {
    final id = task.id;
    if (id == null) return;
    final now = DateTime.now().millisecondsSinceEpoch;
    await _db.update(
        'tasks',
        {
          'deleted': 1,
          'deleted_at': now,
          'updated_at': now,
          if (_deviceId.isNotEmpty) 'device_id': _deviceId,
        },
        where: 'id = ?',
        whereArgs: [id]);
  }

  /// 软删除所有已完成（未删除）的任务。
  Future<void> deleteDone() async {
    final now = DateTime.now().millisecondsSinceEpoch;
    await _db.update(
        'tasks',
        {
          'deleted': 1,
          'deleted_at': now,
          'updated_at': now,
          if (_deviceId.isNotEmpty) 'device_id': _deviceId,
        },
        where: 'done = 1 AND deleted = 0');
  }

  /// 同步合并写入：按 uuid upsert，存在则覆盖业务字段，不存在则插入；
  /// created_at 首次写入后不再变化。同步数据不含本机自增 id。
  Future<void> upsertByUuid(Task task) async {
    final uuid = task.uuid;
    assert(uuid != null, '同步行必须有 uuid');
    await _db.rawInsert('''
      INSERT INTO tasks (uuid, title, description, done, priority, due_at,
                         created_at, updated_at, deleted, deleted_at, device_id)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
      ON CONFLICT(uuid) DO UPDATE SET
        title = excluded.title,
        description = excluded.description,
        done = excluded.done,
        priority = excluded.priority,
        due_at = excluded.due_at,
        updated_at = excluded.updated_at,
        deleted = excluded.deleted,
        deleted_at = excluded.deleted_at,
        device_id = excluded.device_id
    ''', [
      uuid,
      task.title,
      task.description,
      task.done ? 1 : 0,
      task.priority,
      task.dueAt?.millisecondsSinceEpoch,
      task.createdAt.millisecondsSinceEpoch,
      task.updatedAt.millisecondsSinceEpoch,
      task.deleted ? 1 : 0,
      task.deletedAt?.millisecondsSinceEpoch,
      task.deviceId,
    ]);
  }
}
