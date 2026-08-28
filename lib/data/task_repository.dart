import 'package:sqflite/sqflite.dart';
import 'package:uuid/uuid.dart';

import '../models/task.dart';

/// tasks 表的增删改查。
///
/// 删除一律走软删除（置 deleted 墓碑），物理行保留以参与同步。
class TaskRepository {
  TaskRepository(this._db);

  final Database _db;

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

  /// 新增。uuid 为空时自动生成；返回带自增 id 的完整对象。
  Future<Task> insert(Task task) async {
    final withUuid =
        task.uuid == null ? task.copyWith(uuid: const Uuid().v4()) : task;
    final id = await _db.insert('tasks', withUuid.toMap());
    return withUuid.withId(id);
  }

  Future<void> update(Task task) =>
      _db.update('tasks', task.toMap(), where: 'id = ?', whereArgs: [task.id]);

  /// 软删除。
  Future<void> delete(Task task) async {
    final id = task.id;
    if (id == null) return;
    await _db.update('tasks',
        {'deleted': 1, 'updated_at': DateTime.now().millisecondsSinceEpoch},
        where: 'id = ?',
        whereArgs: [id]);
  }

  /// 软删除所有已完成（未删除）的任务。
  Future<void> deleteDone() async {
    await _db.update(
        'tasks',
        {
          'deleted': 1,
          'updated_at': DateTime.now().millisecondsSinceEpoch,
        },
        where: 'done = 1 AND deleted = 0');
  }

  /// 同步合并写入：按 uuid upsert，存在则覆盖业务字段，不存在则插入；
  /// created_at 首次写入后不再变化。同步数据不含本机自增 id。
  Future<void> upsertByUuid(Task task) async {
    final uuid = task.uuid;
    assert(uuid != null, '同步行必须有 uuid');
    await _db.rawInsert('''
      INSERT INTO tasks (uuid, title, done, created_at, updated_at, deleted)
      VALUES (?, ?, ?, ?, ?, ?)
      ON CONFLICT(uuid) DO UPDATE SET
        title = excluded.title,
        done = excluded.done,
        updated_at = excluded.updated_at,
        deleted = excluded.deleted
    ''', [
      uuid,
      task.title,
      task.done ? 1 : 0,
      task.createdAt.millisecondsSinceEpoch,
      task.updatedAt.millisecondsSinceEpoch,
      task.deleted ? 1 : 0,
    ]);
  }
}
