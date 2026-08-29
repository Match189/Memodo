import 'dart:math' as math;

import 'dart:ui' show ImageFilter;

import 'package:flutter/material.dart';

import 'board_theme.dart';

/// Board 背景层（规格 §10/§11：低成本渲染）。
/// - 软木板：底色 + 种子噪点 + 渐变暗角（无图片资产）
/// - 毛玻璃：半透明底 + 单层 BackdropFilter 模糊（整板仅此一个 Blur）
class BoardBackground extends StatelessWidget {
  const BoardBackground({
    super.key,
    required this.theme,
    this.enableBlur = false,
    this.blurSigma = 12,
  });

  final BoardThemeData theme;
  final bool enableBlur;
  final double blurSigma;

  @override
  Widget build(BuildContext context) {
    final body = CustomPaint(
      painter: _BoardBackgroundPainter(theme),
      child: const SizedBox.expand(),
    );
    // 毛玻璃主题：整板唯一一层 BackdropFilter（规格 §11 禁止每卡一个 Blur）
    if (theme.id == BoardThemes.glassId && enableBlur) {
      return ClipRect(
        child: BackdropFilter(
          filter: ImageFilter.blur(
              sigmaX: blurSigma, sigmaY: blurSigma),
          child: body,
        ),
      );
    }
    return body;
  }
}

/// 软木板/玻璃底绘制：底色渐变 + 固定种子噪点 + 四角暗角。
/// 噪点用固定种子，重建不闪烁；数量与画布面积成比例，一次绘制。
class _BoardBackgroundPainter extends CustomPainter {
  _BoardBackgroundPainter(this.theme);

  final BoardThemeData theme;

  @override
  void paint(Canvas canvas, Size size) {
    // 底色渐变
    final rect = Offset.zero & size;
    final gradient = LinearGradient(
      begin: Alignment.topLeft,
      end: Alignment.bottomRight,
      colors: [theme.boardBase, Color.lerp(theme.boardBase, theme.boardVignette, 0.25)!],
    );
    canvas.drawRect(rect, Paint()..shader = gradient.createShader(rect));

    // 低频噪点（固定种子 → 稳定纹理）
    final rng = math.Random(20260829);
    final dot = Paint()..color = theme.boardNoise;
    final count = (size.width * size.height / 900).round().clamp(120, 900);
    for (var i = 0; i < count; i++) {
      final dx = rng.nextDouble() * size.width;
      final dy = rng.nextDouble() * size.height;
      final r = 1.0 + rng.nextDouble() * 2.2;
      canvas.drawCircle(Offset(dx, dy), r, dot);
    }

    // 四角暗角
    final vignette = Paint()
      ..shader = RadialGradient(
        radius: 1.1,
        colors: [Colors.transparent, theme.boardVignette],
      ).createShader(rect);
    canvas.drawRect(rect, vignette);
  }

  @override
  bool shouldRepaint(covariant _BoardBackgroundPainter oldDelegate) =>
      oldDelegate.theme != theme;
}
