import 'dart:convert';
import 'dart:io';

import 'package:args/args.dart';
import 'package:shelf/shelf.dart' as shelf;
import 'package:shelf/shelf_io.dart' as io;

/// 待办备忘同步服务器（参考实现）。
///
/// 协议：
///   GET  /snapshot   → 200 正文 / 404 无快照
///   PUT  /snapshot   → 204 成功（整体覆盖，上一版自动留作 .bak）
///   GET  /health     → 200（无需鉴权，供探活）
/// 除 /health 外都要求 Authorization: Bearer <token>。
///
/// 存储：快照就是一个 JSON 文件（data/snapshot.json），原子写入，
/// 对单人待办同步绰绰有余，无需数据库。
Future<void> main(List<String> args) async {
  final parser = ArgParser()
    ..addOption('port', abbr: 'p', defaultsTo: '8080')
    ..addOption('token', abbr: 't')
    ..addOption('data-dir', abbr: 'd', defaultsTo: 'data');
  final options = parser.parse(args);

  final token = (options['token'] as String?) ??
      Platform.environment['TODOLIST_TOKEN'];
  if (token == null || token.isEmpty) {
    stderr.writeln('必须提供 --token 或环境变量 TODOLIST_TOKEN');
    exit(2);
  }

  final port = int.parse(options['port'] as String);
  final store = SnapshotStore(Directory(options['data-dir'] as String));

  final handler = const shelf.Pipeline()
      .addMiddleware(shelf.logRequests())
      .addHandler(_route(token, store));

  final server = await io.serve(handler, InternetAddress.anyIPv4, port);
  stdout.writeln('todolist_server listening on port ${server.port}');
}

shelf.Handler _route(String token, SnapshotStore store) => (request) async {
      if (request.url.path == 'health') {
        return shelf.Response.ok('ok');
      }
      if (!request.url.path.startsWith('snapshot')) {
        return shelf.Response.notFound('not found');
      }
      final auth = request.headers['authorization'] ?? '';
      final provided = auth.startsWith('Bearer ') ? auth.substring(7) : '';
      if (!_constantTimeEquals(provided, token)) {
        return shelf.Response(401,
            body: jsonEncode({'error': 'unauthorized'}),
            headers: {'Content-Type': 'application/json'});
      }
      switch (request.method) {
        case 'GET':
          final snapshot = await store.read();
          if (snapshot == null) {
            return shelf.Response.notFound('no snapshot yet');
          }
          return shelf.Response.ok(snapshot,
              headers: {'Content-Type': 'application/json; charset=utf-8'});
        case 'PUT':
          final body = await request.readAsString();
          await store.write(body);
          return shelf.Response(204);
        default:
          return shelf.Response(405, body: 'method not allowed');
      }
    };

/// 快照文件的原子读写。
class SnapshotStore {
  SnapshotStore(Directory dir)
      : _file = File('${dir.path}/snapshot.json'),
        _backup = File('${dir.path}/snapshot.json.bak') {
    dir.createSync(recursive: true);
  }

  final File _file;
  final File _backup;

  Future<String?> read() async {
    if (!await _file.exists()) return null;
    return _file.readAsString();
  }

  Future<void> write(String body) async {
    // 上一版留作 .bak，误覆盖时可手工找回。
    if (await _file.exists()) {
      await _file.copy(_backup.path);
    }
    final tmp = File('${_file.path}.tmp');
    await tmp.writeAsString(body, flush: true);
    await tmp.rename(_file.path);
  }
}

/// 恒时比较，避免令牌校验的时序侧信道。
bool _constantTimeEquals(String a, String b) {
  if (a.length != b.length) return false;
  var diff = 0;
  for (var i = 0; i < a.length; i++) {
    diff |= a.codeUnitAt(i) ^ b.codeUnitAt(i);
  }
  return diff == 0;
}
