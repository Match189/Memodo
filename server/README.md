# todolist_server（自建同步服务器参考实现）

给「待办备忘」App 的自建同步通道用的极简服务端：一个单文件 Dart 程序，
快照就是磁盘上的一个 JSON 文件（自动原子写入 + 保留上一版 .bak），
无数据库、无外部服务依赖。

## 协议

| 方法 | 路径 | 鉴权 | 说明 |
| --- | --- | --- | --- |
| GET | `/snapshot` | Bearer Token | 返回快照 JSON，没有则 404 |
| PUT | `/snapshot` | Bearer Token | 覆盖保存快照，204 成功 |
| GET | `/health` | 无 | 探活 |

App 端配置：服务器地址填 `http(s)://<host>:<port>`，访问令牌填 Token。

## 本地运行

```bash
cd server
dart pub get
dart run bin/server.dart --port 8080 --token 换成长随机串
```

## 编译成单个可执行文件（服务器上无需装 Dart）

```bash
dart compile exe bin/server.dart -o todolist_server
./todolist_server --port 8080 --token 换成长随机串 --data-dir ./data
```

## Docker 部署

```bash
docker build -t todolist-server .
docker run -d -p 8080:8080 \
  -e TODOLIST_TOKEN=换成长随机串 \
  -v "$PWD/data:/app/data" \
  todolist-server
```

## 安全建议

- Token 用长随机串（`openssl rand -hex 32`）。
- 公网部署务必加 HTTPS：前面挂一层 Caddy/Nginx 反代（Caddy 自动签证书最省事），
  App 端地址填 https 域名。
- 定期备份 data/ 目录（也可以直接把 data/ 放进任何网盘做双保险）。

> 注意：Android 9+ 默认禁止明文 HTTP。用 IP+端口直连时 App 端会失败，
> 建议公网走 HTTPS，或局域网内临时使用（我们后续可在 APK 里放开明文限制）。
