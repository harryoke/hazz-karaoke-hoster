using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

public sealed class HazzDatabase
{
    public string DatabasePath { get; }
    public string ConnectionString { get; }

    public HazzDatabase(string? databasePath = null)
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hazz Karaoke Hoster");
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
