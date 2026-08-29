using System;

namespace Memodo.Windows.Models;

/// <summary>
/// 表名/列名 常量集中处（不依赖任何 ORM 注解库）。
/// 迁移版本号：1（v1 一次性建表）。
/// </summary>
public static class ModelAttr
{
    public const int SchemaVersion = 1;

    public const string Tasks = "tasks";
    public const string Memos = "memos";
    public const string Boards = "boards";
    public const string Sections = "sections";
    public const string Cards = "cards";
    public const string CardLayouts = "card_layouts";
}
