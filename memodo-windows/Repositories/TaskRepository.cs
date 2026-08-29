using System;
using System.Collections.Generic;
using Memodo.Windows.Data;
using Memodo.Windows.Models;
using Microsoft.Data.Sqlite;

namespace Memodo.Windows.Repositories;

/// <summary>
/// 待办仓储：Local First（任务书 §8）。删除走软删除墓碑（deletedAt）。
/// </summary>
public sealed class TaskRepository
{
    private readonly SqliteConnection _db;
    public TaskRepository(SqliteConnection db) => _db = db;

    public List<TaskItem> ListActive() => Scan("deleted_at IS NULL");
    public List<TaskItem> ListAllForSync() => Scan("1=1");

    private List<TaskItem> Scan(string where)
    {
        var list = new List<TaskItem>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $@"SELECT id, title, description, completed, priority, due_date,
                                   created_at, updated_at, deleted_at
                            FROM {ModelAttr.Tasks}
                            WHERE {where}
                            ORDER BY completed ASC, updated_at DESC";
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) list.Add(Read(rd));
        return list;
    }

    public TaskItem? GetById(string id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            $"SELECT id, title, description, completed, priority, due_date, created_at, updated_at, deleted_at FROM {ModelAttr.Tasks} WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var rd = cmd.ExecuteReader();
        return rd.Read() ? Read(rd) : null;
    }

    public void Insert(TaskItem item)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $@"
INSERT INTO {ModelAttr.Tasks}
  (id, title, description, completed, priority, due_date, created_at, updated_at)
VALUES ($id, $t, $d, $c, $p, $due, $ca, $ua)";
        Bind(cmd, item);
        cmd.ExecuteNonQuery();
    }

    public void Update(TaskItem item)
    {
        item.UpdatedAt = SqlMapper.NowMs();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $@"
UPDATE {ModelAttr.Tasks}
SET title=$t, description=$d, completed=$c, priority=$p, due_date=$due, updated_at=$ua, deleted_at=$del
WHERE id=$id";
        Bind(cmd, item);
        cmd.ExecuteNonQuery();
    }

    public void SoftDelete(string id)
    {
        using var cmd = _db.CreateCommand();
        var t = SqlMapper.NowMs();
        cmd.CommandText = $"UPDATE {ModelAttr.Tasks} SET deleted_at=$t, updated_at=$t WHERE id=$id";
        cmd.Parameters.AddWithValue("$t", t);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void Restore(string id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"UPDATE {ModelAttr.Tasks} SET deleted_at=NULL, updated_at=$u WHERE id=$id";
        cmd.Parameters.AddWithValue("$u", SqlMapper.NowMs());
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static void Bind(SqliteCommand cmd, TaskItem item)
    {
        cmd.Parameters.AddWithValue("$id", item.Id);
        cmd.Parameters.AddWithValue("$t", item.Title);
        cmd.Parameters.AddWithValue("$d", item.Description);
        cmd.Parameters.AddWithValue("$c", item.Completed ? 1 : 0);
        cmd.Parameters.AddWithValue("$p", item.Priority);
        cmd.Parameters.AddWithValue("$due", SqlMapper.IfNotNull(item.DueDate?.ToString()));
        var now = SqlMapper.NowMs();
        cmd.Parameters.AddWithValue("$ca", item.CreatedAt == 0 ? now : item.CreatedAt);
        cmd.Parameters.AddWithValue("$ua", item.UpdatedAt == 0 ? now : item.UpdatedAt);
        cmd.Parameters.AddWithValue("$del", SqlMapper.IfNotNull(item.DeletedAt?.ToString()));
    }

    private static TaskItem Read(SqliteDataReader rd) => new()
    {
        Id = rd.GetString(0),
        Title = rd.GetString(1),
        Description = rd.GetString(2),
        Completed = rd.GetInt32(3) != 0,
        Priority = rd.GetInt32(4),
        DueDate = rd.IsDBNull(5) ? null : rd.GetInt64(5),
        CreatedAt = rd.GetInt64(6),
        UpdatedAt = rd.GetInt64(7),
        DeletedAt = rd.IsDBNull(8) ? null : rd.GetInt64(8),
    };
}
