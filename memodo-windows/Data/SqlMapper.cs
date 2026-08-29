using System;
using Microsoft.Data.Sqlite;

namespace Memodo.Windows.Data;

/// <summary>
/// 手写 SQLite DTO 映射（任务书 §5 推荐 Microsoft.Data.Sqlite 直用）。
/// ORM 引入需小；用本工具既不引 EF Core 又能清晰控制 SQL。
/// </summary>
internal static class SqlMapper
{
    public static object IfNotNull(long? v) => v ?? (object)DBNull.Value;
    public static object IfNotNull(string? v) => (object?)v ?? DBNull.Value;
    public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
