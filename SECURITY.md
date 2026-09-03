# Security Policy（安全策略）

## Supported versions / 支持版本

Only the latest release on the `main` branch is supported with security fixes.
仅支持 `main` 分支最新版本的安全修复。

## Reporting a vulnerability / 报告漏洞

**Please do NOT report security vulnerabilities through public GitHub issues.**
请勿通过公开 GitHub issue 报告安全漏洞。

Use GitHub [Private vulnerability reporting](../../security/advisories/new) instead,
or contact the maintainer directly. Include steps to reproduce, affected
components (client / server / sync protocol), and impact assessment.
请使用 GitHub 私密漏洞报告或直接联系维护者，附复现步骤、受影响组件与影响评估。

## End-to-end encryption notes / 端到端加密说明

- Sync payloads (WebDAV snapshot / server row data) are encrypted on-device
  with **AES-256-GCM**; keys are derived with **PBKDF2-HMAC-SHA256**
  (210,000 iterations) from a user-chosen passphrase (format `MEMODO1`).
  同步载荷在设备端加密；密钥由用户口令经 PBKDF2 派生。
- **The passphrase is never stored or transmitted.** If the passphrase is
  lost, existing encrypted data on the cloud side **cannot be recovered** —
  by design. There is no backdoor, escrow, or reset mechanism.
  口令不落盘不上云；口令丢失后云端已有密文按设计**无法恢复**，不存在后门。
- A wrong or missing passphrase makes sync abort protectively; it never
  overwrites cloud ciphertext with plaintext.
  口令错误/未设置时同步中止，绝不以明文覆盖云端密文。
- Server credentials (JWT secrets, database passwords) are deployment
  concerns: change the default `jwt_secret` in your `.env` before exposing
  the server to any network.
  服务器凭据属部署事项：对外暴露前务必修改 `.env` 中的默认 `jwt_secret`。

## Scope / 范围

In scope: this repository's client apps, sync server, and the wire protocol
(snapshot v3 / MEMODO1). Out of scope: third-party WebDAV providers,
operating systems, and the hosting environment you deploy the server into.
