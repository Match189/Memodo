using System;
using System.Collections.Generic;
using Memodo.Windows.Data;
using Memodo.Windows.Models;
using Microsoft.Data.Sqlite;

namespace Memodo.Windows.Repositories;

public sealed class BoardRepository
{
    private readonly SqliteConnection _db;
    public BoardRepository(SqliteConnection db) => _db = db;

    public List<BoardItem> ListBoards() => ScanBoards("deleted_at IS NULL");

    public BoardItem EnsureDefaultBoard()
    {
        var boards = ListBoards();
        if (boards.Count > 0) return boards[0];
        var b = new BoardItem();
        Insert(b);
        return b;
    }

    public void Insert(BoardItem b)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            $"INSERT INTO {ModelAttr.Boards} (id, name, created_at, updated_at) VALUES ($id, $n, $c, $u)";
        cmd.Parameters.AddWithValue("$id", b.Id);
        cmd.Parameters.AddWithValue("$n", b.Name);
        cmd.Parameters.AddWithValue("$c", b.CreatedAt);
        cmd.Parameters.AddWithValue("$u", b.UpdatedAt == 0 ? SqlMapper.NowMs() : b.UpdatedAt);
        cmd.ExecuteNonQuery();
    }

    public void RenameBoard(string id, string name)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            $"UPDATE {ModelAttr.Boards} SET name=$n, updated_at=$u WHERE id=$id";
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$u", SqlMapper.NowMs());
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public List<CardItem> ListCards(string boardId) => ScanCards(
        $"board_id = $bid AND deleted_at IS NULL", ("$bid", (object)boardId));

    public List<CardItem> ListAllCards() => ScanCards("deleted_at IS NULL");

    public CardItem? GetCard(string id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {ModelAttr.Cards} WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        using var rd = cmd.ExecuteReader();
        return rd.Read() ? ReadCard(rd) : null;
    }

    /// 钉 Todo/Memo 上板（任务书 §12）；同板内同 ref 已存在则不重复。
    public CardItem Pin(string boardId, string refType, string refUuid, string color = "red")
    {
        using var check = _db.CreateCommand();
        check.CommandText =
            $"SELECT * FROM {ModelAttr.Cards} WHERE board_id=$bid AND ref_type=$t AND ref_uuid=$r AND deleted_at IS NULL";
        check.Parameters.AddWithValue("$bid", boardId);
        check.Parameters.AddWithValue("$t", refType);
        check.Parameters.AddWithValue("$r", refUuid);
        using (var rd = check.ExecuteReader())
        {
            if (rd.Read()) return ReadCard(rd);
        }
        var card = new CardItem
        {
            BoardId = boardId,
            RefType = refType,
            RefUuid = refUuid,
            Color = color,
        };
        InsertCard(card);
        return card;
    }

    /// 创建内联卡（蓝图 §10：Idea / Checklist 直接是 Card，不引用实体表）。
    public CardItem CreateInlineCard(string boardId, string type, string title, string content, string color)
    {
        var card = new CardItem
        {
            BoardId = boardId,
            RefType = type,          // "idea" | "checklist"
            RefUuid = "",
            Type = type,
            Title = title,
            Content = content,
            Color = color,
        };
        InsertCard(card);
        return card;
    }

    /// 内联卡编辑（标题/内容/颜色）。todo/memo 的内容请改实体表。
    public void UpdateInlineCard(string id, string title, string content, string color)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $@"UPDATE {ModelAttr.Cards}
                             SET title=$ti, content=$c, color=$col, updated_at=$u WHERE id=$id";
        cmd.Parameters.AddWithValue("$ti", title);
        cmd.Parameters.AddWithValue("$c", content);
        cmd.Parameters.AddWithValue("$col", color);
        cmd.Parameters.AddWithValue("$u", SqlMapper.NowMs());
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// 仅改卡片颜色（todo/memo 也允许换纸色）。
    public void UpdateCardColor(string id, string color)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"UPDATE {ModelAttr.Cards} SET color=$col, updated_at=$u WHERE id=$id";
        cmd.Parameters.AddWithValue("$col", color);
        cmd.Parameters.AddWithValue("$u", SqlMapper.NowMs());
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void InsertCard(CardItem c)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $@"
INSERT INTO {ModelAttr.Cards}
  (id, board_id, section_id, ref_type, ref_uuid, sort, type, title, content, color, created_at, updated_at)
VALUES ($id, $bid, $sid, $t, $r, $s, $ty, $ti, $co, $col, $c, $u)";
        cmd.Parameters.AddWithValue("$id", c.Id);
        cmd.Parameters.AddWithValue("$bid", c.BoardId);
        cmd.Parameters.AddWithValue("$sid", c.SectionId);
        cmd.Parameters.AddWithValue("$t", c.RefType);
        cmd.Parameters.AddWithValue("$r", c.RefUuid);
        cmd.Parameters.AddWithValue("$s", c.Sort);
        cmd.Parameters.AddWithValue("$ty", c.Type);
        cmd.Parameters.AddWithValue("$ti", c.Title);
        cmd.Parameters.AddWithValue("$co", c.Content);
        cmd.Parameters.AddWithValue("$col", c.Color);
        cmd.Parameters.AddWithValue("$c", c.CreatedAt);
        cmd.Parameters.AddWithValue("$u", c.UpdatedAt == 0 ? c.CreatedAt : c.UpdatedAt);
        cmd.ExecuteNonQuery();
    }

    public void UnpinCard(string id)
    {
        using var cmd = _db.CreateCommand();
        var t = SqlMapper.NowMs();
        cmd.CommandText = $"UPDATE {ModelAttr.Cards} SET deleted_at=$t, updated_at=$t WHERE id=$id";
        cmd.Parameters.AddWithValue("$t", t);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public CardLayoutItem? GetLayout(string cardId, string platform)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {ModelAttr.CardLayouts} WHERE card_id=$c AND platform=$p";
        cmd.Parameters.AddWithValue("$c", cardId);
        cmd.Parameters.AddWithValue("$p", platform);
        using var rd = cmd.ExecuteReader();
        return rd.Read() ? ReadLayout(rd) : null;
    }

    public void UpsertLayout(CardLayoutItem layout)
    {
        layout.UpdatedAt = SqlMapper.NowMs();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $@"
INSERT INTO {ModelAttr.CardLayouts}
  (card_id, platform, x, y, width, height, rotation, z, order, size_class, updated_at)
VALUES ($c, $p, $x, $y, $w, $h, $r, $z, $o, $s, $u)
ON CONFLICT(card_id, platform) DO UPDATE SET
  x=excluded.x, y=excluded.y, width=excluded.width, height=excluded.height,
  rotation=excluded.rotation, z=excluded.z, order=excluded.order,
  size_class=excluded.size_class, updated_at=excluded.updated_at";
        cmd.Parameters.AddWithValue("$c", layout.CardId);
        cmd.Parameters.AddWithValue("$p", layout.Platform);
        cmd.Parameters.AddWithValue("$x", layout.X);
        cmd.Parameters.AddWithValue("$y", layout.Y);
        cmd.Parameters.AddWithValue("$w", layout.Width);
        cmd.Parameters.AddWithValue("$h", layout.Height);
        cmd.Parameters.AddWithValue("$r", layout.Rotation);
        cmd.Parameters.AddWithValue("$z", layout.Z);
        cmd.Parameters.AddWithValue("$o", SqlMapper.IfNotNull(layout.Order?.ToString()));
        cmd.Parameters.AddWithValue("$s", SqlMapper.IfNotNull(layout.SizeClass));
        cmd.Parameters.AddWithValue("$u", layout.UpdatedAt);
        cmd.ExecuteNonQuery();
    }

    private List<BoardItem> ScanBoards(string where)
    {
        var list = new List<BoardItem>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            $"SELECT id, name, created_at, updated_at, deleted_at FROM {ModelAttr.Boards} WHERE {where} ORDER BY created_at";
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) list.Add(ReadBoard(rd));
        return list;
    }

    private List<CardItem> ScanCards(string where, params (string, object)[] binds)
    {
        var list = new List<CardItem>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {ModelAttr.Cards} WHERE {where} ORDER BY sort, created_at";
        foreach (var (n, v) in binds) cmd.Parameters.AddWithValue(n, v);
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) list.Add(ReadCard(rd));
        return list;
    }

    private static BoardItem ReadBoard(SqliteDataReader rd) => new()
    {
        Id = rd.GetString(0),
        Name = rd.GetString(1),
        CreatedAt = rd.GetInt64(2),
        UpdatedAt = rd.GetInt64(3),
        DeletedAt = rd.IsDBNull(4) ? null : rd.GetInt64(4),
    };

    private static CardItem ReadCard(SqliteDataReader rd) => new()
    {
        Id = rd.GetString(0),
        BoardId = rd.GetString(1),
        SectionId = rd.GetString(2),
        RefType = rd.GetString(3),
        RefUuid = rd.GetString(4),
        Sort = rd.GetInt32(5),
        CreatedAt = rd.GetInt64(6),
        UpdatedAt = rd.GetInt64(7),
        DeletedAt = rd.IsDBNull(8) ? null : rd.GetInt64(8),
        Type = rd.IsDBNull(9) ? "" : rd.GetString(9),
        Title = rd.IsDBNull(10) ? "" : rd.GetString(10),
        Content = rd.IsDBNull(11) ? "" : rd.GetString(11),
        Color = rd.IsDBNull(12) ? "red" : rd.GetString(12),
    };

    private static CardLayoutItem ReadLayout(SqliteDataReader rd) => new()
    {
        CardId = rd.GetString(1),
        Platform = rd.GetString(2),
        X = rd.GetDouble(3),
        Y = rd.GetDouble(4),
        Width = rd.GetDouble(5),
        Height = rd.GetDouble(6),
        Rotation = rd.GetDouble(7),
        Z = rd.GetInt32(8),
        Order = rd.IsDBNull(9) ? null : rd.GetInt32(9),
        SizeClass = rd.IsDBNull(10) ? null : rd.GetString(10),
        UpdatedAt = rd.GetInt64(11),
    };
}
