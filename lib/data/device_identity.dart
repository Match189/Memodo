import 'dart:io';

import 'package:uuid/uuid.dart';

import 'settings_store.dart';

/// 本机设备身份（SPD §9/§19）：形如 `windows-1a2b3c4d`，
/// 存本地 settings 表，首次生成后不变；同步时用于 LWW 平局决胜。
class DeviceIdentity {
  DeviceIdentity._(this.id);

  final String id;

  static DeviceIdentity? _instance;

  static const _key = 'device.id';

  static Future<DeviceIdentity> load(SettingsStore store) async {
    final cached = _instance;
    if (cached != null) return cached;
    final existing = await store.read(_key);
    if (existing != null && existing.isNotEmpty) {
      return _instance = DeviceIdentity._(existing);
    }
    final id = '${_platformName()}-${const Uuid().v4().substring(0, 8)}';
    await store.write(_key, id);
    return _instance = DeviceIdentity._(id);
  }

  static String _platformName() {
    if (Platform.isWindows) return 'windows';
    if (Platform.isAndroid) return 'android';
    if (Platform.isMacOS) return 'macos';
    if (Platform.isLinux) return 'linux';
    return 'device';
  }
}
