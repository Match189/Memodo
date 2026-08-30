using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memodo.Windows.Services;

/// <summary>
/// 同步客户端（任务书 §5-§9）：对接 memodo-server 的共享协议。
/// LWW 由服务端裁决；本类负责登录、push 全量变更、pull 游标增量。
/// 未接服务器时不会抛异常，调用方据此提示用户。
/// </summary>
public sealed class SyncService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public string ServerUrl { get; set; } = "";
    public string? AccessToken { get; private set; }

    public void SetToken(string? token)
    {
        AccessToken = token;
        _http.DefaultRequestHeaders.Authorization =
            string.IsNullOrEmpty(token) ? null : new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<(bool ok, string? error)> LoginAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{ServerUrl.TrimEnd('/')}/auth/login",
                new { email, password });
            if (!resp.IsSuccessStatusCode)
                return (false, $"登录失败 ({resp.StatusCode})");
            var body = await resp.Content.ReadFromJsonAsync<TokenDto>();
            if (body is null) return (false, "登录响应解析失败");
            SetToken(body.access_token);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool ok, string? error, PullResult? data)> PullAsync(long cursor, int limit = 500)
    {
        try
        {
            var resp = await _http.GetAsync($"{ServerUrl.TrimEnd('/')}/sync/pull?cursor={cursor}&limit={limit}");
            if (!resp.IsSuccessStatusCode) return (false, $"pull 失败 ({resp.StatusCode})", null);
            var data = await resp.Content.ReadFromJsonAsync<PullResult>();
            return (true, null, data);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool ok, string? error)> PushAsync(List<SyncItemDto> items)
    {
        if (items.Count == 0) return (true, null);
        try
        {
            var resp = await _http.PostAsJsonAsync($"{ServerUrl.TrimEnd('/')}/sync/push", new { items });
            if (!resp.IsSuccessStatusCode) return (false, $"push 失败 ({resp.StatusCode})");
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}

public class TokenDto
{
    public string access_token { get; set; } = "";
    public string refresh_token { get; set; } = "";
    public string token_type { get; set; } = "bearer";
}

public class PullResult
{
    public List<SyncItemDto> items { get; set; } = new();
    public long cursor { get; set; }
}

public class SyncItemDto
{
    public required string entity { get; set; }
    public required string entity_id { get; set; }
    public JsonElement data { get; set; }
    public long updated_at { get; set; }
    public long? deleted_at { get; set; }
    public string device_id { get; set; } = "";
}
