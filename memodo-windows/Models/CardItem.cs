using System;

namespace Memodo.Windows.Models;

/// <summary>
/// 卡片（任务书 §12 + 蓝图 §10）。
/// TODO/MEMO：引用实体（ref_type+ref_uuid），不复制内容（SPD 禁止第二数据源）。
/// IDEA/CHECKLIST：无独立实体表，type+title/content 内联存储（蓝图 §10 Card 是核心）。
/// Color 为卡片纸色/图钉色（蓝图 §38：red/yellow/blue/green，默认 Paper 观感）。
/// </summary>
// table = (see ModelAttr)
public class CardItem
{
    // PK
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string BoardId { get; set; } = string.Empty;
    public string SectionId { get; set; } = string.Empty;
    public string RefType { get; set; } = string.Empty; // "todo" | "memo" | "idea" | "checklist"
    public string RefUuid { get; set; } = string.Empty;
    public int Sort { get; set; }

    /// <summary>内联内容（仅 idea/checklist 使用，todo/memo 恒为空）。</summary>
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Color { get; set; } = "red";

    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long? DeletedAt { get; set; }
}
