using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

public sealed class SingerRepository(HazzDatabase database) : ISingerRepository
{
    // Microsoft.Data.Sqlite executes synchronously: keep the entire connection/query off the UI thread.
    public Task<IReadOnlyList<Singer>> SearchSingersAsync(string query, int limit = 100, CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<Singer>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = new SqliteConnection(database.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandTimeout = 2;
            command.CommandText = """
SELECT id, display_name, notes FROM singers
WHERE $query = '' OR instr(lower(display_name), lower($query)) > 0
ORDER BY last_seen_utc DESC, display_name COLLATE NOCASE, id
LIMIT $limit;
""";
            command.Parameters.AddWithValue("$query", (query ?? string.Empty).Trim());
            command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));
            cancellationToken.ThrowIfCancellationRequested();
            using var reader = command.ExecuteReader();
            var rows = new List<Singer>();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(new Singer(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
            cancellationToken.ThrowIfCancellationRequested();
            return rows;
        }, cancellationToken);

    public async Task<long> UpsertSingerAsync(string displayName, string? notes = null, CancellationToken cancellationToken = default)
    {
        displayName = displayName.Trim();
        if (displayName.Length == 0) throw new ArgumentException("Singer name is required.", nameof(displayName));
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO singers(display_name, notes, last_seen_utc) VALUES($name,$notes,CURRENT_TIMESTAMP)
ON CONFLICT(display_name) DO UPDATE SET notes=COALESCE(excluded.notes,singers.notes), last_seen_utc=CURRENT_TIMESTAMP
RETURNING id;
""";
        command.Parameters.AddWithValue("$name", displayName);
        command.Parameters.AddWithValue("$notes", notes ?? (object)DBNull.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<Singer>> GetSingersAsync(int limit = 5000, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, display_name, notes FROM singers ORDER BY display_name COLLATE NOCASE LIMIT $limit";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100000));
        var rows = new List<Singer>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new Singer(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        return rows;
    }


    public async Task<Singer?> GetSingerAsync(long singerId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, display_name, notes FROM singers WHERE id=$id LIMIT 1";
        command.Parameters.AddWithValue("$id", singerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new Singer(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    public async Task UpdateSingerAsync(long singerId, string displayName, string? notes, CancellationToken cancellationToken = default)
    {
        displayName = (displayName ?? string.Empty).Trim();
        if (displayName.Length == 0) throw new ArgumentException("Singer name is required.", nameof(displayName));
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE singers SET display_name=$name, notes=$notes, last_seen_utc=CURRENT_TIMESTAMP WHERE id=$id";
        command.Parameters.AddWithValue("$id", singerId);
        command.Parameters.AddWithValue("$name", displayName);
        command.Parameters.AddWithValue("$notes", string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddHistoryAsync(long singerId, long? songId, string artist, string title, string filePath, DateTimeOffset sungAt, int keyChange, double cdgSyncSeconds, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO singer_history(singer_id,song_id,artist,title,file_path,sung_at_utc,key_change,cdg_sync_seconds,times_sung)
VALUES($singer,$song,$artist,$title,$path,$date,$key,$sync,1);
""";
        command.Parameters.AddWithValue("$singer", singerId);
        command.Parameters.AddWithValue("$song", songId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$artist", artist);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$path", filePath);
        command.Parameters.AddWithValue("$date", sungAt.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$key", keyChange);
        command.Parameters.AddWithValue("$sync", cdgSyncSeconds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SingerHistoryEntry>> GetHistoryAsync(long singerId, int limit = 500, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // The singer-facing history is one row per song, while the underlying table still retains
        // individual Hazz performance events. Karma may provide one aggregate row with times_sung > 1.
        // Grouping here gives a clean Karma-style history with Last Sung + total Times Sung.
        command.CommandText = """
WITH ranked AS (
    SELECT id,singer_id,song_id,artist,title,file_path,sung_at_utc,key_change,cdg_sync_seconds,
           CASE WHEN times_sung < 1 THEN 1 ELSE times_sung END AS times_sung,
           ROW_NUMBER() OVER (
               PARTITION BY singer_id,
                            CASE WHEN trim(file_path)<>'' THEN 'P|'||lower(trim(file_path))
                                 ELSE 'T|'||lower(trim(artist))||'|'||lower(trim(title)) END
               ORDER BY sung_at_utc DESC,id DESC
           ) AS rn,
           SUM(CASE WHEN times_sung < 1 THEN 1 ELSE times_sung END) OVER (
               PARTITION BY singer_id,
                            CASE WHEN trim(file_path)<>'' THEN 'P|'||lower(trim(file_path))
                                 ELSE 'T|'||lower(trim(artist))||'|'||lower(trim(title)) END
           ) AS total_times
    FROM singer_history
    WHERE singer_id=$id
)
SELECT id,singer_id,song_id,artist,title,file_path,sung_at_utc,key_change,cdg_sync_seconds,total_times
FROM ranked
WHERE rn=1
ORDER BY sung_at_utc DESC,id DESC
LIMIT $limit;
""";
        command.Parameters.AddWithValue("$id", singerId);
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit,1,5000));
        var rows = new List<SingerHistoryEntry>();
        await using var r = await command.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            rows.Add(new SingerHistoryEntry(r.GetInt64(0),r.GetInt64(1),r.IsDBNull(2)?null:r.GetInt64(2),r.GetString(3),r.GetString(4),r.GetString(5),
                DateTimeOffset.Parse(r.GetString(6)),r.GetInt32(7),r.GetDouble(8),Convert.ToInt32(r.GetInt64(9))));
        }
        return rows;
    }
    public async Task<IReadOnlyList<SingerHistoryEntry>> SearchHistoryAsync(long singerId, string query, int limit = 5000, CancellationToken cancellationToken = default)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length == 0) return await GetHistoryAsync(singerId, limit, cancellationToken);

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
WITH ranked AS (
    SELECT id,singer_id,song_id,artist,title,file_path,sung_at_utc,key_change,cdg_sync_seconds,
           CASE WHEN times_sung < 1 THEN 1 ELSE times_sung END AS times_sung,
           ROW_NUMBER() OVER (
               PARTITION BY singer_id,
                            CASE WHEN trim(file_path)<>'' THEN 'P|'||lower(trim(file_path))
                                 ELSE 'T|'||lower(trim(artist))||'|'||lower(trim(title)) END
               ORDER BY sung_at_utc DESC,id DESC
           ) AS rn,
           SUM(CASE WHEN times_sung < 1 THEN 1 ELSE times_sung END) OVER (
               PARTITION BY singer_id,
                            CASE WHEN trim(file_path)<>'' THEN 'P|'||lower(trim(file_path))
                                 ELSE 'T|'||lower(trim(artist))||'|'||lower(trim(title)) END
           ) AS total_times
    FROM singer_history
    WHERE singer_id=$id
      AND (instr(lower(artist), $q) > 0 OR instr(lower(title), $q) > 0)
)
SELECT id,singer_id,song_id,artist,title,file_path,sung_at_utc,key_change,cdg_sync_seconds,total_times
FROM ranked
WHERE rn=1
ORDER BY sung_at_utc DESC,id DESC
LIMIT $limit;
""";
        command.Parameters.AddWithValue("$id", singerId);
        command.Parameters.AddWithValue("$q", query.ToLowerInvariant());
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit,1,10000));
        var rows = new List<SingerHistoryEntry>();
        await using var r = await command.ExecuteReaderAsync(cancellationToken);
        while (await r.ReadAsync(cancellationToken))
        {
            rows.Add(new SingerHistoryEntry(r.GetInt64(0),r.GetInt64(1),r.IsDBNull(2)?null:r.GetInt64(2),r.GetString(3),r.GetString(4),r.GetString(5),
                DateTimeOffset.Parse(r.GetString(6)),r.GetInt32(7),r.GetDouble(8),Convert.ToInt32(r.GetInt64(9))));
        }
        return rows;
    }

    public async Task<RecentSingerPerformance?> FindRecentPerformanceAsync(long? songId, string artist, string title, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default)
    {
        artist = (artist ?? string.Empty).Trim();
        title = (title ?? string.Empty).Trim();
        if (songId is null && title.Length == 0) return null;

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT h.singer_id, s.display_name, h.sung_at_utc, h.song_id, h.artist, h.title
FROM singer_history h
JOIN singers s ON s.id=h.singer_id
WHERE h.sung_at_utc >= $since
  AND (
      ($hasSong=1 AND h.song_id=$song)
      OR (lower(trim(h.artist))=lower(trim($artist)) AND lower(trim(h.title))=lower(trim($title)))
  )
ORDER BY h.sung_at_utc DESC, h.id DESC
LIMIT 1;
""";
        command.Parameters.AddWithValue("$since", sinceUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$hasSong", songId is null ? 0 : 1);
        command.Parameters.AddWithValue("$song", songId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$artist", artist);
        command.Parameters.AddWithValue("$title", title);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new RecentSingerPerformance(
            reader.GetInt64(0),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2)),
            reader.IsDBNull(3) ? null : reader.GetInt64(3),
            reader.GetString(4),
            reader.GetString(5));
    }

}
