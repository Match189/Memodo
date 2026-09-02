using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memodo.Windows.Services;

/// <summary>
/// 同步客户端（任务书 §5-§9）：对接 memodo-server 的共享协议。
/// LWW 由服务端裁决；本类负责注册/登录、401 自动 refresh 重试、push/pull。
/// 未接服务器时不会抛异常，调用方据此提示用户。
/// </summary>
public sealed class SyncService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public string ServerUrl { get; set; } = "";
    public string? AccessToken { get; private set; }

    // refresh 凭据（由登录/注册保存，401 时自动续期）
    private string _refreshToken = "";
    private string _email = "";
    private string _password = "";

    public void SetToken(string? token)
    {
        AccessToken = token;
        _http.DefaultRequestHeaders.Authorization =
            string.IsNullOrEmpty(token) ? null : new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public void SetCredentials(string email, string password, string refreshToken)
    {
        _email = email; _password = password; _refreshToken = refreshToken;
    }

    public async Task<(bool ok, string? error)> RegisterAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{ServerUrl.TrimEnd('/')}/auth/register",
                new { email, password });
            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
                return (false, LocalizationService.T("err_register_conflict"));
            if (!resp.IsSuccessStatusCode)
                return (false, string.Format(LocalizationService.T("err_server_login"), resp.StatusCode));
            return (true, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string? error)> LoginAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{ServerUrl.TrimEnd('/')}/auth/login",
                new { email, password });
            if (!resp.IsSuccessStatusCode)
                return (false, string.Format(LocalizationService.T("err_server_login"), resp.StatusCode));
            var body = await resp.Content.ReadFromJsonAsync<TokenDto>();
            if (body is null || string.IsNullOrEmpty(body.access_token))
                return (false, LocalizationService.T("err_server_login_parse"));
            SetToken(body.access_token);
            SetCredentials(email, password, body.refresh_token);
            // 持久化 refresh token（DPAPI），下次启动免登录
            SettingsStore.Current.RefreshTokenProtected = SecretProtector.Protect(body.refresh_token);
            SettingsStore.Save();
            return (true, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>用 refresh token 换新 access token（轮换 refresh token）。</summary>
    public async Task<bool> RefreshAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken)) return false;
        try
        {
            var resp = await _http.PostAsJsonAsync($"{ServerUrl.TrimEnd('/')}/auth/refresh",
                new { refresh_token = _refreshToken });
            if (!resp.IsSuccessStatusCode) return false;
            var body = await resp.Content.ReadFromJsonAsync<TokenDto>();
            if (body is null || string.IsNullOrEmpty(body.access_token)) return false;
            SetToken(body.access_token);
            _refreshToken = body.refresh_token;
            SettingsStore.Current.RefreshTokenProtected = SecretProtector.Protect(_refreshToken);
            SettingsStore.Save();
            return true;
        }
        catch { return false; }
    }

    /// <summary>启动恢复：用 DPAPI 里的 refresh token 免登录续期。</summary>
    public async Task RestoreSessionAsync()
    {
        var saved = SecretProtector.Unprotect(SettingsStore.Current.RefreshTokenProtected);
        if (string.IsNullOrEmpty(saved)) return;
        _refreshToken = saved;
        if (await RefreshAsync()) return;
        // refresh 失败（过期/吊销）：若记住了邮箱密码则重登一次
        if (!string.IsNullOrEmpty(_email) && !string.IsNullOrEmpty(_password))
            await LoginAsync(_email, _password);
    }

    /// <summary>带 401 自动 refresh 重试的 GET。</summary>
    public async Task<(bool ok, string? error, PullResult? data)> PullAsync(long cursor, int limit = 500)
    {
        var (ok, err, data) = await PullCoreAsync(cursor, limit);
        if (!ok && err == "401" && await RefreshAsync())
            return await PullCoreAsync(cursor, limit);
        return (ok, err, data);
    }

    private async Task<(bool ok, string? error, PullResult? data)> PullCoreAsync(long cursor, int limit)
    {
        try
        {
            var resp = await _http.GetAsync($"{ServerUrl.TrimEnd('/')}/sync/pull?cursor={cursor}&limit={limit}");
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) return (false, "401", null);
            if (!resp.IsSuccessStatusCode) return (false, string.Format(LocalizationService.T("err_server_pull"), resp.StatusCode), null);
            var data = await resp.Content.ReadFromJsonAsync<PullResult>();
            return (true, null, data);
        }
        catch (Exception ex) { return (false, ex.Message, null); }
    }

    /// <summary>带 401 自动 refresh 重试的 POST。</summary>
    public async Task<(bool ok, string? error)> PushAsync(List<SyncItemDto> items)
    {
        if (items.Count == 0) return (true, null);
        var (ok, err) = await PushCoreAsync(items);
        if (!ok && err == "401" && await RefreshAsync())
            return await PushCoreAsync(items);
        return (ok, err);
    }

    private async Task<(bool ok, string? error)> PushCoreAsync(List<SyncItemDto> items)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"{ServerUrl.TrimEnd('/')}/sync/push", new { items });
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized) return (false, "401");
            if (!resp.IsSuccessStatusCode) return (false, string.Format(LocalizationService.T("err_server_push"), resp.StatusCode));
            return (true, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
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
