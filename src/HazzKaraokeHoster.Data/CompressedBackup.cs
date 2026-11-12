using System.IO.Compression;
using System.Text.RegularExpressions;

namespace HazzKaraokeHoster.Data;

public static class CompressedBackup
{
    private static readonly SemaphoreSlim CreationGate = new(1, 1);

    public static Task CreateAsync(HazzDatabase database, string folder, CancellationToken token = default) => Task.Run(async () =>
    {
        // Startup/manual requests can overlap. Serialize publication and pruning so
        // one request cannot remove another request's working files or completed ZIP.
        await CreationGate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, "hazz-" + DateTime.Now.ToString("yyyy-MM-dd") + ".zip");
            if (!File.Exists(target))
            {
                // A crashed process may leave its work behind; never reuse those files.
                var workingPath = target + "." + Guid.NewGuid().ToString("N");
                var temporary = workingPath + ".pending";
                var snapshot = workingPath + ".snapshot";
                try
                {
                    await database.BackupAsync(snapshot, token);
                    using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
                    {
                        zip.CreateEntryFromFile(snapshot, "hazz-hoster.db", CompressionLevel.Optimal);
                        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(Path.GetFullPath(database.DatabasePath))!, "*.json"))
                        {
                            token.ThrowIfCancellationRequested();
                            zip.CreateEntryFromFile(file, "settings/" + Path.GetFileName(file), CompressionLevel.Optimal);
                        }
                    }
                    // Read the completed archive before publishing it or retiring older backups.
                    using (var zip = ZipFile.OpenRead(temporary))
                        foreach (var entry in zip.Entries) { token.ThrowIfCancellationRequested(); using var stream = entry.Open(); stream.CopyTo(Stream.Null); }
                    token.ThrowIfCancellationRequested();
                    File.Move(temporary, target);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                    if (File.Exists(snapshot)) File.Delete(snapshot);
                }
            }
            token.ThrowIfCancellationRequested();
            Prune(folder);
        }
        finally
        {
            CreationGate.Release();
        }
    }, token);

    public static void Prune(string folder, long budget = 1024L * 1024 * 1024)
    {
        // Exact generated names only; leave manual backups and legacy .db files alone.
        var files = Directory.GetFiles(folder, "*.zip")
            .Where(x => Regex.IsMatch(Path.GetFileName(x), @"^hazz-\d{4}-\d{2}-\d{2}\.zip$"))
            .OrderByDescending(x => Path.GetFileName(x), StringComparer.Ordinal).ToArray();
        long used = 0;
        for (var i = 0; i < files.Length; i++)
        {
            var size = new FileInfo(files[i]).Length;
            if (i == 0 || (i < 3 && used + size <= budget)) used += size;
            else File.Delete(files[i]);
        }
    }
}
