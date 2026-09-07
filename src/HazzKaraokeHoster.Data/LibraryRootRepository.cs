using HazzKaraokeHoster.Core.Models;
using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

public sealed class LibraryRootRepository(HazzDatabase database)
{
    public async Task UpsertRootsAsync(IEnumerable<string> paths, string mediaKind, bool includeSubfolders = true, CancellationToken cancellationToken = default)
    {
        await database.InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO library_roots(path,media_kind,include_subfolders)
VALUES($path,$kind,$sub)
ON CONFLICT(path,media_kind) DO UPDATE SET include_subfolders=excluded.include_subfolders;
""";
        var pPath = command.Parameters.Add("$path", SqliteType.Text);
        var pKind = command.Parameters.Add("$kind", SqliteType.Text);
        var pSub = command.Parameters.Add("$sub", SqliteType.Integer);
        foreach (var raw in paths.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path;
            try { path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw)); }
            catch { continue; }
            pPath.Value = path;
            pKind.Value = string.Equals(mediaKind, "Music", StringComparison.OrdinalIgnoreCase) ? "Music"
                : string.Equals(mediaKind, "Auto", StringComparison.OrdinalIgnoreCase) ? "Auto" : "Karaoke";
            pSub.Value = includeSubfolders ? 1 : 0;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LibraryRootRecord>> GetRootsAsync(CancellationToken cancellationToken = default)
    {
        await database.InitializeAsync(cancellationToken);
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,path,media_kind,include_subfolders FROM library_roots ORDER BY media_kind,path";
        var result = new List<LibraryRootRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new LibraryRootRecord(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3) != 0));
        return result;
    }
}
