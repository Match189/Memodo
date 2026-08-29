using System;

namespace Memodo.Windows.Models;

/// <summary>
/// 待办条目（任务书 §10）。所有跨设备实体用稳定 UUID 作业务 ID。
/// 软删除：deletedAt 非空即墓碑（任务书 §36）。
/// </summary>
// table = (see ModelAttr)
public class TaskItem
{
    // PK
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public int Priority { get; set; }
    public long? DueDate { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long? DeletedAt { get; set; }
}
