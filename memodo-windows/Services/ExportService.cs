using System.Text.Json;
using Memodo.Windows.Data;
using Microsoft.Data.Sqlite;

namespace Memodo.Windows.Services;

/// <summary>
/// 数据导出（蓝图 §52）：全表 JSON 备份，防数据锁死。
/// 首版只做 Export；Import/Markdown/CSV 为后续里程碑。
/// </summary>
public static class ExportService
{
    private static readonly string[] Tables =
        { "tasks", "memos", "boards", "sections", "cards", "card_layouts" };

    public static string ExportJson(AppDatabase db)
    {
        var obj = new Dictionary<string, object?>();
        foreach (var t in Tables) obj[t] = ReadTable(db.Connection, t);
        return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
    }

    private static List<Dictionary<string, object?>> ReadTable(SqliteConnection conn, string table)
    {
        var list = new List<Dictionary<string, object?>>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {table}";
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < rd.FieldCount; i++)
                row[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i);
            list.Add(row);
        }
        return list;
    }
}
