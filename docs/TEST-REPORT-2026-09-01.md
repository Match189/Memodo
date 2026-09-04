# Memodo 三端验收测试报告（2026-09-01）

测试对象：Windows WPF 客户端 / Android 客户端 / memodo-server 自建同步服务器。
测试方式：代码全量走读捉虫 + 引擎级可执行测试（服务端 API 实测、跨语言加密互操作、WebDAV 模拟器场景）+ 产物校验。

---

## 1. 结论（TL;DR）

**捉虫 5 个（1 个致命、2 个严重、2 个中低），全部修复并重新构建三端。修复后全部测试通过：服务端 API 24/24，加密互操作双向 PASS，WebDAV 快照场景 8/8，Windows 自测 6/6，Android 产物校验全过。**

- 最严重的问题：**服务器通道 + E2EE 的组合此前在两端都完全不可用**（服务端拒收密文字段 + 双端 `IsEncrypted` 判定逻辑失效），以及 **Windows 端一开口令加密就必崩**（`AesGcm` 参数错误）。此前能正常工作的只有 WebDAV 明文同步。
- 修复后三端最新版本号见 §5，均已重新构建/发布。

## 2. 发现并修复的 Bug

| # | 端 | 问题 | 影响 | 修复 |
|---|---|---|---|---|
| B1 | 双端 | `IsEncrypted()` 对 base64 **文本**做 `startsWith("MEMODO1")`，但密文 base64 后开头是 `TUVNT0RP`——判定永远 false | 服务器通道行级解密路径失效：Windows 拉到密文行后生成 id="" 的垃圾行且不解密；Android 拉到密文行全部静默跳过（拉不到任何数据） | 改为解码 base64 后比对魔数字节（Win `SyncCrypto.cs` / And `SyncCrypto.kt`） |
| B2 | 服务端 | `SyncItemIn/Out.data` 声明为 `dict`，E2EE 行的 data 是密文**字符串** → Pydantic 422 整包拒收 | 服务器通道 + E2EE 推送全挂 | `memodo-server/app/schemas.py` 改为 `dict \| str` |
| B3 | Windows | `SyncCrypto.Encrypt` 把整段缓冲区（明文+tag）当密文 span 传入 `AesGcm.Encrypt`，长度校验必抛异常 | **设置口令后 Windows 端加密同步必然失败**（此前从未被运行时触发过） | 密文与 tag 分切片传入（`SyncCrypto.cs`） |
| B4 | Windows | `ApplyMemo` 不读 `completed` 字段 | Android 端已完成的备忘经服务器通道同步到 Windows 后完成态丢失 | 补读 `completed`（`SyncEngine.cs`） |
| B5 | 双端 | 服务器通道**拉取无本地 LWW 防护**（Windows 无条件覆盖本地；Android 缺平局决胜） | 设备时钟偏斜时，服务端旧行会覆盖本地更新的未推送编辑（数据丢失）；平局场景两端语义不一致 | Windows `ApplyTask/ApplyMemo` 补本地 LWW 检查（新者胜+平局 device_id 决胜）；Android 改用与 WebDAV 相同的 `prefer()` |

另修复（上一轮收尾）：WebDAV 通道 E2EE 中止时全局状态卡"同步中"（补 `markFail`）；英文语言包缺 5 条 E2EE 文案（补 `values-en`）。

**测试中确认非 bug 的行为**：服务端在 cursor=0 全量拉取时清理 90 天前的墓碑（设计行为，测试脚本曾误判为分页丢行）。

## 3. 测试执行结果

### 3.1 服务端 API 全链路（对运行中 Docker 实例实测，Node 脚本 `.qa/test-server.mjs`）——24/24 PASS

- 健康检查 /health ✓；注册 201 / 重复 409 ✓；登录 / 错密码 401 ✓
- refresh 轮换 + 旧 refresh 重放 401 ✓
- push 明文 4 行 accepted=4 ✓；LWW：旧时间戳 rejected、新时间戳 accepted、平局按 device_id 字典序决胜 ✓
- pull 全量 + cursor 增量 + cursor=最新为空 ✓；limit=2 分页拉全 ✓
- **E2EE 行（data=密文字符串）：push accepted（B2 修复验证）、回读密文原样、口令可解、云端无明文泄露** ✓
- 墓碑存储/回读 ✓；双用户数据隔离（含同 entity_id 互不干扰）✓；未授权 push/pull 401/403 ✓

### 3.2 跨端加密互操作（C#↔Node 双向，同口令同格式）——PASS

- C#（产品 `SyncCrypto.cs` 原码编译进 harness）加密 → Node（参照实现）解密：内容一致 ✓
- Node 加密 → C# 解密 ✓；错口令 → 解密失败（返回 NULL）✓
- C# selftest 6/6：roundtrip / 错口令 null / isEncrypted(密文)=true / isEncrypted(明文)=false / 空口令明文回退 ✓
- Android `SyncCrypto.kt` 与上述实现逐行同构（javax.crypto PBKDF2WithHmacSHA256 + AES/GCM/NoPadding + 同 MAGIC/长度/迭代参数），格式一致性由同一 wire 规范保证

### 3.3 WebDAV 快照全链路（本地模拟器 `.qa/webdav-mock.mjs`，按真实客户端 HTTP 序 MKCOL→GET→PUT）——8/8 PASS

Windows 上传整包密文 → 云端只存密文（无明文泄露）→ Android 同口令解密合并 → 回传 → Windows 二次拉取拿到 LWW 胜出版本 → 错口令中止保护 → 墓碑传播 → 跨端设置（自动同步间隔）LWW。

### 3.4 产物校验

- **Windows**：`dotnet build` 0 错误；publish/ 与 publish2/ 双目录单文件自包含发布，md5 一致 `2e70848a11b2c7d2ddc48c1c117f9a92`（165,646,222 B，09-02 00:06）
- **Android**：debug + release APK 重建（09-02 00:53 序列，晚于全部修复提交）；release 签名验证 CN=Memodo（SHA-256 c81b80f7…）；权限（INTERNET/SCHEDULE_EXACT_ALARM/RECEIVE_BOOT_COMPLETED）、launchable-activity、widget 组件齐全；`MEMODO1` 常量在 dex 中；中英双语 E2EE 文案资源齐全
- **服务端**：Docker 重建部署（Postgres 数据卷保留，`/health` ok）

### 3.5 静态走读覆盖（协议一致性确认，无需运行时验证）

快照 v3 双端字段逐项一致；task/memo JSON 字段一致（`completed`/`show_on_board`/`due_date` 等含 null 语义）；服务器通道 entity 值 `tasks/memos` 两端一致；E2EE wire 格式同构（MEMODO1+salt16+nonce12+GCM tag16，PBKDF2-HMAC-SHA256 210k，32B key）；Windows 增量推送 `LastPushAt` 的 `now` 先于查询捕获，无漏推窗口。

## 4. 遗留风险与已知限制（不阻塞，建议排期）

1. **WebDAV 混合口令场景**：一端设口令、另一端不设时，无口令端会把云端密文当"不可读"并以明文覆盖回传（Windows 走 `remoteUnreadable` 分支、Android 走空快照分支）。建议双端加护栏：检测云端是本格式密文而本地无口令 → 中止并提示（对应 B1 修复后的 `IsEncrypted` 现在可正确识别）。
2. **服务器通道口令错误的静默性**：Windows 拉取全解不开时报 `sync_e2ee_fail`（本次已加），但 Android 仍会显示"pushed N, pulled 0"——建议同样检测"全部行解不开"时明确报口令错误。
3. **Android 服务器通道全量推送**：每次同步推送全部行（含未变更），行数大时流量/电量浪费；且 E2EE 时每行一次 PBKDF2（210k 迭代）在手机上明显耗 CPU。建议比照 Windows 增加 `LastPushAt` 增量推送 + 会话内缓存派生密钥。
4. **服务端 server_seq 全局序列**：序列跨用户共享（`sync_seq` 全局 nextval），仅影响数值跨度不影响正确性；服务端无速率限制，公网部署建议加反代限流。
5. **Android 凭据明文存储**：WebDAV/服务器密码存于 SharedPreferences 明文（Windows 端是 DPAPI）。建议 Android 端引入 Keystore 加密存储（与 E2EE 口令同方案）。
6. **todo-server 双实现漂移**：`todo-server/`（SPD 旧版，`/api/v1`+camelCase+`todo/memo`）与客户端协议不兼容且无客户端使用；`memodo-server/` 为实际部署版但无 pytest。建议归档 todo-server 或把其测试移植到 memodo-server。
7. **SQLitePCLRaw.lib.e_sqlite3 2.1.6 高危漏洞告警**（NU1903，Windows 构建输出）：建议升级 Microsoft.Data.Sqlite 依赖链。
8. **未覆盖**：Windows WPF UI 真机点击回归、Android 真机全功能回归（无设备接入）、坚果云实网 WebDAV、大数据量压测（>500 行仅单页逻辑验证）。

## 5. 三端最新版本基线（本次交付）

| 端 | 位置 | 版本证据 |
|---|---|---|
| Windows | `memodo-windows/publish/` + `publish2/` | md5 `2e70848a…`（09-02 00:06，含 B1/B3/B4/B5 修复 + E2EE 全部功能） |
| Android | `app/build/outputs/apk/{debug,release}/` | 09-02 00:53 构建，含 isEncrypted/LWW/状态/英文资源修复，release 已签名 |
| 服务端 | Docker `memodo-server-web-1`（localhost:8000） | 09-02 00:35 重建，含 B2 修复（data: dict\|str），数据卷保留 |

## 6. 测试资产（可复用回归）

- `.qa/test-server.mjs` — 服务端 API 全链路 24 用例（含 MEMODO1 Node 参照实现）
- `.qa/CryptoHarness/` — C# SyncCrypto harness（引用产品源码，selftest + 跨语言互操作）
- `.qa/webdav-mock.mjs` — mini WebDAV 模拟器（MKCOL/GET/PUT/Basic Auth）
- 回归方式：先起 mock 与服务端，再依次运行上述脚本即可。
