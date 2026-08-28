import 'package:flutter/material.dart';

import 'theme_settings.dart';

/// 统一的 ThemeData 工厂：由主题色种子 + AMOLED 开关生成浅色/深色主题。
/// 所有页面不写死颜色，统一从 Theme 派生（深度美化的基础设施）。
class AppTheme {
  AppTheme._();

  static const seedFallback = Color(0xFF00696D);

  static ThemeData light(Color seed) =>
      _build(ColorScheme.fromSeed(seedColor: seed), Brightness.light);

  static ThemeData dark(Color seed, {bool amoled = false}) {
    final scheme =
        ColorScheme.fromSeed(seedColor: seed, brightness: Brightness.dark);
    if (!amoled) return _build(scheme, Brightness.dark);
    // AMOLED 纯黑：把面层压到纯黑并收敛描边
    final black = scheme.copyWith(
      surface: Colors.black,
      surfaceContainerLowest: Colors.black,
      surfaceContainerLow: const Color(0xFF0A0A0A),
      surfaceContainer: const Color(0xFF101010),
      surfaceContainerHigh: const Color(0xFF161616),
      surfaceContainerHighest: const Color(0xFF1C1C1C),
    );
    return _build(black, Brightness.dark, scaffold: Colors.black);
  }

  static ThemeData _build(
    ColorScheme scheme,
    Brightness brightness, {
    Color? scaffold,
  }) {
    final isDark = brightness == Brightness.dark;
    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: scaffold,
      appBarTheme: AppBarTheme(
        centerTitle: false,
        elevation: 0,
        scrolledUnderElevation: 2,
        backgroundColor: isDark ? scaffold : scheme.surfaceContainer,
      ),
      cardTheme: CardThemeData(
        elevation: 0,
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        color: scheme.surfaceContainerLow,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
        filled: true,
        fillColor: scheme.onSurface.withValues(alpha: 0.04),
      ),
      snackBarTheme: const SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
      ),
      dividerTheme: DividerThemeData(
        color: scheme.outline.withValues(alpha: 0.2),
      ),
    );
  }
}
