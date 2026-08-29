import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:memodo/sync/transports/oss_transport.dart';

void main() {
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
      expect(
          capturedDate,
          matches(RegExp(
              r'^[A-Z][a-z]{2}, \d{2} \w{3} \d{4} \d{2}:\d{2}:\d{2} GMT$')));

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
