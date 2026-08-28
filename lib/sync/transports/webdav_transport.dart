import 'dart:convert';

import 'package:http/http.dart' as http;

import '../sync_transport.dart';

/// 通用 WebDAV 通道（坚果云、InfiniCloud 等任何支持 WebDAV 的网盘）。
///
/// 只用到 WebDAV 的一小块：MKCOL（建目录）、PROPFIND（探测）、GET、PUT。
class WebdavTransport implements SyncTransport {
  WebdavTransport({
    required this.baseUrl,
    required this.folder,
    required this.username,
    required this.password,
    http.Client? client,
  })  : _client = client ?? http.Client(),
        fileName = 'todolist-snapshot.json';

  /// 服务根地址，如 https://dav.jianguoyun.com/dav/
  final String baseUrl;
  final String folder;
  final String username;
  final String password;
  final String fileName;

  final http.Client _client;

  static const _timeout = Duration(seconds: 30);

  Map<String, String> get _authHeader => {
        'Authorization':
            'Basic ${base64Encode(utf8.encode('$username:$password'))}',
      };

  /// 根地址路径 + folder 拆成段，逐段 MKCOL（已存在会被忽略）。
  List<String> get _folderSegments {
    final baseUri = Uri.parse(baseUrl);
    final segments = [
      ...baseUri.path.split('/').where((s) => s.isNotEmpty),
      ...folder.split('/').where((s) => s.isNotEmpty),
    ];
    return segments;
  }

  Uri _uriFor(List<String> segments, {String? file}) {
    final baseUri = Uri.parse(baseUrl);
    final fullPath = '/${[...segments, ?file].join('/')}';
    return baseUri.replace(path: fullPath);
  }

  @override
  String get displayName => 'WebDAV';

  Future<void> _ensureFolders() async {
    final segments = _folderSegments;
    var built = <String>[];
    for (final segment in segments) {
      built = [...built, segment];
      final uri = _uriFor(built);
      final request = http.Request('MKCOL', uri)..headers.addAll(_authHeader);
      try {
        final response =
            await _client.send(request).then(http.Response.fromStream);
        // 201 创建成功；405 已存在；其余当次忽略，最终以 GET/PUT 结果为准。
        if (response.statusCode >= 200 && response.statusCode < 400) continue;
        if (response.statusCode == 405) continue;
      } catch (_) {
        // 网络盘偶发 MKCOL 失败不致命，继续尝试下一级/直接使用。
      }
    }
  }

  @override
  Future<void> testConnection() async {
    final baseUri = Uri.parse(baseUrl);
    final request = http.Request('PROPFIND', baseUri)
      ..headers.addAll(_authHeader)
      ..headers['Depth'] = '0';
    final response = await _client
        .send(request)
        .then(http.Response.fromStream)
        .timeout(_timeout);
    if (response.statusCode == 401 || response.statusCode == 403) {
      throw Exception('WebDAV 鉴权失败（${response.statusCode}）');
    }
    if (response.statusCode >= 400) {
      throw Exception('WebDAV 服务异常（${response.statusCode}），请检查地址');
    }
    await _ensureFolders();
  }

  @override
  Future<String?> fetchSnapshot() async {
    await _ensureFolders();
    final uri = _uriFor(_folderSegments, file: fileName);
    final response = await _client
        .get(uri, headers: _authHeader)
        .timeout(_timeout);
    if (response.statusCode == 404) return null;
    if (response.statusCode == 200) return response.body;
    throw Exception('WebDAV 读取失败（${response.statusCode}）');
  }

  @override
  Future<void> uploadSnapshot(String body) async {
    await _ensureFolders();
    final uri = _uriFor(_folderSegments, file: fileName);
    final response = await _client
        .put(
          uri,
          headers: {
            ..._authHeader,
            'Content-Type': 'application/json; charset=utf-8',
          },
          body: body,
        )
        .timeout(_timeout);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('WebDAV 上传失败（${response.statusCode}）');
    }
  }
}
