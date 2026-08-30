using System;

namespace Memodo.Windows.Models;

// table = (see ModelAttr)
public class MemoItem
{
    // PK
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    /// <summary>完成/归档：完成的备忘从钉板移除（用户裁定，语义同待办）。</summary>
    public bool Completed { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long? DeletedAt { get; set; }
}
