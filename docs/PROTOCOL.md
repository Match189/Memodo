# Memodo Sync Protocol / 同步协议

Language-neutral specification of the three contracts that keep Memodo clients
interoperable: the **snapshot format** (WebDAV channel), the **sync API** (server
channel), and the **E2EE envelope**. All timestamps are Unix epoch milliseconds.

三份跨端约定：WebDAV 快照格式、服务器同步 API、端到端加密信封。所有时间戳为毫秒。

---

## 1. Snapshot format (WebDAV channel) / 快照格式 v3

One JSON file per account, typically `memodo/memodo-sync.json` on the WebDAV root.

```json
{
  "format": 3,
  "device_id": "win-a1b2c3d4",
  "exported_at": 1700000000000,
  "tasks": [ { ...TaskObject... } ],
  "memos": [ { ...MemoObject... } ],
  "settings": { "auto_sync_minutes": 5, "updated_at": 1700000000000 }
}
```

**TaskObject** (all fields snake_case; nullable fields use JSON `null`):

| field | type | notes |
|---|---|---|
| `id` | string | UUID, global identity |
| `title` | string | |
| `description` | string | |
| `completed` | bool | older writers may send `0/1` — treat numbers as bools |
| `priority` | int | |
| `due_date` | int/null | |
| `created_at` / `updated_at` | int | ms |
| `deleted_at` | int/null | non-null = tombstone (soft delete) |
| `archived_at` | int/null | |

**MemoObject** adds: `content` (string), `completed` (bool), `show_on_board` (bool, default true).

**Merge rule (LWW)**: per `id`, the row with the larger `updated_at` wins; on a tie,
the row from the `device_id` with the greater lexicographic order wins. Tombstones
(`deleted_at != null`) participate and propagate deletions. `settings.updated_at`
resolves the auto-sync interval the same way.

---

## 2. Server sync API / 服务器同步 API

Base path `/` on a self-hosted [FastAPI](../memodo-server) instance. All bodies JSON.
`{entity}` is `tasks` or `memos`.

| method & path | purpose |
|---|---|
| `POST /auth/register` `{email, password}` | create account → 201 |
| `POST /auth/login` → `{access_token, refresh_token}` | JWT auth (access 15 min, refresh 7 days, refresh rotates) |
| `POST /auth/refresh` `{refresh_token}` | rotate tokens; old refresh token replay → 401 |
| `POST /sync/push` `{items: [SyncItem]}` | LWW-upsert rows; response `{accepted:[], rejected:[]}` |
| `GET /sync/pull?cursor=&limit=` | incremental pull by `server_seq`; response `{items:[SyncItemOut], cursor}` |
| `GET /health` | `{"status":"ok"}` |

**SyncItem** (push):

```json
{
  "entity": "tasks",
  "entity_id": "uuid",
  "data": { "id": "uuid", "title": "...", ... },
  "updated_at": 1700000000000,
  "deleted_at": null,
  "device_id": "win-a1b2c3d4"
}
```

**Server-side LWW**: accept if incoming `updated_at` is greater, or equal with a
lexicographically greater `device_id`; otherwise the row is listed in `rejected`.
Every accepted write advances a per-server monotonic `server_seq` (the pull cursor).
Rows are scoped by `user_id` — users never see each other's data.
`data` may also be a **ciphertext string** when E2EE is enabled (§3); the server
stores it opaquely and returns it verbatim.

---

## 3. E2EE envelope / 端到端加密信封（MEMODO1）

```
payload = base64( "MEMODO1" || salt[16] || nonce[12] || AES-256-GCM(plaintext || tag[16]) )
key     = PBKDF2-HMAC-SHA256(passphrase, salt, iterations=210000, dkLen=32)
```

- WebDAV channel: the **whole snapshot** is the plaintext (encrypted file replaces the JSON).
- Server channel: only each row's `data` field is sealed (`SyncItem.data` becomes a
  base64 string); `entity_id`, `updated_at`, `deleted_at`, `device_id` stay plaintext
  so incremental pull and LWW keep working.
- Empty/absent passphrase = plaintext mode (fully backward compatible).
- Wrong or missing passphrase on read → decryption fails → **sync aborts protectively**;
  clients never overwrite cloud ciphertext with plaintext.
- The passphrase never leaves the device. **Lost passphrase = cloud data unrecoverable.**
- Cross-client vectors/keys are random per payload; a fixed magic + salt makes the
  format self-describing without leaking the passphrase.

Reference implementations: `memodo-windows/Services/SyncCrypto.cs`,
`memodo-android/.../data/SyncCrypto.kt` (bit-compatible), plus a Node test vector
in `.qa/test-server.mjs` (not shipped in the public repo; re-create from this spec
if you need a third-party implementation — the format above is complete).
