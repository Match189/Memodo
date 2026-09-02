using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;

namespace Memodo.Windows.Services;

/// <summary>
/// 同步执行器（任务书 §5-§9）：拉取服务端增量并应用到本地（LWW 由服务端裁决），
/// 再把本地变更（含软删除墓碑）推送上去。先实现待办/备忘，板与布局为后续里程碑。
/// 线上 JSON 用 snake_case 列名（与双端 SQLite 列一致），push/pull 两侧同构。
/// </summary>
public sealed class SyncEngine
{
    private static readonly JsonSerializerOptions Wire = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly SyncService _sync;
    private readonly TaskRepository _tasks;
    private readonly MemoRepository _memos;
    private static readonly SemaphoreSlim _syncGate = new(1, 1); // 防定时器+手动同步并发跑双合并

    public SyncEngine(SyncService sync, TaskRepository tasks, MemoRepository memos)
    {
        _sync = sync; _tasks = tasks; _memos = memos;
    }

    public async Task<(int pulled, int pushed, string? error)> RunAsync(string password)
    {
        if (string.IsNullOrEmpty(_sync.AccessToken))
        {
            // 先试 refresh token 免登录恢复；失败再用账号密码登录
            await _sync.RestoreSessionAsync();
            if (string.IsNullOrEmpty(_sync.AccessToken))
            {
                var (ok, err) = await _sync.LoginAsync(SettingsStore.Current.AccountEmail, password);
                if (!ok) return (0, 0, err ?? LocalizationService.T("err_not_logged_in"));
            }
        }
        // W3 口令指纹：口令变化（含清除）→ 游标归零全量重拉，
        // 否则曾因口令错误跳过的行（游标已越过）永远不会被重新拉到
        var s0 = SettingsStore.Current;
        var fp = Fingerprint(s0.SyncPassphrase);
        if (!string.Equals(fp, s0.E2eePassFingerprint, StringComparison.Ordinal))
        {
            s0.E2eePassFingerprint = fp;
            s0.LastPullCursor = 0;
            s0.LastPushAt = 0;
            SettingsStore.Save();
        }
        // 先 pull 再 push：若先 push，本设备全量上行会把 server_seq 推得很高，
        // 其间其他设备写入的行（seq 较小）会被随后的 cursor 增量拉取跳过 → 漏数据
        int pulled = await PullAsync();
        int pushed = await PushAsync();
        return (pulled, pushed, null);
    }

    private static string Fingerprint(string passphrase)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(passphrase));
        return Convert.ToHexString(bytes);
    }

    // ================= WebDAV（蓝图 §43，快照 + LWW） =================

    private const string WebDavDir = "memodo";
    private const string WebDavFile = "memodo/memodo-sync.json";

    private sealed class Snapshot
    {
        public int format { get; set; } = 3;
        public string device_id { get; set; } = "";
        public long exported_at { get; set; }
        public List<TaskItem> tasks { get; set; } = new();
        public List<MemoItem> memos { get; set; } = new();
        public SnapshotSettings? settings { get; set; }
    }

    /// <summary>跨端同步的机器设置（自动同步间隔等）。</summary>
    private sealed class SnapshotSettings
    {
        public int auto_sync_minutes { get; set; } = 3;
        public long updated_at { get; set; }
    }

    /// <summary>
    /// WebDAV 快照同步：下载远端 → LWW 合并（含墓碑）→ 应用本地 → 上传合并结果。
    /// 平局（updatedAt 相等）按 deviceId 字典序决胜（§19/§47）。
    /// </summary>
    public async Task<(int tasks, int memos, string? error)> RunWebDavAsync()
    {
        // 并发守卫：定时器/托盘/设置页可能同时触发，双合并会互相覆盖快照
        if (!await _syncGate.WaitAsync(0)) return (0, 0, null);
        try
        {
            return await RunWebDavCoreAsync();
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task<(int tasks, int memos, string? error)> RunWebDavCoreAsync()
    {
        var s = SettingsStore.Current;
        var pass = s.SyncPassphrase;
        var client = new WebDavClient(s.WebDavUrl, s.WebDavUser, SecretProtector.Unprotect(s.WebDavPassProtected));
        try
        {
            if (!await client.EnsureDirAsync(WebDavDir))
                return (0, 0, LocalizationService.T("err_webdav_mkdir"));

            var remoteJson = await client.GetFileAsync(WebDavFile);
            Snapshot remote = new();
            bool remoteUnreadable = false;
            if (remoteJson is not null)
            {
                try
                {
                    // W1 混合口令护栏：云端是本格式密文而本机未设口令 → 中止（绝不以明文覆盖云端密文）
                    if (SyncCrypto.IsEncrypted(remoteJson) && string.IsNullOrEmpty(pass))
                        return (0, 0, LocalizationService.T("sync_e2ee_no_pass"));
                    // E2EE：云端是密文（或旧明文），先解密再反序列化
                    var decrypted = SyncCrypto.TryDecrypt(remoteJson, pass);
                    if (decrypted is null)
                    {
                        // 口令不一致或数据损坏：不合并、不回传，保护本地不被旧数据覆盖
                        return (0, 0, LocalizationService.T("sync_e2ee_fail"));
                    }
                    remote = JsonSerializer.Deserialize<Snapshot>(decrypted, Wire) ?? new Snapshot();
                    // format 校验：非 v3 视作不可读，防止旧格式/损坏数据被合并后回传覆盖
                    if (remote.format != 3) remoteUnreadable = true;
                }
                catch { remoteUnreadable = true; } // 远端损坏：不参与合并，也不回传覆盖
            }

            var myDevice = s.EnsureDeviceId();
            var local = new Snapshot
            {
                device_id = myDevice,
                exported_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                tasks = _tasks.ListAllForSync(),
                memos = _memos.ListAllForSync(),
            };

var mergedTasks = remoteUnreadable ? local.tasks : MergeById(local.tasks, remote.tasks,
                t => t.Id, t => t.UpdatedAt, remote.device_id, myDevice);
            var mergedMemos = remoteUnreadable ? local.memos : MergeById(local.memos, remote.memos,
                m => m.Id, m => m.UpdatedAt, remote.device_id, myDevice);

            // 应用到本地（不推进 updated_at，保 LWW 语义）
            foreach (var t in mergedTasks) _tasks.UpsertFromSync(t);
            foreach (var m in mergedMemos) _memos.UpsertFromSync(m);

            // 跨端设置合并：自动同步间隔 LWW（updated_at 新者胜）
            var localSettingsUpdatedAt = s.AutoSyncIntervalUpdatedAt;
            var localSettings = new SnapshotSettings
            {
                auto_sync_minutes = s.AutoSyncIntervalMinutes,
                updated_at = localSettingsUpdatedAt,
            };
            if (remote.settings is { } rs && rs.updated_at > localSettingsUpdatedAt
                && rs.auto_sync_minutes >= 1 && rs.auto_sync_minutes <= 120)
            {
                s.AutoSyncIntervalMinutes = rs.auto_sync_minutes;
                s.AutoSyncIntervalUpdatedAt = rs.updated_at;
                App.RestartAutoSyncTimer();
            }

            // 上传合并结果（含墓碑，§49 删除传播）；启用了口令则整包 AES-256-GCM 加密，云端只存密文
            var merged = new Snapshot
            {
                device_id = myDevice,
                exported_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                tasks = mergedTasks,
                memos = mergedMemos,
                settings = new SnapshotSettings
                {
                    auto_sync_minutes = s.AutoSyncIntervalMinutes,
                    updated_at = Math.Max(localSettingsUpdatedAt, SettingsStore.Current.AutoSyncIntervalUpdatedAt),
                },
            };
            var payload = JsonSerializer.Serialize(merged, Wire);
            await client.PutFileAsync(WebDavFile, SyncCrypto.Encrypt(payload, pass));

            s.LastSyncAt = merged.exported_at;
            SettingsStore.Save();
            if (remoteUnreadable)
                return (mergedTasks.Count, mergedMemos.Count, LocalizationService.T("err_remote_corrupt"));
            return (mergedTasks.Count, mergedMemos.Count, null);
        }
        catch (Exception ex)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "app.memodo");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "sync.log"),
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {ex}{Environment.NewLine}");
            }
            catch { /* 日志失败不影响主流程 */ }
            return (0, 0, ex.Message);
        }
    }

    private static List<T> MergeById<T>(
        List<T> local, List<T> remote,
        Func<T, string> id, Func<T, long> ts,
        string remoteDevice, string localDevice)
    {
        var map = local.ToDictionary(id, x => x);
        foreach (var r in remote)
        {
            if (!map.TryGetValue(id(r), out var l))
            {
                map[id(r)] = r;
                continue;
            }
            if (ts(r) > ts(l)) map[id(r)] = r;                                   // 新时间赢
            else if (ts(r) == ts(l) && string.CompareOrdinal(remoteDevice, localDevice) > 0)
                map[id(r)] = r;                                                  // 平局字典序决胜
        }
        return map.Values.ToList();
    }

    private async Task<int> PullAsync()
    {
        int total = 0;
        int undecryptable = 0;
        long cursor = SettingsStore.Current.LastPullCursor;
        while (true)
        {
            var (ok, _, res) = await _sync.PullAsync(cursor);
            if (!ok || res is null) break;
            foreach (var it in res.items)
            {
                try
                {
                    var data = OpenData(it.data); // E2EE：解密行载荷（明文行原样通过）
                    if (it.entity == "tasks") ApplyTask(data, it.device_id);
                    else if (it.entity == "memos") ApplyMemo(data, it.device_id);
                    total++;
                }
                catch
                {
                    // 单条坏数据/口令不一致跳过，不中断整体；全解不开则按口令错误上报
                    if (it.data.ValueKind == JsonValueKind.String
                        && SyncCrypto.IsEncrypted(it.data.GetString() ?? ""))
                        undecryptable++;
                }
            }
            SettingsStore.Current.LastPullCursor = res.cursor;
            SettingsStore.Save();
            if (res.items.Count == 0 || res.cursor == cursor) break;
            cursor = res.cursor;
        }
        if (undecryptable > 0 && total == 0)
            throw new CryptographicException(LocalizationService.T("sync_e2ee_fail"));
        return total;
    }

    private async Task<int> PushAsync()
    {
        var items = new List<SyncItemDto>();
        // 增量推送：只推自上次同步后有变更的行（首推 LastPushAt=0 自然为全量）。
        // 全量上行会让 server_seq 每轮暴涨，cursor 增量拉取会跳过其他设备刚写入的行。
        var since = SettingsStore.Current.LastPushAt;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var t in _tasks.ListAllForSync())          // 含墓碑，删除也能传播
            if (t.UpdatedAt > since) items.Add(ToDto("tasks", t.Id, SealData(t), t.UpdatedAt));
        foreach (var m in _memos.ListAllForSync())
            if (m.UpdatedAt > since) items.Add(ToDto("memos", m.Id, SealData(m), m.UpdatedAt));
        var (ok, _) = await _sync.PushAsync(items);
        if (ok)
        {
            SettingsStore.Current.LastPushAt = now;
            SettingsStore.Save();
        }
        return ok ? items.Count : 0;
    }

    // ---- 服务器通道行级 E2EE：entity_id/updated_at 保持明文（服务器仍可增量/LWW），
    //      data 内容加密；未设口令时原样明文（兼容未加密设备） ----
    private JsonElement SealData(object o)
    {
        var el = JsonSerializer.SerializeToElement(o, Wire);
        var pass = SettingsStore.Current.SyncPassphrase;
        if (string.IsNullOrEmpty(pass)) return el;
        return JsonSerializer.SerializeToElement(SyncCrypto.Encrypt(el.GetRawText(), pass));
    }

    private JsonElement OpenData(JsonElement d)
    {
        if (d.ValueKind != JsonValueKind.String) return d; // 明文行（未加密设备写入）
        var s = d.GetString();
        if (string.IsNullOrEmpty(s)) return d;
        if (!SyncCrypto.IsEncrypted(s)) return d; // 无魔数 → 明文行
        // W2 护栏：密文行必须真正解开。本机未设口令或口令不对都按"解不开"跳过，
        // 绝不把密文字符串当明文行写入（会生成 id="" 的垃圾行）
        var pass = SettingsStore.Current.SyncPassphrase;
        if (string.IsNullOrEmpty(pass)) throw new CryptographicException("passphrase not set");
        var opened = SyncCrypto.TryDecrypt(s, pass)
                     ?? throw new CryptographicException("passphrase mismatch");
        return JsonSerializer.Deserialize<JsonElement>(opened, Wire);
    }

    private static SyncItemDto ToDto(string entity, string id, JsonElement data, long updatedAt) => new()
    {
        entity = entity,
        entity_id = id,
        data = data,
        updated_at = updatedAt,
        device_id = Environment.MachineName,
    };

    private static JsonElement ToJson(object o) => JsonSerializer.SerializeToElement(o, Wire);

    private void ApplyTask(JsonElement d, string remoteDevice)
    {
        var t = new TaskItem
        {
            Id = Str(d, "id"),
            Title = Str(d, "title"),
            Description = Str(d, "description"),
            Completed = Int(d, "completed") != 0,
            Priority = Int(d, "priority"),
            CreatedAt = Long(d, "created_at"),
            UpdatedAt = Long(d, "updated_at"),
            DeletedAt = LongOrNull(d, "deleted_at"),
            ArchivedAt = LongOrNull(d, "archived_at"),
        };
        var due = LongOrNull(d, "due_date");
        if (due.HasValue) t.DueDate = due;
        // 本地 LWW 防护：拉取不得覆盖本地更新/相持胜出的未推送编辑（设备时钟可能偏斜）
        var local = _tasks.GetById(t.Id);
        if (local is null || t.UpdatedAt > local.UpdatedAt
            || (t.UpdatedAt == local.UpdatedAt
                && string.CompareOrdinal(remoteDevice, Environment.MachineName) > 0))
        {
            _tasks.UpsertFromSync(t);
        }
    }

    private void ApplyMemo(JsonElement d, string remoteDevice)
    {
        var m = new MemoItem
        {
            Id = Str(d, "id"),
            Title = Str(d, "title"),
            Content = Str(d, "content"),
            Completed = Int(d, "completed") != 0, // 之前缺失：Android 端备忘完成态同步到 Windows 会丢
            CreatedAt = Long(d, "created_at"),
            UpdatedAt = Long(d, "updated_at"),
            DeletedAt = LongOrNull(d, "deleted_at"),
            ArchivedAt = LongOrNull(d, "archived_at"),
            ShowOnBoard = !d.TryGetProperty("show_on_board", out var sob) || sob.ValueKind != JsonValueKind.False,
        };
        var local = _memos.GetById(m.Id);
        if (local is null || m.UpdatedAt > local.UpdatedAt
            || (m.UpdatedAt == local.UpdatedAt
                && string.CompareOrdinal(remoteDevice, Environment.MachineName) > 0))
        {
            _memos.UpsertFromSync(m);
        }
    }

    /// <summary>兼容读取：Android 端 completed 是 JSON boolean，老数据可能是 0/1。</summary>
    private static int Int(JsonElement d, string k) =>
        d.TryGetProperty(k, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.Number => v.GetInt32(),
                JsonValueKind.True => 1,
                JsonValueKind.False => 0,
                _ => 0,
            }
            : 0;

    private static string Str(JsonElement d, string k) =>
        d.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static long Long(JsonElement d, string k) =>
        d.TryGetProperty(k, out var v) ? v.GetInt64() : 0;

    private static long? LongOrNull(JsonElement d, string k) =>
        d.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetInt64() : null;
}
