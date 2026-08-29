import 'package:flutter/material.dart';

/// Board 主题（SPD 图钉板规格 §12）：背景、卡片、图钉、阴影的视觉参数。
/// BoardTheme 只管视觉，不承载 Todo/Memo 业务逻辑。
class BoardThemeData {
  const BoardThemeData({
    required this.id,
    required this.label,
    required this.boardBase,
    required this.boardNoise,
    required this.boardVignette,
    required this.cardSurface,
    required this.cardBorder,
    required this.pinColor,
    required this.pinHighlight,
    required this.sectionText,
    required this.dark,
  });

  final String id;
  final String label;

  /// 软木板/玻璃底色（背景填充）
  final Color boardBase;

  /// 噪点/杂色（低频纹理）
  final Color boardNoise;

  /// 四角渐暗
  final Color boardVignette;

  /// 卡片纸面
  final Color cardSurface;

  /// 卡片描边
  final Color cardBorder;

  /// 图钉主体
  final Color pinColor;

  /// 图钉高光
  final Color pinHighlight;

  /// 分区/标题文字
  final Color sectionText;

  final bool dark;
}

/// 内置板主题：软木板 / 毛玻璃（各含深浅两态，跟随应用主题模式）。
class BoardThemes {
  BoardThemes._();

  static const corkId = 'cork';
  static const glassId = 'glass';

  static BoardThemeData resolve(String id, Brightness brightness) {
    final dark = brightness == Brightness.dark;
    if (id == glassId) return dark ? _glassDark : _glassLight;
    return dark ? _corkDark : _corkLight;
  }

  static BoardThemeData corkOf(Brightness brightness) =>
      resolve(corkId, brightness);

  static BoardThemeData glassOf(Brightness brightness) =>
      resolve(glassId, brightness);

  // ---- 软木板（暖棕 + 纸卡 + 红钉）----
  static const _corkLight = BoardThemeData(
    id: corkId,
    label: '软木板',
    boardBase: Color(0xFFD9B38C),
    boardNoise: Color(0x33A97C50),
    boardVignette: Color(0x556B4A2F),
    cardSurface: Color(0xFFFDF8EF),
    cardBorder: Color(0x22000000),
    pinColor: Color(0xFFD3453C),
    pinHighlight: Color(0x88FFFFFF),
    sectionText: Color(0xFF6B4A2F),
    dark: false,
  );

  static final _corkDark = BoardThemeData(
    id: corkId,
    label: '软木板',
    boardBase: Color(0xFF2B211A),
    boardNoise: Color(0x264A3826),
    boardVignette: Color(0x88000000),
    cardSurface: Color(0xFF3A2F26),
    cardBorder: Color(0x33FFFFFF),
    pinColor: Color(0xFFE05A50),
    pinHighlight: Color(0x66FFFFFF),
    sectionText: Color(0xFFC8A882),
    dark: true,
  );

  // ---- 毛玻璃（冷灰蓝 + 半透明卡 + 玻璃钉）----
  static const _glassLight = BoardThemeData(
    id: glassId,
    label: '毛玻璃',
    boardBase: Color(0xFFDFE7EC),
    boardNoise: Color(0x22FFFFFF),
    boardVignette: Color(0x33546E7A),
    cardSurface: Color(0xCCFFFFFF),
    cardBorder: Color(0x33000000),
    pinColor: Color(0xFF546E7A),
    pinHighlight: Color(0xAAFFFFFF),
    sectionText: Color(0xFF37474F),
    dark: false,
  );

  static final _glassDark = BoardThemeData(
    id: glassId,
    label: '毛玻璃',
    boardBase: Color(0xFF14181C),
    boardNoise: Color(0x22FFFFFF),
    boardVignette: Color(0x99000000),
    cardSurface: Color(0x59222A30),
    cardBorder: Color(0x33FFFFFF),
    pinColor: Color(0xFF90A4AE),
    pinHighlight: Color(0x88FFFFFF),
    sectionText: Color(0xFFB0BEC5),
    dark: true,
  );
}
