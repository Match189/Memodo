using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Memodo.Windows.Data;

/// <summary>
/// 本地 SQLite 数据库（任务书 §5 + §11 Local-first）。
/// 库文件位置：%APPDATA%\app.memodo\Memodo\memodo.db
/// 禁止把业务数据写 Windows 注册表——单一事实源只能是数据库。
/// </summary>
public sealed class AppDatabase : IDisposable
{
    public SqliteConnection Connection { get; }

    private AppDatabase(SqliteConnection connection)
    {
        Connection = connection;
    }

    public static AppDatabase Open()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "app.memodo", "Memodo");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "memodo.db");
        var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        var db = new AppDatabase(conn);
        db.Migrate();
        return db;
    }

    /// <summary>
    /// 内联版本迁移（任务书 §44）。每次 schema 变更：bump Version、添加新分支。
    /// 不允许 drop & recreate。
    /// </summary>
    private void Migrate()
    {
        // v1：所有表一次创建。后续版本用 PRAGMA user_version + switch。
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA busy_timeout = 3000;

CREATE TABLE IF NOT EXISTS tasks (
    id            TEXT PRIMARY KEY NOT NULL,
    title         TEXT NOT NULL,
    description   TEXT NOT NULL DEFAULT '',
    completed     INTEGER NOT NULL DEFAULT 0,
    priority      INTEGER NOT NULL DEFAULT 0,
    due_date      INTEGER,
    created_at    INTEGER NOT NULL,
    updated_at    INTEGER NOT NULL,
    deleted_at    INTEGER
);
CREATE INDEX IF NOT EXISTS ix_tasks_done ON tasks(completed);
CREATE INDEX IF NOT EXISTS ix_tasks_updated ON tasks(updated_at);

CREATE TABLE IF NOT EXISTS memos (
    id            TEXT PRIMARY KEY NOT NULL,
    title         TEXT NOT NULL,
    content       TEXT NOT NULL DEFAULT '',
    created_at    INTEGER NOT NULL,
    updated_at    INTEGER NOT NULL,
    deleted_at    INTEGER
);
CREATE INDEX IF NOT EXISTS ix_memos_updated ON memos(updated_at);

CREATE TABLE IF NOT EXISTS boards (
    id            TEXT PRIMARY KEY NOT NULL,
    name          TEXT NOT NULL DEFAULT '',
    created_at    INTEGER NOT NULL,
    updated_at    INTEGER NOT NULL,
    deleted_at    INTEGER
);

CREATE TABLE IF NOT EXISTS sections (
    id            TEXT PRIMARY KEY NOT NULL,
    board_id      TEXT NOT NULL,
    name          TEXT NOT NULL,
    sort          INTEGER NOT NULL DEFAULT 0,
    created_at    INTEGER NOT NULL,
    updated_at    INTEGER NOT NULL,
    deleted_at    INTEGER
);
CREATE INDEX IF NOT EXISTS ix_sections_board ON sections(board_id);

CREATE TABLE IF NOT EXISTS cards (
    id            TEXT PRIMARY KEY NOT NULL,
    board_id      TEXT NOT NULL,
    section_id    TEXT NOT NULL DEFAULT '',
    ref_type      TEXT NOT NULL,
    ref_uuid      TEXT NOT NULL,
    sort          INTEGER NOT NULL DEFAULT 0,
    created_at    INTEGER NOT NULL,
    updated_at    INTEGER NOT NULL,
    deleted_at    INTEGER
);
CREATE INDEX IF NOT EXISTS ix_cards_board ON cards(board_id);
CREATE INDEX IF NOT EXISTS ix_cards_ref ON cards(ref_type, ref_uuid);

CREATE TABLE IF NOT EXISTS card_layouts (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    card_id       TEXT NOT NULL,
    platform      TEXT NOT NULL DEFAULT 'windows',
    x             REAL NOT NULL DEFAULT 0,
    y             REAL NOT NULL DEFAULT 0,
    width         REAL NOT NULL DEFAULT 190,
    height        REAL NOT NULL DEFAULT 150,
    rotation      REAL NOT NULL DEFAULT 0,
    z             INTEGER NOT NULL DEFAULT 0,
    order         INTEGER,
    size_class    TEXT,
    updated_at    INTEGER NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_card_platform ON card_layouts(card_id, platform);
";
        cmd.ExecuteNonQuery();

        // 列schema幂等补齐（每次启动执行，代价为几条 PRAGMA）。
        // 不再依赖 user_version 门控——曾因版本错位导致 "ordinal 越界" 同步失败。
        AddColumnIfMissing("cards", "type", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing("cards", "title", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing("cards", "content", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing("cards", "color", "TEXT NOT NULL DEFAULT 'red'");
        AddColumnIfMissing("cards", "note_color", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing("memos", "completed", "INTEGER NOT NULL DEFAULT 0");
        // v5（用户裁定）：备忘改用「是否显示在钉板」语义（眼睛按钮），completed 保留但不再使用
        AddColumnIfMissing("memos", "show_on_board", "INTEGER NOT NULL DEFAULT 1");
        // 归档时间戳：Android 侧 TaskItem / MemoItem 均有 archived_at，Windows 须补齐。
        AddColumnIfMissing("tasks", "archived_at", "INTEGER");
        AddColumnIfMissing("memos", "archived_at", "INTEGER");
    }

    private long UserVersion
    {
        get
        {
            using var c = Connection.CreateCommand();
            c.CommandText = "PRAGMA user_version";
            return (long)(c.ExecuteScalar() ?? 0L);
        }
        set
        {
            using var c = Connection.CreateCommand();
            c.CommandText = $"PRAGMA user_version={value}";
            c.ExecuteNonQuery();
        }
    }

    private void AddColumnIfMissing(string table, string column, string decl)
    {
        using var check = Connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        using (var rd = check.ExecuteReader())
        {
            while (rd.Read())
                if (rd.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                    return; // 已存在
        }
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {decl}";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => Connection.Dispose();
}
