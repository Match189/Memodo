using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Memodo.Windows.Services;

/// <summary>
/// 极简 WebDAV 客户端（蓝图 §43）：GET / PUT / MKCOL，Basic 认证。
/// 适配坚果云（dav.jianguoyun.com）、Nextcloud、NAS 等。实测坚果云：
/// MKCOL=201（已存在一般 405）、PUT=201、GET 缺失文件=404。
/// </summary>
public sealed class WebDavClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string _base;

    public WebDavClient(string baseUrl, string user, string password)
    {
        _base = baseUrl.TrimEnd('/') + "/";
        if (!string.IsNullOrEmpty(user))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
        }
    }

    /// <summary>读取文件；404 返回 null（调用方视为远端无快照）。</summary>
    public async Task<string?> GetFileAsync(string relativePath)
    {
        using var resp = await _http.GetAsync(_base + relativePath);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    public async Task PutFileAsync(string relativePath, string content)
    {
        using var resp = await _http.PutAsync(_base + relativePath,
            new StringContent(content, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>建目录；201/200/405(已存在)/301 都算可用。</summary>
    public async Task<bool> EnsureDirAsync(string relativeDir)
    {
        try
        {
            using var req = new HttpRequestMessage(new HttpMethod("MKCOL"), _base + relativeDir);
            using var resp = await _http.SendAsync(req);
            return (int)resp.StatusCode is 200 or 201 or 301 or 405;
        }
        catch { return false; }
    }
}
