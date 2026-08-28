import 'dart:convert';

import 'package:crypto/crypto.dart' as crypto;
import 'package:http/http.dart' as http;

import '../sync_transport.dart';

/// 阿里云 OSS / 腾讯云 COS（兼容 S3 风格）对象存储通道。
///
/// 手写 V1 签名（HMAC-SHA1），不引 SDK：对单个固定对象来说
/// 只需要 PUT / GET 两个带签名的请求。
///
/// 建议使用 RAM 子账号，只授予目标 Bucket 的读写权限。
class OssTransport implements SyncTransport {
  OssTransport({
    required this.endpoint,
    required this.bucket,
    required this.accessKeyId,
    required this.accessKeySecret,
    this.objectKey = 'todolist/snapshot.json',
    http.Client? client,
  }) : _client = client ?? http.Client();

  /// 例如 oss-cn-hangzhou.aliyuncs.com（阿里云）或 cos.ap-guangzhou.myqcloud.com（腾讯云）
  final String endpoint;
  final String bucket;
  final String accessKeyId;
  final String accessKeySecret;
  final String objectKey;

  final http.Client _client;

  static const _timeout = Duration(seconds: 30);

  Uri get _objectUri => Uri.parse('https://$bucket.$endpoint/$objectKey');

  /// OSS V1 签名：
  /// `Authorization: OSS <AccessKeyId>:<base64(hmac-sha1(secret, stringToSign))>`
  /// stringToSign = `VERB + "\n" + Content-MD5 + "\n" + Content-Type + "\n"
  ///                + Date + "\n" + CanonicalizedOSSHeaders + CanonicalizedResource`
  String _authorization({required String verb, required String contentType}) {
    final date = httpDate(DateTime.now().toUtc());
    final stringToSign =
        '$verb\n\n$contentType\n$date\n/$bucket/$objectKey';
    final digest = crypto.Hmac(crypto.sha1, utf8.encode(accessKeySecret))
        .convert(utf8.encode(stringToSign));
    return 'OSS $accessKeyId:${base64Encode(digest.bytes)}';
  }

  Map<String, String> _headers({required String verb, String? contentType}) {
    final date = httpDate(DateTime.now().toUtc());
    return {
      'Date': date,
      'Authorization':
          _authorization(verb: verb, contentType: contentType ?? ''),
      'Content-Type': ?contentType,
    };
  }

  @override
  String get displayName => '对象存储';

  @override
  Future<void> testConnection() async {
    // 探测：GET 不存在的对象返回 404 也算连通且鉴权通过。
    final response = await _client
        .get(_objectUri, headers: _headers(verb: 'GET'))
        .timeout(_timeout);
    if (response.statusCode == 404) return;
    if (response.statusCode == 401 || response.statusCode == 403) {
      throw Exception('对象存储鉴权失败，请检查 AccessKey 与权限（${response.statusCode}）');
    }
    if (response.statusCode >= 400) {
      throw Exception('对象存储访问异常（${response.statusCode}），请检查 Endpoint/Bucket');
    }
  }

  @override
  Future<String?> fetchSnapshot() async {
    final response = await _client
        .get(_objectUri, headers: _headers(verb: 'GET'))
        .timeout(_timeout);
    if (response.statusCode == 404) return null;
    if (response.statusCode == 200) return response.body;
    throw Exception('对象存储读取失败（${response.statusCode}）');
  }

  @override
  Future<void> uploadSnapshot(String body) async {
    final response = await _client
        .put(
          _objectUri,
          headers: _headers(verb: 'PUT', contentType: 'application/json'),
          body: body,
        )
        .timeout(_timeout);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw Exception('对象存储上传失败（${response.statusCode}）');
    }
  }
}

/// RFC 1123 格式（GMT），如：Tue, 26 Aug 2026 12:00:00 GMT
String httpDate(DateTime utc) {
  const weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  const months = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
  ];
  final h = utc.hour.toString().padLeft(2, '0');
  final m = utc.minute.toString().padLeft(2, '0');
  final s = utc.second.toString().padLeft(2, '0');
  final d = utc.day.toString().padLeft(2, '0');
  return '${weekdays[utc.weekday - 1]}, $d ${months[utc.month - 1]} ${utc.year} $h:$m:$s GMT';
}
