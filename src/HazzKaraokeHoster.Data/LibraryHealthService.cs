using System.IO.Compression;
using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

public sealed record LibraryHealthIssue(long SongId, string Artist, string Title, string Path, string Problem, string Detail, long Size);
public sealed record LibraryRelink(long SongId, string OldPath, string NewPath);

public sealed class LibraryHealthService(HazzDatabase database)
{
    private static readonly string[] Audio = [".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".aif", ".aiff"];
    public async Task<List<LibraryHealthIssue>> ScanAsync(IProgress<string>? progress, CancellationToken token)
    {
        var issues = new List<LibraryHealthIssue>();
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(token);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id,artist,title,file_path,file_size,media_kind FROM songs ORDER BY id";
        await using var reader = await cmd.ExecuteReaderAsync(token);
        int count = 0;
        while (await reader.ReadAsync(token))
        {
            token.ThrowIfCancellationRequested();
            var id = reader.GetInt64(0); var artist = reader.GetString(1); var title = reader.GetString(2); var path = reader.GetString(3); var size = reader.GetInt64(4);
            void Add(string problem, string detail) => issues.Add(new(id, artist, title, path, problem, detail, size));
            if (!paths.Add(path.Replace('/', '\\'))) Add("Duplicate path", "Another database entry points to the same path (ignoring letter case).");
            var key = string.Join(' ', HazzKaraokeHoster.Core.Models.AlternativeMatch.Words(artist)) + "|" + string.Join(' ', HazzKaraokeHoster.Core.Models.AlternativeMatch.Words(title)) + "|" + reader.GetString(5);
            if (artist.Length > 0 && title.Length > 0)
            {
                if (seen.TryGetValue(key, out var previous)) Add("Possible duplicate", "Same artist/title; may be a different recording. Compare with: " + previous);
                else seen[key] = path;
            }
            if (!File.Exists(path)) Add("Missing file", "File is missing, inaccessible or its drive is disconnected.");
            else
            {
                try
                {
                    var problem = InspectFile(path, reader.GetString(5), token);
                    if (problem is not null) Add(problem.Value.Kind, problem.Value.Detail);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
                { Add("Unreadable / broken file", ex.Message); }
            }
            if (++count % 50 == 0) progress?.Report($"Checked {count:N0} entries; {issues.Count:N0} findings. {System.IO.Path.GetFileName(path)}");
        }
        progress?.Report($"Scan complete: {count:N0} entries checked; {issues.Count:N0} findings.");
        return issues;
    }

    public static (string Kind, string Detail)? InspectFile(string path, string kind, CancellationToken token)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (new FileInfo(path).Length == 0) return ("Empty file", "File contains no data.");
        if (ext == ".zip")
        {
            using var zip = ZipFile.OpenRead(path);
            if (zip.Entries.Count > 10000 || zip.Entries.Sum(e => (double)e.Length) > 2L * 1024 * 1024 * 1024)
                return ("ZIP not checked", "Archive exceeds the 2 GB / 10,000 entry safety limit; inspect manually.");
            var names = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).Select(e => e.FullName).ToArray();
            var cdgs = names.Where(n => System.IO.Path.GetExtension(n).Equals(".cdg", StringComparison.OrdinalIgnoreCase)).ToArray();
            var audio = names.Where(n => Audio.Contains(System.IO.Path.GetExtension(n), StringComparer.OrdinalIgnoreCase)).ToArray();
            var buffer = new byte[65536];
            foreach (var entry in zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)))
            {
                token.ThrowIfCancellationRequested();
                using var stream = entry.Open(); long bytes = 0; uint crc = uint.MaxValue; int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested(); bytes += read;
                    if (bytes > entry.Length) throw new InvalidDataException("ZIP entry exceeds its declared size.");
                    for (var i = 0; i < read; i++) crc = CrcTable[(crc ^ buffer[i]) & 255] ^ (crc >> 8);
                }
                if (bytes != entry.Length || ~crc != entry.Crc32) throw new InvalidDataException("ZIP data/checksum is damaged: " + entry.FullName);
            }
            if (kind.Equals("Karaoke", StringComparison.OrdinalIgnoreCase) && (cdgs.Length == 0 || audio.Length == 0))
                return ("Missing ZIP partner", "Archive needs CDG graphics and a supported audio file.");
            if (cdgs.Any(c => !audio.Any(a => System.IO.Path.GetFileNameWithoutExtension(a).Equals(System.IO.Path.GetFileNameWithoutExtension(c), StringComparison.OrdinalIgnoreCase))))
                return ("ZIP partner name mismatch", "CDG and audio names do not match; verify the intended pair.");
        }
        else if (ext == ".cdg" && !Audio.Any(a => File.Exists(System.IO.Path.ChangeExtension(path, a))))
            return ("Missing audio partner", "No same-name audio file beside this CDG.");
        else if (kind.Equals("Karaoke", StringComparison.OrdinalIgnoreCase) && Audio.Contains(ext) && !File.Exists(System.IO.Path.ChangeExtension(path, ".cdg")))
            return ("Missing CDG partner", "No same-name CDG. An intentional audio-only backing track can be left unchanged.");
        return null;
    }
    private static readonly uint[] CrcTable = Enumerable.Range(0,256).Select(n => { uint c=(uint)n; for(int i=0;i<8;i++) c=(c&1)!=0 ? 0xedb88320U^(c>>1):c>>1; return c; }).ToArray();

    public static string? MappedPath(string path, string oldRoot, string newRoot)
    {
        if (string.IsNullOrWhiteSpace(oldRoot) || string.IsNullOrWhiteSpace(newRoot) || !System.IO.Path.IsPathFullyQualified(oldRoot) || !System.IO.Path.IsPathFullyQualified(newRoot)) throw new ArgumentException("Enter complete old and new folder paths, such as E:\\Karaoke and F:\\Karaoke.");
        oldRoot = System.IO.Path.GetFullPath(oldRoot).TrimEnd('\\','/') + System.IO.Path.DirectorySeparatorChar;
        newRoot = System.IO.Path.GetFullPath(newRoot).TrimEnd('\\','/') + System.IO.Path.DirectorySeparatorChar;
        var full = System.IO.Path.GetFullPath(path);
        if (!full.StartsWith(oldRoot, StringComparison.OrdinalIgnoreCase)) return null;
        var mapped = System.IO.Path.GetFullPath(System.IO.Path.Combine(newRoot, full[oldRoot.Length..]));
        return mapped.StartsWith(newRoot, StringComparison.OrdinalIgnoreCase) ? mapped : null;
    }

    public async Task ApplyRelinksAsync(IReadOnlyList<LibraryRelink> repairs, CancellationToken token)
    {
        // Recheck all candidates before any write. One transaction: no half-repaired batch.
        foreach (var r in repairs)
        {
            token.ThrowIfCancellationRequested();
            if (File.Exists(r.OldPath) || !File.Exists(r.NewPath)) throw new IOException("Locations changed since preview. Scan again: " + r.OldPath);
            if (!System.IO.Path.GetExtension(r.OldPath).Equals(System.IO.Path.GetExtension(r.NewPath), StringComparison.OrdinalIgnoreCase)) throw new IOException("Replacement format differs.");
        }
        await using var connection = new SqliteConnection(database.ConnectionString); await connection.OpenAsync(token);
        using var transaction = connection.BeginTransaction();
        foreach (var table in new[] { "singer_history", "music_history", "music_playlist_items" })
        {
            await using var index = connection.CreateCommand(); index.Transaction = transaction;
            index.CommandText = $"CREATE INDEX IF NOT EXISTS ix_{table}_relink_path ON {table}(file_path)";
            await index.ExecuteNonQueryAsync(token);
        }
        foreach (var r in repairs)
        {
            token.ThrowIfCancellationRequested();
            await using var cmd = connection.CreateCommand(); cmd.Transaction = transaction;
            cmd.CommandText = "UPDATE songs SET file_path=$new WHERE id=$id AND file_path=$old";
            cmd.Parameters.AddWithValue("$new", r.NewPath); cmd.Parameters.AddWithValue("$old", r.OldPath); cmd.Parameters.AddWithValue("$id", r.SongId);
            if (await cmd.ExecuteNonQueryAsync(token) != 1) throw new IOException("Library changed since preview; scan again.");
            foreach (var table in new[] { "singer_history", "music_history", "music_playlist_items" })
            { cmd.CommandText = $"UPDATE {table} SET file_path=$new WHERE file_path=$old"; await cmd.ExecuteNonQueryAsync(token); }
        }
        transaction.Commit();
    }
}
