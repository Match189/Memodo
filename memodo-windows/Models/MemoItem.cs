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
    /// <summary>是否显示在钉板（眼睛按钮控制；用户裁定 v2：备忘用可见性而非完成语义）。</summary>
    public bool ShowOnBoard { get; set; } = true;
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long? DeletedAt { get; set; }
}
