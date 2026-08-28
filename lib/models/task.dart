/// 一条待办任务。
///
/// SPD §17/18/19：
/// - [uuid] 是跨设备的全局标识（同步按它合并，本地自增 id 只在本机有意义）
/// - [deletedAt] 是软删除墓碑时间戳；[deleted] 为兼容旧快照的派生列
/// - [deviceId] 记录最后修改来源，LWW 平局时作确定性决胜
class Task {
  const Task({
    this.id,
    this.uuid,
    required this.title,
    this.description = '',
    required this.done,
    this.priority = 0,
    this.dueAt,
    required this.createdAt,
    required this.updatedAt,
    this.deleted = false,
    this.deletedAt,
    this.deviceId,
  });

  factory Task.fromMap(Map<String, Object?> map) {
    final deletedAtMs = map['deleted_at'] as int?;
    final legacyDeleted = (map['deleted'] as int? ?? 0) != 0;
    return Task(
      id: map['id'] as int?,
      uuid: map['uuid'] as String?,
      title: map['title'] as String,
      description: map['description'] as String? ?? '',
      done: (map['done'] as int? ?? 0) != 0,
      priority: map['priority'] as int? ?? 0,
      dueAt: map['due_at'] == null
          ? null
          : DateTime.fromMillisecondsSinceEpoch(map['due_at'] as int),
      createdAt:
          DateTime.fromMillisecondsSinceEpoch(map['created_at'] as int),
      updatedAt:
          DateTime.fromMillisecondsSinceEpoch(map['updated_at'] as int),
      deleted: legacyDeleted || deletedAtMs != null,
      deletedAt: deletedAtMs == null
          ? null
          : DateTime.fromMillisecondsSinceEpoch(deletedAtMs),
      deviceId: map['device_id'] as String?,
    );
  }

  final int? id;
  final String? uuid;
  final String title;
  final String description;
  final bool done;
  final int priority;
  final DateTime? dueAt;
  final DateTime createdAt;
  final DateTime updatedAt;
  final bool deleted;
  final DateTime? deletedAt;
  final String? deviceId;

  /// 入库后回填自增主键用。
  Task withId(int id) => Task(
        id: id,
        uuid: uuid,
        title: title,
        description: description,
        done: done,
        priority: priority,
        dueAt: dueAt,
        createdAt: createdAt,
        updatedAt: updatedAt,
        deleted: deleted,
        deletedAt: deletedAt,
        deviceId: deviceId,
      );

  Task copyWith({
    String? uuid,
    String? title,
    String? description,
    bool? done,
    int? priority,
    DateTime? dueAt,
    DateTime? updatedAt,
    bool clearDueAt = false,
    DateTime? deletedAt,
    String? deviceId,
  }) =>
      Task(
        id: id,
        uuid: uuid ?? this.uuid,
        title: title ?? this.title,
        description: description ?? this.description,
        done: done ?? this.done,
        priority: priority ?? this.priority,
        dueAt: clearDueAt ? null : (dueAt ?? this.dueAt),
        createdAt: createdAt,
        updatedAt: updatedAt ?? this.updatedAt,
        deleted: deletedAt != null || this.deleted,
        deletedAt: deletedAt ?? this.deletedAt,
        deviceId: deviceId ?? this.deviceId,
      );

  Map<String, Object?> toMap() => {
        if (id != null) 'id': id,
        if (uuid != null) 'uuid': uuid,
        'title': title,
        'description': description,
        'done': done ? 1 : 0,
        'priority': priority,
        'due_at': dueAt?.millisecondsSinceEpoch,
        'created_at': createdAt.millisecondsSinceEpoch,
        'updated_at': updatedAt.millisecondsSinceEpoch,
        'deleted': deleted ? 1 : 0,
        'deleted_at': deletedAt?.millisecondsSinceEpoch,
        if (deviceId != null) 'device_id': deviceId,
      };
}
