using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

public sealed class MusicPlaylistRepository(HazzDatabase database) : IMusicPlaylistRepository
{
    public async Task<IReadOnlyList<MusicPlaylistSummary>> GetPlaylistsAsync(int limit = 5000, CancellationToken cancellationToken = default)
    {
        await database.InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT p.id,p.name,p.source_type,COUNT(i.id),p.imported_utc
FROM music_playlists p
LEFT JOIN music_playlist_items i ON i.playlist_id=p.id
GROUP BY p.id,p.name,p.source_type,p.imported_utc
ORDER BY
    CASE WHEN p.source_type='BPM Daily History' THEN 0 ELSE 1 END,
    CASE WHEN p.source_type='BPM Daily History' THEN p.name END DESC,
    p.name COLLATE NOCASE
LIMIT $limit;
""";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 20000));
        var rows = new List<MusicPlaylistSummary>();
        await using var r = await command.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            DateTimeOffset? imported = null;
            if (!r.IsDBNull(4) && DateTimeOffset.TryParse(r.GetString(4), out var dto)) imported = dto;
            rows.Add(new MusicPlaylistSummary(r.GetInt64(0), r.GetString(1), r.GetString(2), Convert.ToInt32(r.GetInt64(3)), imported));
        }
        return rows;
    }

    public async Task<IReadOnlyList<MusicPlaylistItem>> GetPlaylistItemsAsync(long playlistId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT id,playlist_id,position,song_id,file_path,artist,title
FROM music_playlist_items WHERE playlist_id=$id ORDER BY position,id;
""";
        command.Parameters.AddWithValue("$id", playlistId);
        var rows = new List<MusicPlaylistItem>();
        await using var r = await command.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
            rows.Add(new MusicPlaylistItem(r.GetInt64(0), r.GetInt64(1), r.GetInt32(2), r.IsDBNull(3) ? null : r.GetInt64(3), r.GetString(4), r.GetString(5), r.GetString(6)));
        return rows;
    }

    public async Task<IReadOnlyList<MusicHistoryEntry>> GetMusicHistoryAsync(int limit = 10000, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT id,source_list,position,song_id,file_path,artist,title,played_at_utc,duration_seconds
FROM music_history ORDER BY datetime(played_at_utc) DESC,id DESC LIMIT $limit;
""";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100000));
        var rows = new List<MusicHistoryEntry>();
        await using var r = await command.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            DateTimeOffset? played = null;
            if (!r.IsDBNull(7) && DateTimeOffset.TryParse(r.GetString(7), out var dto)) played = dto;
            rows.Add(new MusicHistoryEntry(r.GetInt64(0), r.GetString(1), r.GetInt32(2), r.IsDBNull(3) ? null : r.GetInt64(3),
                r.GetString(4), r.GetString(5), r.GetString(6), played, r.IsDBNull(8) ? null : r.GetDouble(8)));
        }
        return rows;
    }

    public async Task SavePlaylistAsync(
        string name,
        IReadOnlyList<MusicPlaylistSaveItem> items,
        CancellationToken cancellationToken = default)
    {
        var cleanName = (name ?? string.Empty).Trim();
        if (cleanName.Length == 0)
            throw new ArgumentException("Playlist name is required.", nameof(name));

        await database.InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        long playlistId;
        await using (var find = connection.CreateCommand())
        {
            find.Transaction = (SqliteTransaction)transaction;
            find.CommandText = """
SELECT id
FROM music_playlists
WHERE source_type='Hazz Saved'
  AND source_path='manual'
  AND name=$name COLLATE NOCASE
LIMIT 1;
""";
            find.Parameters.AddWithValue("$name", cleanName);
            var existing = await find.ExecuteScalarAsync(cancellationToken);
            playlistId = existing is null || existing is DBNull ? 0 : Convert.ToInt64(existing);
        }

        if (playlistId == 0)
        {
            await using var create = connection.CreateCommand();
            create.Transaction = (SqliteTransaction)transaction;
            create.CommandText = """
INSERT INTO music_playlists(name,source_type,source_path,imported_utc)
VALUES($name,'Hazz Saved','manual',$utc);
SELECT last_insert_rowid();
""";
            create.Parameters.AddWithValue("$name", cleanName);
            create.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
            playlistId = Convert.ToInt64(await create.ExecuteScalarAsync(cancellationToken));
        }
        else
        {
            await using var update = connection.CreateCommand();
            update.Transaction = (SqliteTransaction)transaction;
            update.CommandText = """
UPDATE music_playlists
SET name=$name, imported_utc=$utc
WHERE id=$id;
DELETE FROM music_playlist_items WHERE playlist_id=$id;
""";
            update.Parameters.AddWithValue("$name", cleanName);
            update.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$id", playlistId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
INSERT INTO music_playlist_items(playlist_id,position,song_id,file_path,artist,title)
VALUES($playlist,$position,$song,$path,$artist,$title);
""";
            var playlistParam = insert.Parameters.Add("$playlist", SqliteType.Integer);
            var positionParam = insert.Parameters.Add("$position", SqliteType.Integer);
            var songParam = insert.Parameters.Add("$song", SqliteType.Integer);
            var pathParam = insert.Parameters.Add("$path", SqliteType.Text);
            var artistParam = insert.Parameters.Add("$artist", SqliteType.Text);
            var titleParam = insert.Parameters.Add("$title", SqliteType.Text);

            for (var i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = items[i];
                playlistParam.Value = playlistId;
                positionParam.Value = i + 1;
                songParam.Value = item.SongId is long songId ? songId : DBNull.Value;
                pathParam.Value = item.FilePath ?? string.Empty;
                artistParam.Value = item.Artist ?? string.Empty;
                titleParam.Value = item.Title ?? string.Empty;
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordMusicPlayAsync(
        string deckName,
        int position,
        long? songId,
        string filePath,
        string artist,
        string title,
        DateTimeOffset playedAt,
        double? durationSeconds,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO music_history(source_list,position,song_id,file_path,artist,title,played_at_utc,imported_from,duration_seconds)
VALUES($deck,$position,$song,$path,$artist,$title,$played,'Hazz',$duration);
""";
        command.Parameters.AddWithValue("$deck", deckName);
        command.Parameters.AddWithValue("$position", Math.Max(1, position));
        command.Parameters.AddWithValue("$song", songId is null ? DBNull.Value : songId.Value);
        command.Parameters.AddWithValue("$path", filePath ?? string.Empty);
        command.Parameters.AddWithValue("$artist", artist ?? string.Empty);
        command.Parameters.AddWithValue("$title", title ?? string.Empty);
        command.Parameters.AddWithValue("$played", playedAt.ToString("O"));
        command.Parameters.AddWithValue("$duration", durationSeconds is null ? DBNull.Value : durationSeconds.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
