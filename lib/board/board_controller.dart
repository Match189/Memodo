import 'dart:convert';
import 'dart:math' as math;

import 'package:flutter/foundation.dart';

import '../data/board_repository.dart';
import '../data/settings_store.dart';
import 'base_card.dart';

/// 单张板卡的视图状态：实体记录 + 可独立更新的布局（拖动只重建本卡）。
class BoardCardView {
  BoardCardView({required this.record, required this.layout})
      : layoutNotifier = ValueNotifier(layout);

  final BoardCardRecord record;
  BoardCardLayout layout;
  final ValueNotifier<BoardCardLayout> layoutNotifier;
}

/// Board 控制器（规格 §15）：板卡的装载、增删、拖动/缩放的内存更新与落盘。
/// 布局（x/y/w/h/旋转/z）是**本机视觉状态**，存本地 kv，不进同步协议。
class BoardController extends ChangeNotifier {
  BoardController({
    required BoardRepository boardRepository,
    required SettingsStore settingsStore,
  })  : _repo = boardRepository,
        _store = settingsStore;

  final BoardRepository _repo;
  final SettingsStore _store;

  static const _layoutKeyPrefix = 'board.layout.';

  String? boardUuid;
  final List<BoardCardView> cards = [];

  /// 网格吸附（拖动结束时取整到 8px）。
  bool snapToGrid = false;

  bool _loaded = false;
  bool _loading = false;

  Future<void> load() async {
    if (_loaded || _loading) return;
    _loading = true;
    try {
      boardUuid = await _repo.ensureDefaultBoard();
      final records = await _repo.listCards(boardUuid!);
      final Map<String, Map<String, Object?>> layouts =
          await _loadLayouts();
      cards
        ..clear()
        ..addAll([
          for (final r in records)
            BoardCardView(
              record: r,
              layout: layouts.containsKey(r.uuid)
                  ? BoardCardLayout.fromJson(layouts[r.uuid]!)
                  : _seedLayout(cards.length),
            )
        ]);
      _loaded = true;
      notifyListeners();
    } finally {
      _loading = false;
    }
  }

  BoardCardLayout _seedLayout(int index) {
    final rng = index;
    return BoardCardLayout(
      x: 48 + (rng % 5) * 36.0,
      y: 64 + (rng % 4) * 30.0,
      rotationDegrees: _randomRotation(rng),
    );
  }

  /// 视觉旋转种子：±1.5° 内（规格 §7），随卡片生成一次后持久化。
  static double _randomRotation(int seed) {
    final values = [-1.4, -0.8, 0.0, 0.7, 1.3];
    return values[seed % values.length];
  }

  BoardCardView? viewOf(String cardUuid) {
    for (final v in cards) {
      if (v.record.uuid == cardUuid) return v;
    }
    return null;
  }

  /// 钉一条 Todo/Memo 上板。
  Future<BoardCardView?> pinCard({
    required String refType,
    required String refUuid,
  }) async {
    if (boardUuid == null) {
      boardUuid = await _repo.ensureDefaultBoard();
    }
    final rec = await _repo.pinCard(
      boardUuid: boardUuid!,
      refType: refType,
      refUuid: refUuid,
    );
    if (rec == null) return null; // 已在板上
    final view = BoardCardView(
      record: rec,
      layout: _seedLayout(cards.length),
    );
    cards.add(view);
    await persistLayouts();
    notifyListeners();
    return view;
  }

  /// 从板上取下（实体不受影响）。
  Future<void> unpin(BoardCardView view) async {
    await _repo.unpin(view.record);
    cards.remove(view);
    await persistLayouts();
    notifyListeners();
  }

  /// 点击置顶：让该卡的 z 严格高于其他所有卡。
  void bringToFront(BoardCardView view) {
    final maxOther = cards.fold<int>(0, (m, v) {
      if (identical(v, view)) return m;
      return math.max(m, v.layout.z);
    });
    if (view.layout.z > maxOther) return;
    view.layout.z = maxOther + 1;
    _pushLayout(view);
    schedulePersist();
  }

  /// 拖动：只更新本卡布局（规格 §31：PointerMove 不全局 rebuild、不写库）。
  void dragBy(BoardCardView view, double dx, double dy) {
    view.layout
      ..x += dx
      ..y += dy;
    _pushLayout(view);
  }

  /// 缩放：右下角手柄增量。
  void resizeBy(BoardCardView view, double dw, double dh) {
    view.layout
      ..w = (view.layout.w + dw).clamp(140.0, 800.0)
      ..h = (view.layout.h + dh).clamp(100.0, 800.0);
    _pushLayout(view);
  }

  void _pushLayout(BoardCardView view) {
    view.layoutNotifier.value = BoardCardLayout(
      x: view.layout.x,
      y: view.layout.y,
      w: view.layout.w,
      h: view.layout.h,
      rotationDegrees: view.layout.rotationDegrees,
      z: view.layout.z,
    );
  }

  /// 拖动/缩放结束：吸附取整 + 落盘。
  Future<void> endGesture(BoardCardView view) async {
    if (snapToGrid) {
      const g = 8.0;
      view.layout
        ..x = (view.layout.x / g).roundToDouble() * g
        ..y = (view.layout.y / g).roundToDouble() * g;
      _pushLayout(view);
    }
    await persistLayouts();
  }

  bool _persistScheduled = false;

  /// 合并写盘（防抖由调用方保证语义；这里立即写，量很小）。
  Future<void> persistLayouts() async {
    if (boardUuid == null) return;
    final map = {
      for (final v in cards)
        v.record.uuid: v.layout.toJson(),
    };
    await _store.write('$_layoutKeyPrefix$boardUuid', map);
  }

  /// 变更后的延迟落盘（拖动结束时调用 endGesture 即可，此方法备用）。
  void schedulePersist() {
    if (_persistScheduled) return;
    _persistScheduled = true;
    Future<void>.delayed(const Duration(milliseconds: 300), () async {
      _persistScheduled = false;
      await persistLayouts();
    });
  }

  Future<Map<String, Map<String, Object?>>> _loadLayouts() async {
    if (boardUuid == null) return {};
    final raw = await _store.read('$_layoutKeyPrefix$boardUuid');
    if (raw == null) return {};
    try {
      final decoded = (jsonDecode(raw) as Map)
          .cast<String, Map<dynamic, dynamic>>()
          .map((k, v) => MapEntry(k, v.cast<String, Object?>()));
      return decoded;
    } catch (_) {
      return {};
    }
  }
}
