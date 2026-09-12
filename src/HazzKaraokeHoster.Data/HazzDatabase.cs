using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

public sealed class HazzDatabase
{
    private int _optimizedThisRun;
    public string DatabasePath { get; }
    public string ConnectionString { get; }

    public HazzDatabase(string? databasePath = null)
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hazz Karaoke Hoster LibVLC Test");
        Directory.CreateDirectory(appData);
        DatabasePath = databasePath ?? Path.Combine(appData, "hazz-hoster.db");
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            ForeignKeys = true,
            DefaultTimeout = 15
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA temp_store=MEMORY;
PRAGMA foreign_keys=ON;
PRAGMA busy_timeout=15000;
PRAGMA wal_autocheckpoint=1000;

CREATE TABLE IF NOT EXISTS songs (
    id INTEGER PRIMARY KEY,
    artist TEXT NOT NULL DEFAULT '',
    title TEXT NOT NULL DEFAULT '',
    manufacturer TEXT NOT NULL DEFAULT '',
    disc_id TEXT NOT NULL DEFAULT '',
    file_path TEXT NOT NULL UNIQUE,
    format TEXT NOT NULL DEFAULT '',
    file_size INTEGER NOT NULL DEFAULT 0,
    date_added TEXT NULL,
    cdg_sync_seconds REAL NOT NULL DEFAULT 0,
    preferred_key INTEGER NOT NULL DEFAULT 0,
    last_seen_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    media_kind TEXT NOT NULL DEFAULT 'Karaoke'
);

CREATE VIRTUAL TABLE IF NOT EXISTS songs_fts USING fts5(
    artist, title, manufacturer, disc_id, file_path,
    content='songs', content_rowid='id', tokenize='unicode61 remove_diacritics 2'
);
CREATE TRIGGER IF NOT EXISTS songs_ai AFTER INSERT ON songs BEGIN
  INSERT INTO songs_fts(rowid, artist, title, manufacturer, disc_id, file_path)
  VALUES (new.id, new.artist, new.title, new.manufacturer, new.disc_id, new.file_path);
END;
CREATE TRIGGER IF NOT EXISTS songs_ad AFTER DELETE ON songs BEGIN
  INSERT INTO songs_fts(songs_fts, rowid, artist, title, manufacturer, disc_id, file_path)
  VALUES('delete', old.id, old.artist, old.title, old.manufacturer, old.disc_id, old.file_path);
END;
CREATE TRIGGER IF NOT EXISTS songs_au AFTER UPDATE ON songs BEGIN
  INSERT INTO songs_fts(songs_fts, rowid, artist, title, manufacturer, disc_id, file_path)
  VALUES('delete', old.id, old.artist, old.title, old.manufacturer, old.disc_id, old.file_path);
  INSERT INTO songs_fts(rowid, artist, title, manufacturer, disc_id, file_path)
  VALUES (new.id, new.artist, new.title, new.manufacturer, new.disc_id, new.file_path);
END;

CREATE TABLE IF NOT EXISTS singers (
    id INTEGER PRIMARY KEY,
    display_name TEXT NOT NULL COLLATE NOCASE UNIQUE,
    notes TEXT NULL,
    created_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_seen_utc TEXT NULL
);

-- display_name already has a UNIQUE NOCASE index. Match the picker ordering without a sort.
CREATE INDEX IF NOT EXISTS ix_singers_recent_name ON singers(last_seen_utc DESC, display_name COLLATE NOCASE, id);

CREATE TABLE IF NOT EXISTS singer_history (
    id INTEGER PRIMARY KEY,
    singer_id INTEGER NOT NULL REFERENCES singers(id) ON DELETE CASCADE,
    song_id INTEGER NULL REFERENCES songs(id) ON DELETE SET NULL,
    artist TEXT NOT NULL DEFAULT '',
    title TEXT NOT NULL DEFAULT '',
    file_path TEXT NOT NULL DEFAULT '',
    sung_at_utc TEXT NOT NULL,
    key_change INTEGER NOT NULL DEFAULT 0,
    cdg_sync_seconds REAL NOT NULL DEFAULT 0,
    times_sung INTEGER NOT NULL DEFAULT 1,
    imported_from TEXT NULL
);
CREATE INDEX IF NOT EXISTS ix_history_singer_date ON singer_history(singer_id, sung_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_history_song ON singer_history(song_id);
CREATE INDEX IF NOT EXISTS ix_history_date ON singer_history(sung_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_history_song_date ON singer_history(song_id, sung_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_history_import_source ON singer_history(imported_from);

CREATE TABLE IF NOT EXISTS show_state (
    key TEXT PRIMARY KEY,
    json_value TEXT NOT NULL,
    updated_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS import_log (
    id INTEGER PRIMARY KEY,
    source_type TEXT NOT NULL,
    source_path TEXT NOT NULL,
    imported_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    singers_imported INTEGER NOT NULL DEFAULT 0,
    history_rows_imported INTEGER NOT NULL DEFAULT 0,
    report_json TEXT NULL
);

CREATE TABLE IF NOT EXISTS music_playlists (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL,
    source_type TEXT NOT NULL DEFAULT 'Hazz',
    source_path TEXT NULL,
    imported_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(name, source_type, source_path)
);

CREATE TABLE IF NOT EXISTS music_playlist_items (
    id INTEGER PRIMARY KEY,
    playlist_id INTEGER NOT NULL REFERENCES music_playlists(id) ON DELETE CASCADE,
    position INTEGER NOT NULL,
    song_id INTEGER NULL REFERENCES songs(id) ON DELETE SET NULL,
    file_path TEXT NOT NULL,
    artist TEXT NOT NULL DEFAULT '',
    title TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS ix_music_playlist_items_playlist_position ON music_playlist_items(playlist_id, position);

CREATE TABLE IF NOT EXISTS music_history (
    id INTEGER PRIMARY KEY,
    source_list TEXT NOT NULL DEFAULT '',
    position INTEGER NOT NULL DEFAULT 0,
    song_id INTEGER NULL REFERENCES songs(id) ON DELETE SET NULL,
    file_path TEXT NOT NULL DEFAULT '',
    artist TEXT NOT NULL DEFAULT '',
    title TEXT NOT NULL DEFAULT '',
    played_at_utc TEXT NULL,
    imported_from TEXT NULL,
    duration_seconds REAL NULL
);
CREATE INDEX IF NOT EXISTS ix_music_history_played ON music_history(played_at_utc DESC);

CREATE TABLE IF NOT EXISTS library_roots (
    id INTEGER PRIMARY KEY,
    path TEXT NOT NULL COLLATE NOCASE,
    media_kind TEXT NOT NULL,
    include_subfolders INTEGER NOT NULL DEFAULT 1,
    added_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(path, media_kind)
);

CREATE TABLE IF NOT EXISTS song_sources (
    id INTEGER PRIMARY KEY,
    song_id INTEGER NOT NULL REFERENCES songs(id) ON DELETE CASCADE,
    source_type TEXT NOT NULL,
    source_path TEXT NOT NULL DEFAULT '',
    imported_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(song_id, source_type, source_path)
);
CREATE INDEX IF NOT EXISTS ix_song_sources_source ON song_sources(source_type, source_path);

CREATE TABLE IF NOT EXISTS virtual_folders (
    id INTEGER PRIMARY KEY,
    parent_id INTEGER NULL REFERENCES virtual_folders(id) ON DELETE CASCADE,
    name TEXT NOT NULL COLLATE NOCASE,
    created_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK(parent_id IS NULL OR parent_id <> id)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_virtual_folders_parent_name
    ON virtual_folders(COALESCE(parent_id, 0), name COLLATE NOCASE);

CREATE TABLE IF NOT EXISTS virtual_folder_songs (
    folder_id INTEGER NOT NULL REFERENCES virtual_folders(id) ON DELETE CASCADE,
    song_id INTEGER NOT NULL REFERENCES songs(id) ON DELETE CASCADE,
    added_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY(folder_id, song_id)
);
CREATE INDEX IF NOT EXISTS ix_virtual_folder_songs_song ON virtual_folder_songs(song_id, folder_id);

CREATE TABLE IF NOT EXISTS bpm_virtual_folder_sources (
    source_path TEXT PRIMARY KEY COLLATE NOCASE,
    file_size INTEGER NOT NULL,
    last_write_utc TEXT NOT NULL,
    folder_path TEXT NOT NULL,
    track_count INTEGER NOT NULL DEFAULT 0,
    imported_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS ix_bpm_virtual_folder_sources_stamp
    ON bpm_virtual_folder_sources(file_size, last_write_utc);
""";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // Incremental migrations for databases created by earlier Hazz builds.
        await EnsureColumnAsync(connection, "songs", "media_kind", "TEXT NOT NULL DEFAULT 'Karaoke'", cancellationToken);
        await EnsureColumnAsync(connection, "music_history", "duration_seconds", "REAL NULL", cancellationToken);
        await EnsureColumnAsync(connection, "singer_history", "times_sung", "INTEGER NOT NULL DEFAULT 1", cancellationToken);

        await using var indexes = connection.CreateCommand();
        indexes.CommandText = """
CREATE INDEX IF NOT EXISTS ix_songs_artist_title ON songs(artist, title);
CREATE INDEX IF NOT EXISTS ix_songs_disc_id ON songs(disc_id);
CREATE INDEX IF NOT EXISTS ix_songs_manufacturer ON songs(manufacturer);
CREATE INDEX IF NOT EXISTS ix_songs_file_path ON songs(file_path);
CREATE INDEX IF NOT EXISTS ix_songs_media_kind ON songs(media_kind);
CREATE INDEX IF NOT EXISTS ix_songs_kind_id ON songs(media_kind, id);
CREATE INDEX IF NOT EXISTS ix_songs_kind_artist_title ON songs(media_kind, artist, title, id);
CREATE INDEX IF NOT EXISTS ix_songs_kind_title_artist ON songs(media_kind, title, artist, id);
CREATE INDEX IF NOT EXISTS ix_songs_kind_manufacturer ON songs(media_kind, manufacturer, artist, title, id);
CREATE INDEX IF NOT EXISTS ix_songs_kind_date_added ON songs(media_kind, date_added, id);
""";
        await indexes.ExecuteNonQueryAsync(cancellationToken);

        // Refresh planner statistics once per application run. PRAGMA optimize is deliberately
        // bounded by SQLite and avoids a full VACUUM, so startup remains safe for large show data.
        if (Interlocked.Exchange(ref _optimizedThisRun, 1) == 0)
        {
            await using var optimize = connection.CreateCommand();
            optimize.CommandText = "PRAGMA optimize;";
            await optimize.ExecuteNonQueryAsync(cancellationToken);
        }

        await MergeDuplicateVirtualFoldersAsync(connection, cancellationToken);
    }

    public long GetStorageSizeBytes()
    {
        long total = 0;
        foreach (var path in new[] { DatabasePath, DatabasePath + "-wal", DatabasePath + "-shm" })
        {
            try { if (File.Exists(path)) total += new FileInfo(path).Length; } catch { }
        }
        return total;
    }

    private static async Task MergeDuplicateVirtualFoldersAsync(SqliteConnection connection, CancellationToken token)
    {
        // Older test builds could create the same imported folder more than once. Consolidate
        // sibling folders by name on startup, moving child folders and links into one survivor.
        while (true)
        {
            var duplicate = new List<(long Keep, long Duplicate)>();
            var groups = new Dictionary<(long Parent, string Name), List<long>>();
            await using (var rows = connection.CreateCommand())
            {
                rows.CommandText = "SELECT id,COALESCE(parent_id,0),name FROM virtual_folders ORDER BY id";
                await using var all = await rows.ExecuteReaderAsync(token);
                while (await all.ReadAsync(token))
                {
                    var key = (all.GetInt64(1), all.GetString(2).Trim().ToUpperInvariant());
                    if (!groups.TryGetValue(key, out var ids)) groups[key] = ids = new();
                    ids.Add(all.GetInt64(0));
                }
            }
            foreach (var ids in groups.Values.Where(x => x.Count > 1))
                duplicate.AddRange(ids.Skip(1).Select(id => (ids[0], id)));
            if (duplicate.Count == 0) return;
            foreach (var pair in duplicate)
            {
                token.ThrowIfCancellationRequested();
                await MergeFolderAsync(connection, pair.Keep, pair.Duplicate, token);
            }
        }
    }

    private static async Task MergeFolderAsync(SqliteConnection c, long keepId, long duplicateId, CancellationToken token)
    {
        var children = new List<(long Id, string Name)>();
        await using (var getChildren = c.CreateCommand())
        {
            getChildren.CommandText = "SELECT id,name FROM virtual_folders WHERE parent_id=$parent";
            getChildren.Parameters.AddWithValue("$parent", duplicateId);
            await using var reader = await getChildren.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) children.Add((reader.GetInt64(0), reader.GetString(1)));
        }
        foreach (var child in children)
        {
            long? matching = null;
            await using (var find = c.CreateCommand())
            {
                find.CommandText = "SELECT id FROM virtual_folders WHERE parent_id=$parent AND name=$name COLLATE NOCASE LIMIT 1";
                find.Parameters.AddWithValue("$parent", keepId);
                find.Parameters.AddWithValue("$name", child.Name);
                var value = await find.ExecuteScalarAsync(token);
                if (value is not null && value is not DBNull) matching = Convert.ToInt64(value);
            }
            if (matching is long existing) await MergeFolderAsync(c, existing, child.Id, token);
            else
            {
                await using var move = c.CreateCommand();
                move.CommandText = "UPDATE virtual_folders SET parent_id=$parent WHERE id=$id";
                move.Parameters.AddWithValue("$parent", keepId); move.Parameters.AddWithValue("$id", child.Id);
                await move.ExecuteNonQueryAsync(token);
            }
        }
        await using (var links = c.CreateCommand())
        {
            links.CommandText = """
INSERT OR IGNORE INTO virtual_folder_songs(folder_id,song_id)
SELECT $keep,song_id FROM virtual_folder_songs WHERE folder_id=$duplicate;
DELETE FROM virtual_folder_songs WHERE folder_id=$duplicate;
DELETE FROM virtual_folders WHERE id=$duplicate;
""";
            links.Parameters.AddWithValue("$keep", keepId); links.Parameters.AddWithValue("$duplicate", duplicateId);
            await links.ExecuteNonQueryAsync(token);
        }
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken cancellationToken)
    {
        await using var info = connection.CreateCommand();
        info.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await info.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        }
        await reader.DisposeAsync();

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }
    public async Task BackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("Choose a backup file.", nameof(destinationPath));
        destinationPath = Path.GetFullPath(destinationPath);
        var folder = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);

        await InitializeAsync(cancellationToken);
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            using var source = new SqliteConnection(ConnectionString);
            source.Open();
            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = destinationPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString());
            destination.Open();
            source.BackupDatabase(destination);
        }, cancellationToken);
    }

}
