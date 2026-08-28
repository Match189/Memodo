import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:todolist/sync/transports/oss_transport.dart';
import 'package:todolist/sync/transports/server_transport.dart';

void main() {
  group('ServerTransport（对接本地真实 HttpServer）', () {
    late HttpServer server;
    late String stored;
    var hasSnapshot = false;

    setUp(() async {
      stored = '';
      hasSnapshot = false;
      server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
      server.listen((request) async {
        final path = request.uri.path;
        final auth = request.headers.value('authorization') ?? '';
        if (path != '/snapshot') {
          request.response.statusCode = 404;
          await request.response.close();
          return;
        }
        if (auth != 'Bearer t1') {
          request.response.statusCode = 401;
          await request.response.close();
          return;
        }
        switch (request.method) {
          case 'GET':
            if (!hasSnapshot) {
              request.response.statusCode = 404;
            } else {
              request.response.write(stored);
            }
            await request.response.close();
          case 'PUT':
            stored = await utf8.decodeStream(request);
            hasSnapshot = true;
            request.response.statusCode = 204;
            await request.response.close();
          default:
            request.response.statusCode = 405;
            await request.response.close();
        }
      });
    });

    tearDown(() async => server.close(force: true));

    test('404 → null；PUT 后能读回', () async {
      final transport = ServerTransport(
        baseUrl: 'http://${server.address.host}:${server.port}',
        token: 't1',
      );

      expect(await transport.fetchSnapshot(), isNull);

      await transport.uploadSnapshot('{"hello":"world"}');
      expect(await transport.fetchSnapshot(), '{"hello":"world"}');
    });

    test('令牌错误被识别为异常', () async {
      final transport = ServerTransport(
        baseUrl: 'http://${server.address.host}:${server.port}',
        token: 'wrong',
      );
      // 测试服务器对错误令牌直接断开（无响应）→ 抛异常；连通性检查应失败。
      expect(transport.testConnection(), throwsA(anything));
    });
  });

  group('OSS 通道（MockClient 校验签名请求）', () {
    test('PUT 带 OSS V1 签名头；GET 404 → null', () async {
      String? capturedAuth;
      String? capturedDate;
      Future<http.Response> mockHandler(http.Request request) async {
        capturedAuth = request.headers['Authorization'];
        capturedDate = request.headers['Date'];
        if (request.method == 'GET') {
          return http.Response('not found', 404);
        }
        return http.Response('', 200);
      }

      final transport = OssTransport(
        endpoint: 'oss-cn-hangzhou.aliyuncs.com',
        bucket: 'my-bucket',
        accessKeyId: 'AKID',
        accessKeySecret: 'SECRET',
        client: MockClient(mockHandler),
      );

      expect(await transport.fetchSnapshot(), isNull);
      expect(capturedAuth, startsWith('OSS AKID:'));
      expect(capturedDate, matches(RegExp(r'^[A-Z][a-z]{2}, \d{2} \w{3} \d{4} \d{2}:\d{2}:\d{2} GMT$')));

      await transport.uploadSnapshot('body');
      expect(capturedAuth, isNotNull);
      expect(capturedDate, isNotNull);
    });

    test('httpDate 输出 RFC1123 GMT 格式', () {
      expect(
        httpDate(DateTime.utc(2026, 8, 26, 12, 0, 5)),
        'Wed, 26 Aug 2026 12:00:05 GMT',
      );
    });
  });
}
