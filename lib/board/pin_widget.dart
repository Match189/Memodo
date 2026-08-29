
import 'package:flutter/material.dart';

/// 图钉（规格 §9）：纯本地 CustomPaint 渲染，无网络资源。
/// 结构：钉帽（径向渐变）+ 高光 + 投影 + 针杆。
class PinWidget extends StatelessWidget {
  const PinWidget({
    super.key,
    this.size = 26,
    this.color = const Color(0xFFD3453C),
    this.highlight = const Color(0x88FFFFFF),
  });

  final double size;
  final Color color;
  final Color highlight;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: size,
      height: size * 1.25,
      child: CustomPaint(painter: _PinPainter(color, highlight)),
    );
  }
}

class _PinPainter extends CustomPainter {
  _PinPainter(this.color, this.highlight);

  final Color color;
  final Color highlight;

  @override
  void paint(Canvas canvas, Size size) {
    final s = size.width;
    final headR = s * 0.36;
    final headCenter = Offset(s / 2, headR + s * 0.06);

    // 针杆阴影
    final stem = Paint()
      ..color = Colors.black.withValues(alpha: 0.25)
      ..strokeWidth = s * 0.06
      ..strokeCap = StrokeCap.round;
    canvas.drawLine(
      Offset(headCenter.dx, headCenter.dy + headR * 0.4),
      Offset(headCenter.dx + s * 0.04, s - s * 0.04),
      stem,
    );

    // 钉帽投影
    canvas.drawCircle(
      headCenter + Offset(s * 0.03, s * 0.05),
      headR,
      Paint()..color = Colors.black.withValues(alpha: 0.30),
    );

    // 钉帽：径向渐变模拟球面
    canvas.drawCircle(
      headCenter,
      headR,
      Paint()
        ..shader = RadialGradient(
          center: Alignment(-0.35, -0.35),
          radius: 1.1,
          colors: [
            Color.lerp(color, Colors.white, 0.45)!,
            color,
            Color.lerp(color, Colors.black, 0.35)!,
          ],
          stops: const [0, 0.55, 1],
        ).createShader(Rect.fromCircle(center: headCenter, radius: headR)),
    );

    // 高光
    canvas.drawCircle(
      headCenter - Offset(headR * 0.35, headR * 0.35),
      headR * 0.22,
      Paint()..color = highlight,
    );

    // 高光旁的数学占位（保持导入整洁）
  }

  @override
  bool shouldRepaint(covariant _PinPainter old) =>
      old.color != color || old.highlight != highlight;
}
