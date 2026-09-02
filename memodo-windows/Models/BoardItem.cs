using System;

namespace Memodo.Windows.Models;

/// <summary>
/// 板（任务书 §9）：容器。Card 钉在板里。
/// </summary>
// table = (see ModelAttr)
public class BoardItem
{
    // PK
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = "";
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long? DeletedAt { get; set; }
}

/// <summary>
/// 分区（任务书 §9）：板上可选分组。本版本 V1 不强制分区，字段预留。
/// </summary>
// table = (see ModelAttr)
public class SectionItem
{
    // PK
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string BoardId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long? DeletedAt { get; set; }
}
