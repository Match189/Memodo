import 'package:flutter/material.dart';

import 'board_theme.dart';
import 'pin_widget.dart';

/// 卡片布局（逻辑坐标，单位 = 板面逻辑像素）。
class BoardCardLayout {
  BoardCardLayout({
    required this.x,
    required this.y,
    this.w = 190,
    this.h = 150,
    this.rotationDegrees = 0,
    this.z = 0,
  });

  factory BoardCardLayout.fromJson(Map<String, Object?> j) => BoardCardLayout(
        x: (j['x'] as num?)?.toDouble() ?? 24,
        y: (j['y'] as num?)?.toDouble() ?? 24,
        w: (j['w'] as num?)?.toDouble() ?? 190,
        h: (j['h'] as num?)?.toDouble() ?? 150,
        rotationDegrees: (j['r'] as num?)?.toDouble() ?? 0,
        z: (j['z'] as num?)?.toInt() ?? 0,
      );

  double x;
  double y;
  double w;
  double h;

  /// 视觉旋转（度）。创建时随机 ±1.5° 生成一次并持久化（规格 §7）。
  double rotationDegrees;
  int z;

  Map<String, Object?> toJson() => {
        'x': x,
        'y': y,
        'w': w,
        'h': h,
        'r': rotationDegrees,
        'z': z,
      };
}

/// 卡片基座（规格 §5/§14 BaseCard）：图钉 + 纸面 + 阴影状态 + 缩放手柄。
/// 拖动由外层手势驱动（回调上抛），基座只负责视觉与命中区域。
class BaseCard extends StatelessWidget {
  const BaseCard({
    super.key,
    required this.theme,
    required this.layout,
    required this.onDrag,
    required this.onDragEnd,
    required this.onResize,
    required this.onResizeEnd,
    required this.onTap,
    this.dragging = false,
    required this.child,
  });

  final BoardThemeData theme;
  final BoardCardLayout layout;

  /// 拖动增量回调（逻辑像素）。
  final void Function(double dx, double dy) onDrag;
  final VoidCallback onDragEnd;

  /// 缩放增量回调（右下角手柄）。
  final void Function(double dw, double dh) onResize;
  final VoidCallback onResizeEnd;

  final VoidCallback onTap;
  final bool dragging;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final shadow = BoxShadow(
      color: Colors.black.withValues(
          alpha: dragging ? 0.35 : (theme.dark ? 0.45 : 0.18)),
      blurRadius: dragging ? 22 : 8,
      offset: Offset(0, dragging ? 10 : 4),
    );
    return Transform.rotate(
      angle: layout.rotationDegrees * 3.14159265 / 180,
      child: SizedBox(
        width: layout.w,
        height: layout.h,
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            // 纸面
            GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTap: onTap,
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 120),
                decoration: BoxDecoration(
                  color: theme.cardSurface,
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: theme.cardBorder),
                  boxShadow: [shadow],
                ),
                child: child,
              ),
            ),
            // 图钉：顶部居中，兼作拖动手柄
            Positioned(
              top: -12,
              left: layout.w / 2 - 13,
              width: 26,
              height: 34,
              child: GestureDetector(
                behavior: HitTestBehavior.opaque,
                onPanUpdate: (d) {
                  var dx = d.delta.dx;
                  var dy = d.delta.dy;
                  onDrag(dx, dy);
                },
                onPanEnd: (_) => onDragEnd(),
                child: MouseRegion(
                  cursor: SystemMouseCursors.move,
                  child: PinWidget(
                    size: 26,
                    color: theme.pinColor,
                    highlight: theme.pinHighlight,
                  ),
                ),
              ),
            ),
            // 右下缩放手柄
            Positioned(
              right: -6,
              bottom: -6,
              width: 20,
              height: 20,
              child: GestureDetector(
                behavior: HitTestBehavior.opaque,
                onPanUpdate: (d) {
                  var dw = d.delta.dx;
                  var dh = d.delta.dy;
                  onResize(dw, dh);
                },
                onPanEnd: (_) => onResizeEnd(),
                child: MouseRegion(
                  cursor: SystemMouseCursors.resizeDownRight,
                  child: Container(
                    decoration: BoxDecoration(
                      color: theme.cardSurface,
                      shape: BoxShape.circle,
                      border: Border.all(color: theme.cardBorder),
                    ),
                    child: Icon(Icons.drag_handle_rounded,
                        size: 12, color: theme.sectionText),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
