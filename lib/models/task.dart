/// 一条待办任务。
///
/// [uuid] 是跨设备的全局标识（同步按它合并，本地自增 id 只在本机有意义）；
/// [deleted] 是软删除墓碑：删除不物理清行，同步到其他端后再保持一致。
class Task {
  const Task({
    this.id,
    this.uuid,
    required this.title,
    required this.done,
    required this.createdAt,
    required this.updatedAt,
    this.deleted = false,
  });

  factory Task.fromMap(Map<String, Object?> map) => Task(
        id: map['id'] as int?,
        uuid: map['uuid'] as String?,
        title: map['title'] as String,
        done: (map['done'] as int? ?? 0) != 0,
        createdAt:
            DateTime.fromMillisecondsSinceEpoch(map['created_at'] as int),
        updatedAt:
            DateTime.fromMillisecondsSinceEpoch(map['updated_at'] as int),
        deleted: (map['deleted'] as int? ?? 0) != 0,
      );

  final int? id;
  final String? uuid;
  final String title;
  final bool done;
  final DateTime createdAt;
  final DateTime updatedAt;
  final bool deleted;

  /// 入库后回填自增主键用。
  Task withId(int id) => Task(
        id: id,
        uuid: uuid,
        title: title,
        done: done,
        createdAt: createdAt,
        updatedAt: updatedAt,
        deleted: deleted,
      );

  Task copyWith({
    String? uuid,
    String? title,
    bool? done,
    DateTime? updatedAt,
    bool? deleted,
  }) =>
      Task(
        id: id,
        uuid: uuid ?? this.uuid,
        title: title ?? this.title,
        done: done ?? this.done,
        createdAt: createdAt,
        updatedAt: updatedAt ?? this.updatedAt,
        deleted: deleted ?? this.deleted,
      );

  Map<String, Object?> toMap() => {
        if (id != null) 'id': id,
        if (uuid != null) 'uuid': uuid,
        'title': title,
        'done': done ? 1 : 0,
        'created_at': createdAt.millisecondsSinceEpoch,
        'updated_at': updatedAt.millisecondsSinceEpoch,
        'deleted': deleted ? 1 : 0,
      };
}
