import 'package:http/http.dart' as http;

import '../sync_transport.dart';

/// 自建同步服务器通道，协议非常简单：
///   GET  `{baseUrl}/snapshot`  → 200 正文 / 404 无快照
///   PUT  `{baseUrl}/snapshot`  → 2xx 成功
/// 请求头带 `Authorization: Bearer <token>`。
/// 服务端参考实现在项目 server/ 目录。
class ServerTransport implements SyncTransport {
  ServerTransport({
    required this.baseUrl,
    required this.token,
    http.Client? client,
  }) : _client = client ?? http.Client();

  /// 如 http://192.168.1.10:8080 或 https://sync.example.com
  final String baseUrl;
  final String token;

  final http.Client _client;

  static const _timeout = Duration(seconds: 30);

  Uri get _snapshotUri {
    final base = Uri.parse(baseUrl);
    final path =
        '${base.path.endsWith('/') ? base.path.substring(0, base.path.length - 1) : base.path}/snapshot';
    return base.replace(path: path);
  }

  Map<String, String> get _headers => {
        'Authorization': 'Bearer $token',
      };

  @override
  String get displayName => '自建服务器';

  @override
  Future<void> testConnection() async {
    final response = await _client
        .get(_snapshotUri, headers: _headers)
        .timeout(_timeout);
    if (response.statusCode == 404) return; // 服务器正常，还没存过快照
    if (response.statusCode == 401 || response.statusCode == 403) {
      throw Exception('服务器鉴权失败，请检查访问令牌');
    }
    if (response.statusCode >= 400) {
      throw Exception('服务器异常（${response.statusCode}）');
    }
  }

  @override
  Future<String?> fetchSnapshot() async {
    final response = await _client
        .get(_snapshotUri, headers: _headers)
        .timeout(_timeout);
    if (response.statusCode == 404) return null;
    if (response.statusCode == 200) return response.body;
    throw Exception('服务器读取失败（${response.statusCode}）');
  }

  @override
  Future<void> uploadSnapshot(String body) async {
    final response = await _client
        .put(
          _snapshotUri,
          headers: {..._headers, 'Content-Type': 'application/json; charset=utf-8'},
          body: body,
        )
        .timeout(_timeout);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('服务器上传失败（${response.statusCode}）');
    }
  }
}
