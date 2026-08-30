using System.IO;
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

    public SyncEngine(SyncService sync, TaskRepository tasks, MemoRepository memos)
    {
        _sync = sync; _tasks = tasks; _memos = memos;
    }

    public async Task<(int pulled, int pushed, string? error)> RunAsync(string password)
    {
        if (string.IsNullOrEmpty(_sync.AccessToken))
        {
            var (ok, err) = await _sync.LoginAsync(SettingsStore.Current.AccountEmail, password);
            if (!ok) return (0, 0, err ?? "未登录");
        }
        int pushed = await PushAsync();
        int pulled = await PullAsync();
        return (pulled, pushed, null);
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
    }

    /// <summary>
    /// WebDAV 快照同步：下载远端 → LWW 合并（含墓碑）→ 应用本地 → 上传合并结果。
    /// 平局（updatedAt 相等）按 deviceId 字典序决胜（§19/§47）。
    /// </summary>
    public async Task<(int tasks, int memos, string? error)> RunWebDavAsync()
    {
        var s = SettingsStore.Current;
        var client = new WebDavClient(s.WebDavUrl, s.WebDavUser, SecretProtector.Unprotect(s.WebDavPassProtected));
        try
        {
            if (!await client.EnsureDirAsync(WebDavDir))
                return (0, 0, "无法创建 WebDAV 目录，请检查地址/账号权限");

            var remoteJson = await client.GetFileAsync(WebDavFile);
            Snapshot remote = new();
            if (remoteJson is not null)
            {
                try { remote = JsonSerializer.Deserialize<Snapshot>(remoteJson, Wire) ?? new Snapshot(); }
                catch { remote = new Snapshot(); } // 远端损坏视作空快照，本地数据会覆盖之
            }

            var myDevice = s.EnsureDeviceId();
            var local = new Snapshot
            {
                device_id = myDevice,
                exported_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                tasks = _tasks.ListAllForSync(),
                memos = _memos.ListAllForSync(),
            };

            var mergedTasks = MergeById(local.tasks, remote.tasks,
                t => t.Id, t => t.UpdatedAt, remote.device_id, myDevice);
            var mergedMemos = MergeById(local.memos, remote.memos,
                m => m.Id, m => m.UpdatedAt, remote.device_id, myDevice);

            // 应用到本地（不推进 updated_at，保 LWW 语义）
            foreach (var t in mergedTasks) _tasks.UpsertFromSync(t);
            foreach (var m in mergedMemos) _memos.UpsertFromSync(m);

            // 上传合并结果（含墓碑，§49 删除传播）
            var merged = new Snapshot
            {
                device_id = myDevice,
                exported_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                tasks = mergedTasks,
                memos = mergedMemos,
            };
            await client.PutFileAsync(WebDavFile, JsonSerializer.Serialize(merged, Wire));

            s.LastSyncAt = merged.exported_at;
            SettingsStore.Save();
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
        long cursor = SettingsStore.Current.LastPullCursor;
        while (true)
        {
            var (ok, _, res) = await _sync.PullAsync(cursor);
            if (!ok || res is null) break;
            foreach (var it in res.items)
            {
                try
                {
                    if (it.entity == "tasks") ApplyTask(it.data);
                    else if (it.entity == "memos") ApplyMemo(it.data);
                    total++;
                }
                catch { /* 单条坏数据跳过，不中断整体 */ }
            }
            SettingsStore.Current.LastPullCursor = res.cursor;
            SettingsStore.Save();
            if (res.items.Count == 0 || res.cursor == cursor) break;
            cursor = res.cursor;
        }
        return total;
    }

    private async Task<int> PushAsync()
    {
        var items = new List<SyncItemDto>();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var t in _tasks.ListAllForSync())          // 含墓碑，删除也能传播
            items.Add(ToDto("tasks", t.Id, ToJson(t), ts));
        foreach (var m in _memos.ListAllForSync())
            items.Add(ToDto("memos", m.Id, ToJson(m), ts));
        var (ok, _) = await _sync.PushAsync(items);
        return ok ? items.Count : 0;
    }

    private static SyncItemDto ToDto(string entity, string id, JsonElement data, long ts) => new()
    {
        entity = entity,
        entity_id = id,
        data = data,
        updated_at = ts,
        device_id = Environment.MachineName,
    };

    private static JsonElement ToJson(object o) => JsonSerializer.SerializeToElement(o, Wire);

    private void ApplyTask(JsonElement d)
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
        };
        var due = LongOrNull(d, "due_date");
        if (due.HasValue) t.DueDate = due;
        _tasks.UpsertFromSync(t);
    }

    private void ApplyMemo(JsonElement d)
    {
        var m = new MemoItem
        {
            Id = Str(d, "id"),
            Title = Str(d, "title"),
            Content = Str(d, "content"),
            CreatedAt = Long(d, "created_at"),
            UpdatedAt = Long(d, "updated_at"),
            DeletedAt = LongOrNull(d, "deleted_at"),
        };
        _memos.UpsertFromSync(m);
    }

    private static string Str(JsonElement d, string k) =>
        d.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static int Int(JsonElement d, string k) =>
        d.TryGetProperty(k, out var v) ? v.GetInt32() : 0;
    private static long Long(JsonElement d, string k) =>
        d.TryGetProperty(k, out var v) ? v.GetInt64() : 0;
    private static long? LongOrNull(JsonElement d, string k) =>
        d.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetInt64() : null;
}
