// ignore_for_file: deprecated_member_use
import 'dart:ffi';
import 'dart:io';

import 'package:ffi/ffi.dart';
import 'package:win32/win32.dart';

/// Windows 开机自启：写/删 HKCU Run 注册表值（无需管理员）。
class Autostart {
  Autostart._();

  static const _runKey = r'Software\Microsoft\Windows\CurrentVersion\Run';
  static const _valueName = 'Memodo';

  static bool _isEnabled() {
    final keyPtr = calloc<HKEY>();
    try {
      if (RegOpenKey(HKEY_CURRENT_USER, TEXT(_runKey), keyPtr) !=
          ERROR_SUCCESS) {
        return false;
      }
      final typePtr = calloc<DWORD>();
      final sizePtr = calloc<DWORD>();
      try {
        final status = RegQueryValueEx(keyPtr.value, TEXT(_valueName),
            nullptr, typePtr, nullptr, sizePtr);
        return status == ERROR_SUCCESS;
      } finally {
        calloc.free(typePtr);
        calloc.free(sizePtr);
        RegCloseKey(keyPtr.value);
      }
    } finally {
      calloc.free(keyPtr);
    }
  }

  static Future<void> setEnabled(bool enabled) async {
    if (enabled) {
      final exePath = Platform.resolvedExecutable;
      final keyPtr = calloc<HKEY>();
      try {
        if (RegOpenKey(HKEY_CURRENT_USER, TEXT(_runKey), keyPtr) !=
            ERROR_SUCCESS) {
          throw Exception('无法打开注册表 Run 键');
        }
        final value = TEXT('"$exePath"');
        final bytes = value.length * 2 + 2;
        RegSetValueEx(
          keyPtr.value,
          TEXT(_valueName),
          0,
          REG_SZ,
          value.cast<Uint8>(),
          bytes,
        );
        RegCloseKey(keyPtr.value);
      } finally {
        calloc.free(keyPtr);
      }
    } else {
      final keyPtr = calloc<HKEY>();
      try {
        if (RegOpenKey(HKEY_CURRENT_USER, TEXT(_runKey), keyPtr) !=
            ERROR_SUCCESS) {
          return;
        }
        RegDeleteValue(keyPtr.value, TEXT(_valueName));
        RegCloseKey(keyPtr.value);
      } finally {
        calloc.free(keyPtr);
      }
    }
  }

  /// 当前状态与注册表对齐（应用启动时校正 UI）。
  static Future<bool> current() async => _isEnabled();
}
