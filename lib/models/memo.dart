/// 一条备忘（标题 + 正文）。
///
/// [uuid] 跨设备全局标识；[deletedAt] 软删除墓碑；[deviceId] 最后修改来源，
/// 语义同任务表（SPD §17/18）。
class Memo {
  const Memo({
    this.id,
    this.uuid,
    required this.title,
    required this.content,
    required this.createdAt,
    required this.updatedAt,
    this.deleted = false,
    this.deletedAt,
    this.deviceId,
  });

  factory Memo.fromMap(Map<String, Object?> map) {
    final deletedAtMs = map['deleted_at'] as int?;
    final legacyDeleted = (map['deleted'] as int? ?? 0) != 0;
    return Memo(
      id: map['id'] as int?,
      uuid: map['uuid'] as String?,
      title: map['title'] as String,
      content: map['content'] as String? ?? '',
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
  final String content;
  final DateTime createdAt;
  final DateTime updatedAt;
  final bool deleted;
  final DateTime? deletedAt;
  final String? deviceId;

  /// 入库后回填自增主键用。
  Memo withId(int id) => Memo(
        id: id,
        uuid: uuid,
        title: title,
        content: content,
        createdAt: createdAt,
        updatedAt: updatedAt,
        deleted: deleted,
        deletedAt: deletedAt,
        deviceId: deviceId,
      );

  Memo copyWith({
    String? uuid,
    String? title,
    String? content,
    DateTime? updatedAt,
    DateTime? deletedAt,
    String? deviceId,
  }) =>
      Memo(
        id: id,
        uuid: uuid ?? this.uuid,
        title: title ?? this.title,
        content: content ?? this.content,
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
        'content': content,
        'created_at': createdAt.millisecondsSinceEpoch,
        'updated_at': updatedAt.millisecondsSinceEpoch,
        'deleted': deleted ? 1 : 0,
        'deleted_at': deletedAt?.millisecondsSinceEpoch,
        if (deviceId != null) 'device_id': deviceId,
      };
}
