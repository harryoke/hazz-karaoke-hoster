using System.Globalization;
using System.Text;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

public sealed class LibraryRepository(HazzDatabase database) : ILibraryRepository
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) => database.InitializeAsync(cancellationToken);

    public Task<IReadOnlyList<SongRecord>> SearchAsync(string query, int limit = 200, CancellationToken cancellationToken = default)
        => SearchByKindAsync(query, "Karaoke", limit, cancellationToken);

    public async Task<IReadOnlyList<SongRecord>> SearchByKindAsync(string query, string? mediaKind, int limit = 200, CancellationToken cancellationToken = default)
    {
        query = (query ?? string.Empty).Trim();
        if (query.Length == 0) return Array.Empty<SongRecord>();
        limit = Math.Clamp(limit, 1, 2000);
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .Select(t => t.Replace("\"", "\"\"") + "*");
        var fts = string.Join(" AND ", terms);

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT s.id, s.artist, s.title, s.manufacturer, s.disc_id, s.file_path, s.format,
       s.file_size, s.date_added, s.cdg_sync_seconds, s.preferred_key, s.media_kind
FROM songs_fts f
JOIN songs s ON s.id = f.rowid
WHERE songs_fts MATCH $q
  AND ($kind IS NULL OR s.media_kind = $kind)
ORDER BY bm25(songs_fts), s.artist, s.title
LIMIT $limit;
""";
        command.Parameters.AddWithValue("$q", fts);
        command.Parameters.AddWithValue("$kind", mediaKind is null ? DBNull.Value : mediaKind);
        command.Parameters.AddWithValue("$limit", limit);
        var result = new List<SongRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(ReadSong(reader));
        return result;
    }

    public async Task<SongRecord?> FindByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        filePath = Path.GetFullPath(filePath);
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT id, artist, title, manufacturer, disc_id, file_path, format,
       file_size, date_added, cdg_sync_seconds, preferred_key, media_kind
FROM songs WHERE file_path=$path LIMIT 1;
""";
        command.Parameters.AddWithValue("$path", filePath);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSong(reader) : null;
    }

    public async Task<IReadOnlyList<SongRecord>> FindAlternativesAsync(string artist, string title, string? excludeFilePath = null, int limit = 100, CancellationToken cancellationToken = default)
    {
        artist = (artist ?? string.Empty).Trim();
        title = (title ?? string.Empty).Trim();
        if (title.Length == 0) return Array.Empty<SongRecord>();

        IReadOnlyList<SongRecord> candidates;
        try
        {
            candidates = await SearchByKindAsync(string.Join(' ', new[] { artist, title }.Where(x => !string.IsNullOrWhiteSpace(x))), "Karaoke", 1500, cancellationToken);
        }
        catch
        {
            candidates = Array.Empty<SongRecord>();
        }

        // FTS can be too strict for punctuation-heavy titles. Fall back to a title/artist query.
        if (candidates.Count == 0)
        {
            await using var connection = new SqliteConnection(database.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
SELECT id, artist, title, manufacturer, disc_id, file_path, format,
       file_size, date_added, cdg_sync_seconds, preferred_key, media_kind
FROM songs
WHERE media_kind='Karaoke'
  AND title LIKE $title COLLATE NOCASE
  AND ($artist='' OR artist LIKE $artist COLLATE NOCASE)
LIMIT 1500;
""";
            command.Parameters.AddWithValue("$title", $"%{title}%");
            command.Parameters.AddWithValue("$artist", artist.Length == 0 ? string.Empty : $"%{artist}%");
            var temp = new List<SongRecord>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) temp.Add(ReadSong(reader));
            candidates = temp;
        }

        var artistKey = Normalize(artist);
        var titleKey = Normalize(title);
        var result = candidates
            .Where(s => string.Equals(s.MediaKind, "Karaoke", StringComparison.OrdinalIgnoreCase))
            .Where(s => string.IsNullOrWhiteSpace(excludeFilePath) || !PathEquals(s.FilePath, excludeFilePath))
            .Where(s => artistKey.Length == 0 || Normalize(s.Artist) == artistKey)
            .Where(s => IsLooseTitleMatch(titleKey, Normalize(s.Title)))
            .GroupBy(s => Path.GetFullPath(s.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(s => s.Manufacturer)
            .ThenBy(s => s.DiscId)
            .ThenBy(s => s.FilePath)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
        return result;
    }

    public async Task<long> UpsertSongAsync(SongRecord song, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO songs(artist,title,manufacturer,disc_id,file_path,format,file_size,date_added,cdg_sync_seconds,preferred_key,last_seen_utc,media_kind)
VALUES($artist,$title,$manufacturer,$disc,$path,$format,$size,$added,$sync,$key,CURRENT_TIMESTAMP,$kind)
ON CONFLICT(file_path) DO UPDATE SET
 artist=excluded.artist,title=excluded.title,manufacturer=excluded.manufacturer,disc_id=excluded.disc_id,
 format=excluded.format,file_size=excluded.file_size,date_added=COALESCE(excluded.date_added,songs.date_added),
 media_kind=excluded.media_kind,last_seen_utc=CURRENT_TIMESTAMP
RETURNING id;
""";
        command.Parameters.AddWithValue("$artist", song.Artist);
        command.Parameters.AddWithValue("$title", song.Title);
        command.Parameters.AddWithValue("$manufacturer", song.Manufacturer);
        command.Parameters.AddWithValue("$disc", song.DiscId);
        command.Parameters.AddWithValue("$path", song.FilePath);
        command.Parameters.AddWithValue("$format", song.Format);
        command.Parameters.AddWithValue("$size", song.FileSize);
        command.Parameters.AddWithValue("$added", song.DateAdded?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$sync", song.CdgSyncSeconds);
        command.Parameters.AddWithValue("$key", song.PreferredKey);
        command.Parameters.AddWithValue("$kind", string.IsNullOrWhiteSpace(song.MediaKind) ? "Karaoke" : song.MediaKind);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value);
    }

    public async Task SaveTrackPreferencesAsync(long songId, int keyChange, double cdgSyncSeconds, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE songs SET preferred_key=$key, cdg_sync_seconds=$sync WHERE id=$id";
        command.Parameters.AddWithValue("$key", Math.Clamp(keyChange, -6, 6));
        command.Parameters.AddWithValue("$sync", Math.Round(cdgSyncSeconds * 4) / 4.0);
        command.Parameters.AddWithValue("$id", songId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LibraryBrowsePage> BrowseAsync(
        string mediaKind,
        string? filter,
        string sortBy,
        bool descending,
        int offset,
        int pageSize = 500,
        CancellationToken cancellationToken = default)
    {
        mediaKind = string.Equals(mediaKind, "Music", StringComparison.OrdinalIgnoreCase) ? "Music" : "Karaoke";
        filter = (filter ?? string.Empty).Trim();
        offset = Math.Max(0, offset);
        pageSize = Math.Clamp(pageSize, 50, 1000);

        var direction = descending ? "DESC" : "ASC";
        var orderBy = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "title" => $"s.title COLLATE NOCASE {direction}, s.artist COLLATE NOCASE {direction}, s.id {direction}",
            "manufacturer" => $"s.manufacturer COLLATE NOCASE {direction}, s.artist COLLATE NOCASE {direction}, s.title COLLATE NOCASE {direction}, s.id {direction}",
            "dateadded" => $"s.date_added {direction}, s.id {direction}",
            _ => $"s.artist COLLATE NOCASE {direction}, s.title COLLATE NOCASE {direction}, s.id {direction}"
        };

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var hasFilter = filter.Length > 0;
        var fts = hasFilter
            ? string.Join(" AND ", filter.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.Replace("\"", "\"\"") + "*"))
            : string.Empty;
        var fromAndWhere = hasFilter
            ? "FROM songs_fts f JOIN songs s ON s.id=f.rowid WHERE songs_fts MATCH $filter AND s.media_kind=$kind"
            : "FROM songs s WHERE s.media_kind=$kind";

        long total;
        await using (var count = connection.CreateCommand())
        {
            count.CommandText = $"SELECT COUNT(*) {fromAndWhere};";
            count.Parameters.AddWithValue("$kind", mediaKind);
            if (hasFilter) count.Parameters.AddWithValue("$filter", fts);
            total = Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken));
        }

        if (total == 0) return new LibraryBrowsePage(Array.Empty<SongRecord>(), 0, 0, pageSize);
        if (offset >= total)
            offset = (int)Math.Max(0, ((total - 1) / pageSize) * pageSize);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
SELECT s.id, s.artist, s.title, s.manufacturer, s.disc_id, s.file_path, s.format,
       s.file_size, s.date_added, s.cdg_sync_seconds, s.preferred_key, s.media_kind
{fromAndWhere}
ORDER BY {orderBy}
LIMIT $limit OFFSET $offset;
""";
        command.Parameters.AddWithValue("$kind", mediaKind);
        if (hasFilter) command.Parameters.AddWithValue("$filter", fts);
        command.Parameters.AddWithValue("$limit", pageSize);
        command.Parameters.AddWithValue("$offset", offset);

        var items = new List<SongRecord>(pageSize);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(ReadSong(reader));
        return new LibraryBrowsePage(items, total, offset, pageSize);
    }

    public async Task<IReadOnlyList<SongRecord>> GetRandomCandidatesAsync(
        string mediaKind,
        int limit = 64,
        CancellationToken cancellationToken = default)
    {
        mediaKind = string.Equals(mediaKind, "Music", StringComparison.OrdinalIgnoreCase) ? "Music" : "Karaoke";
        limit = Math.Clamp(limit, 1, 256);

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        long minId;
        long maxId;
        await using (var bounds = connection.CreateCommand())
        {
            // This uses the media-kind/id index and avoids OFFSET, RANDOM(), and alphabetic sorting.
            // Those approaches become increasingly expensive once the library reaches millions of rows.
            bounds.CommandText = "SELECT MIN(id), MAX(id) FROM songs WHERE media_kind=$kind;";
            bounds.Parameters.AddWithValue("$kind", mediaKind);
            await using var reader = await bounds.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.IsDBNull(1))
                return Array.Empty<SongRecord>();
            minId = reader.GetInt64(0);
            maxId = reader.GetInt64(1);
        }

        var span = Math.Max(0L, maxId - minId);
        var startId = span == 0 ? minId : minId + Random.Shared.NextInt64(span + 1);
        var result = new List<SongRecord>(limit);

        async Task ReadRangeAsync(string comparison, string direction, long pivot, int take)
        {
            if (take <= 0) return;
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
SELECT id, artist, title, manufacturer, disc_id, file_path, format,
       file_size, date_added, cdg_sync_seconds, preferred_key, media_kind
FROM songs
WHERE media_kind=$kind AND id {comparison} $pivot
ORDER BY id {direction}
LIMIT $limit;
""";
            command.Parameters.AddWithValue("$kind", mediaKind);
            command.Parameters.AddWithValue("$pivot", pivot);
            command.Parameters.AddWithValue("$limit", take);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) result.Add(ReadSong(reader));
        }

        // Read forward from a random primary-key position, then wrap around if necessary.
        await ReadRangeAsync(">=", "ASC", startId, limit);
        if (result.Count < limit)
            await ReadRangeAsync("<", "ASC", startId, limit - result.Count);

        // Shuffle only this tiny in-memory candidate set; never sort the full SQLite library randomly.
        for (var i = result.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }

    public async Task<(long Karaoke, long Music)> GetLibraryCountsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT media_kind, COUNT(*) FROM songs GROUP BY media_kind";
        long karaoke = 0, music = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var kind = reader.GetString(0);
            var count = reader.GetInt64(1);
            if (string.Equals(kind, "Karaoke", StringComparison.OrdinalIgnoreCase)) karaoke += count;
            else if (string.Equals(kind, "Music", StringComparison.OrdinalIgnoreCase)) music += count;
        }
        return (karaoke, music);
    }

    private static SongRecord ReadSong(SqliteDataReader reader)
    {
        DateTimeOffset? added = null;
        if (!reader.IsDBNull(8) && DateTimeOffset.TryParse(reader.GetString(8), out var dt)) added = dt;
        return new SongRecord(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt64(7), added,
            reader.GetDouble(9), reader.GetInt32(10), reader.GetString(11));
    }

    private static bool PathEquals(string a, string b)
    {
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static bool IsLooseTitleMatch(string a, string b)
    {
        if (a == b) return true;
        if (a.Length == 0 || b.Length == 0) return false;
        if (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal))
            return Math.Abs(a.Length - b.Length) <= 4;
        var allowance = Math.Max(2, (int)Math.Ceiling(Math.Max(a.Length, b.Length) * 0.08));
        return LevenshteinWithin(a, b, allowance);
    }

    private static bool LevenshteinWithin(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max) return false;
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        var current = new int[b.Length + 1];
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                rowMin = Math.Min(rowMin, current[j]);
            }
            if (rowMin > max) return false;
            (previous, current) = (current, previous);
        }
        return previous[b.Length] <= max;
    }
}
