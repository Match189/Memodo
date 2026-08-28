import 'dart:async';

/// 同步通道的统一接口：核心合并逻辑只认这个抽象，
/// WebDAV / OSS / 自建服务器各自实现，设置页可切换。
abstract interface class SyncTransport {
  String get displayName;

  /// 连通性与鉴权检查。远端还没有快照不算失败。
  Future<void> testConnection();

  /// 拉取快照正文；远端不存在快照时返回 null。
  Future<String?> fetchSnapshot();

  /// 上传快照正文（整体覆盖）。
  Future<void> uploadSnapshot(String body);
}

/// 常见网络错误转成可读的中文提示。
String describeTransportError(Object error) {
  if (error is TimeoutException) {
    return '连接超时，请检查网络或代理';
  }
  final s = error.toString();
  if (s.contains('401') || s.contains('403')) {
    return '鉴权失败，请检查账号/密码/密钥/令牌';
  }
  if (s.contains('SocketException') || s.contains('Failed host lookup')) {
    return '无法连接服务器，请检查地址和网络';
  }
  return '同步出错：$s';
}
