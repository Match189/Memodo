using System;
using System.Collections.Generic;
using Memodo.Windows.Data;
using Memodo.Windows.Models;
using Microsoft.Data.Sqlite;

namespace Memodo.Windows.Repositories;

public sealed class MemoRepository
{
    private readonly SqliteConnection _db;
    public MemoRepository(SqliteConnection db) => _db = db;

    public List<MemoItem> ListActive() => Scan("deleted_at IS NULL AND archived_at IS NULL");
    public List<MemoItem> ListArchived() => Scan("archived_at IS NOT NULL AND deleted_at IS NULL");
    public List<MemoItem> ListAllForSync() => Scan("1=1");

    private List<MemoItem> Scan(string where)
    {
        var list = new List<MemoItem>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $@"SELECT id, title, content, completed, show_on_board, created_at, updated_at, deleted_at, archived_at
                            FROM {ModelAttr.Memos} WHERE {where} ORDER BY updated_at DESC";
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) list.Add(Read(rd));
        return list;
    }

    public MemoItem? GetById(string id)
    {
        using var cmd = _db.CreateCommand();
            cmd.CommandText =
            $"SELECT id, title, content, completed, show_on_board, created_at, updated_at, deleted_at, archived_at FROM {ModelAttr.Memos} WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var rd = cmd.ExecuteReader();
        return rd.Read() ? Read(rd) : null;
    }

    public void Insert(MemoItem m)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {ModelAttr.Memos} (id, title, content, completed, show_on_board, created_at, updated_at, archived_at)
                            VALUES ($id, $t, $c, $comp, $sb, $ca, $ua, $arc)";
        Bind(cmd, m);
        cmd.ExecuteNonQuery();
    }

    public void Update(MemoItem m)
    {
        m.UpdatedAt = SqlMapper.NowMs();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $@"UPDATE {ModelAttr.Memos} SET title=$t, content=$c, completed=$comp, show_on_board=$sb, updated_at=$ua, deleted_at=$d, archived_at=$arc WHERE id=$id";
        Bind(cmd, m);
        cmd.ExecuteNonQuery();
    }

    /// <summary>同步专用：按服务端时间戳原样落库（不推进 updated_at，保证 LWW 语义）。</summary>
    public void UpsertFromSync(MemoItem m)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $@"INSERT INTO {ModelAttr.Memos} (id, title, content, completed, show_on_board, created_at, updated_at, deleted_at, archived_at)
                             VALUES ($id, $t, $c, $comp, $sb, $ca, $ua, $d, $arc)
                             ON CONFLICT(id) DO UPDATE SET
                               title=excluded.title, content=excluded.content, completed=excluded.completed,
                               show_on_board=excluded.show_on_board,
                               created_at=excluded.created_at, updated_at=excluded.updated_at,
                               deleted_at=excluded.deleted_at, archived_at=excluded.archived_at";
        Bind(cmd, m);
        cmd.ExecuteNonQuery();
    }

    public void SoftDelete(string id)
    {
        using var cmd = _db.CreateCommand();
        var t = SqlMapper.NowMs();
        cmd.CommandText = $"UPDATE {ModelAttr.Memos} SET deleted_at=$t, updated_at=$t WHERE id=$id";
        cmd.Parameters.AddWithValue("$t", t);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void Restore(string id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"UPDATE {ModelAttr.Memos} SET deleted_at=NULL, updated_at=$u WHERE id=$id";
        cmd.Parameters.AddWithValue("$u", SqlMapper.NowMs());
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>归档所有未归档的备忘。</summary>
    public void ArchiveAll()
    {
        var now = SqlMapper.NowMs();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"UPDATE {ModelAttr.Memos} SET archived_at=$t, updated_at=$t WHERE archived_at IS NULL AND deleted_at IS NULL";
        cmd.Parameters.AddWithValue("$t", now);
        cmd.ExecuteNonQuery();
    }

    /// <summary>取消归档。</summary>
    public void Unarchive(string id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"UPDATE {ModelAttr.Memos} SET archived_at=NULL, updated_at=$u WHERE id=$id";
        cmd.Parameters.AddWithValue("$u", SqlMapper.NowMs());
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void UnarchiveAll()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"UPDATE {ModelAttr.Memos} SET archived_at=NULL, updated_at=$u WHERE archived_at IS NOT NULL";
        cmd.Parameters.AddWithValue("$u", SqlMapper.NowMs());
        cmd.ExecuteNonQuery();
    }

    private static void Bind(SqliteCommand cmd, MemoItem m)
    {
        cmd.Parameters.AddWithValue("$id", m.Id);
        cmd.Parameters.AddWithValue("$t", m.Title);
        cmd.Parameters.AddWithValue("$c", m.Content);
        cmd.Parameters.AddWithValue("$comp", m.Completed ? 1 : 0);
        cmd.Parameters.AddWithValue("$sb", m.ShowOnBoard ? 1 : 0);
        cmd.Parameters.AddWithValue("$ca", m.CreatedAt);
        cmd.Parameters.AddWithValue("$ua", m.UpdatedAt == 0 ? m.CreatedAt : m.UpdatedAt);
        cmd.Parameters.AddWithValue("$d", SqlMapper.IfNotNull(m.DeletedAt?.ToString()));
        cmd.Parameters.AddWithValue("$arc", SqlMapper.IfNotNull(m.ArchivedAt?.ToString()));
    }

    private static MemoItem Read(SqliteDataReader rd) => new()
    {
        Id = rd.GetString(0),
        Title = rd.GetString(1),
        Content = rd.GetString(2),
        Completed = !rd.IsDBNull(3) && rd.GetInt32(3) != 0,
        ShowOnBoard = !rd.IsDBNull(4) && rd.GetInt32(4) != 0,
        CreatedAt = rd.GetInt64(5),
        UpdatedAt = rd.GetInt64(6),
        DeletedAt = rd.IsDBNull(7) ? null : rd.GetInt64(7),
        ArchivedAt = rd.IsDBNull(8) ? null : rd.GetInt64(8),
    };
}
