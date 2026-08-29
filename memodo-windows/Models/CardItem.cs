using System;

namespace Memodo.Windows.Models;

/// <summary>
/// 卡片（任务书 §12）：只引用实体，不复制内容（SPD 禁止第二数据源）。
/// </summary>
// table = (see ModelAttr)
public class CardItem
{
    // PK
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string BoardId { get; set; } = string.Empty;
    public string SectionId { get; set; } = string.Empty;
    public string RefType { get; set; } = string.Empty; // "todo" | "memo"
    public string RefUuid { get; set; } = string.Empty;
    public int Sort { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long? DeletedAt { get; set; }
}
