import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:uuid/uuid.dart';

/// 图钉板实体（SPD Board 扩展 / docs/BOARD.md）：
/// Board / Card 两级（Section V1 用默认分区承载，模型已预留）。
/// Card 只引用实体（ref_type: todo|memo + ref_uuid），不复制内容。
class BoardRecord {
  const BoardRecord({
    required this.uuid,
    required this.name,
    required this.createdAt,
    required this.updatedAt,
  });

  factory BoardRecord.fromMap(Map<String, Object?> m) => BoardRecord(
        uuid: m['uuid'] as String,
        name: m['name'] as String,
        createdAt:
            DateTime.fromMillisecondsSinceEpoch(m['created_at'] as int),
        updatedAt:
            DateTime.fromMillisecondsSinceEpoch(m['updated_at'] as int),
      );

  final String uuid;
  final String name;
  final DateTime createdAt;
  final DateTime updatedAt;
}

/// 一张钉在板上的卡片：引用一条 Todo / Memo。
class BoardCardRecord {
  const BoardCardRecord({
    required this.uuid,
    required this.boardUuid,
    required this.refType,
    required this.refUuid,
    required this.createdAt,
    required this.updatedAt,
  });

  factory BoardCardRecord.fromMap(Map<String, Object?> m) => BoardCardRecord(
        uuid: m['uuid'] as String,
        boardUuid: m['board_uuid'] as String,
        refType: m['ref_type'] as String,
        refUuid: m['ref_uuid'] as String,
        createdAt:
            DateTime.fromMillisecondsSinceEpoch(m['created_at'] as int),
        updatedAt:
            DateTime.fromMillisecondsSinceEpoch(m['updated_at'] as int),
      );

  final String uuid;
  final String boardUuid;
  final String refType; // todo | memo
  final String refUuid;
  final DateTime createdAt;
  final DateTime updatedAt;
}

class BoardRepository {
  BoardRepository(this._db, {String deviceId = ''}) : _deviceId = deviceId;

  final Database _db;
  final String _deviceId;
  static const _uuid = Uuid();

  /// 确保存在默认板（首次进入图钉板时调用，幂等）。
  Future<String> ensureDefaultBoard() async {
    final rows = await _db.query('boards', limit: 1);
    if (rows.isNotEmpty) return rows.first['uuid'] as String;
    final uuid = _uuid.v4();
    final now = DateTime.now().millisecondsSinceEpoch;
    await _db.insert('boards', {
      'uuid': uuid,
      'name': '我的图钉板',
      'created_at': now,
      'updated_at': now,
      if (_deviceId.isNotEmpty) 'device_id': _deviceId,
    });
    return uuid;
  }

  Future<List<BoardRecord>> listBoards() async {
    final rows = await _db
        .query('boards', where: 'deleted = 0', orderBy: 'created_at ASC');
    return [for (final r in rows) BoardRecord.fromMap(r)];
  }

  /// 板上的全部卡片（不含软删除）。
  Future<List<BoardCardRecord>> listCards(String boardUuid) async {
    final rows = await _db.query('cards',
        where: 'board_uuid = ? AND deleted = 0',
        whereArgs: [boardUuid],
        orderBy: 'created_at ASC');
    return [for (final r in rows) BoardCardRecord.fromMap(r)];
  }

  /// 钉一条 Todo/Memo 上板；同一板上同实体唯一（重复返回 null）。
  Future<BoardCardRecord?> pinCard({
    required String boardUuid,
    required String refType,
    required String refUuid,
  }) async {
    final exists = await _db.query('cards',
        where:
            'board_uuid = ? AND ref_type = ? AND ref_uuid = ? AND deleted = 0',
        whereArgs: [boardUuid, refType, refUuid],
        limit: 1);
    if (exists.isNotEmpty) return null;
    final rec = BoardCardRecord(
      uuid: _uuid.v4(),
      boardUuid: boardUuid,
      refType: refType,
      refUuid: refUuid,
      createdAt: DateTime.now(),
      updatedAt: DateTime.now(),
    );
    await _db.insert('cards', {
      'uuid': rec.uuid,
      'board_uuid': rec.boardUuid,
      'ref_type': rec.refType,
      'ref_uuid': rec.refUuid,
      'created_at': rec.createdAt.millisecondsSinceEpoch,
      'updated_at': rec.updatedAt.millisecondsSinceEpoch,
      'deleted': 0,
      if (_deviceId.isNotEmpty) 'device_id': _deviceId,
    });
    return rec;
  }

  /// 从板上取下（软删除墓碑，实体本身不受影响）。
  Future<void> unpin(BoardCardRecord card) async {
    final now = DateTime.now().millisecondsSinceEpoch;
    await _db.update(
        'cards',
        {
          'deleted': 1,
          'deleted_at': now,
          'updated_at': now,
          if (_deviceId.isNotEmpty) 'device_id': _deviceId,
        },
        where: 'uuid = ?',
        whereArgs: [card.uuid]);
  }
}
