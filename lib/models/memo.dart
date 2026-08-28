/// 一条备忘（标题 + 正文）。
///
/// [uuid] 是跨设备的全局标识；[deleted] 是软删除墓碑，含义同任务表。
class Memo {
  const Memo({
    this.id,
    this.uuid,
    required this.title,
    required this.content,
    required this.createdAt,
    required this.updatedAt,
    this.deleted = false,
  });

  factory Memo.fromMap(Map<String, Object?> map) => Memo(
        id: map['id'] as int?,
        uuid: map['uuid'] as String?,
        title: map['title'] as String,
        content: map['content'] as String? ?? '',
        createdAt:
            DateTime.fromMillisecondsSinceEpoch(map['created_at'] as int),
        updatedAt:
            DateTime.fromMillisecondsSinceEpoch(map['updated_at'] as int),
        deleted: (map['deleted'] as int? ?? 0) != 0,
      );

  final int? id;
  final String? uuid;
  final String title;
  final String content;
  final DateTime createdAt;
  final DateTime updatedAt;
  final bool deleted;

  /// 入库后回填自增主键用。
  Memo withId(int id) => Memo(
        id: id,
        uuid: uuid,
        title: title,
        content: content,
        createdAt: createdAt,
        updatedAt: updatedAt,
        deleted: deleted,
      );

  Memo copyWith({
    String? uuid,
    String? title,
    String? content,
    DateTime? updatedAt,
    bool? deleted,
  }) =>
      Memo(
        id: id,
        uuid: uuid ?? this.uuid,
        title: title ?? this.title,
        content: content ?? this.content,
        createdAt: createdAt,
        updatedAt: updatedAt ?? this.updatedAt,
        deleted: deleted ?? this.deleted,
      );

  Map<String, Object?> toMap() => {
        if (id != null) 'id': id,
        if (uuid != null) 'uuid': uuid,
        'title': title,
        'content': content,
        'created_at': createdAt.millisecondsSinceEpoch,
        'updated_at': updatedAt.millisecondsSinceEpoch,
        'deleted': deleted ? 1 : 0,
      };
}
