using System;

namespace Memodo.Windows.Models;

/// <summary>
/// 卡片布局（任务书 §13-15）。业务数据与平台布局数据分离：boardId/cardId
/// 由 Card 关联；本机布局存本机 kv（不进同步协议）。
/// 同一张卡片在 Windows / Android 下可以有不同的 layout。
/// </summary>
// table = (see ModelAttr)
public class CardLayoutItem
{
    // PK
    public int Id { get; set; }

    // IX
    public string CardId { get; set; } = string.Empty;

    /// <summary>"windows" | "android"</summary>
    public string Platform { get; set; } = "windows";

    // Windows 自由布局字段
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 190;
    public double Height { get; set; } = 150;
    public double Rotation { get; set; }  // 度，±1.5° 内（任务书 §19）
    public int Z { get; set; }

    // Android 适配字段（占位，未来启用）
    public int? Order { get; set; }
    public string? SizeClass { get; set; }  // "2x2" | "4x2" | "4x4"

    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
