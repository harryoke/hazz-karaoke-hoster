using System.IO.Compression;

namespace HazzKaraokeHoster.Core;

/// <summary>
/// Best-effort media duration lookup used by karaoke search and queue estimates.
/// ZIP karaoke is probed by extracting only its matching audio member to a private
/// temporary file. Failures never block search/playback; they simply return null.
/// </summary>
public static class MediaDurationProbe
{
    private static readonly SemaphoreSlim Readers = new(2);
    private static readonly string[] AudioExtensions = [".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".aif", ".aiff"];

    public static async Task<double?> TryReadSecondsAsync(string? sourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return null;
        await Readers.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => ReadSeconds(sourcePath, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
        finally { Readers.Release(); }
    }

    private static double? ReadSeconds(string sourcePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.GetFullPath(sourcePath);
        if (!File.Exists(path)) return null;
        var ext = Path.GetExtension(path);
        if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return ReadZipSeconds(path, cancellationToken);
        if (ext.Equals(".cdg", StringComparison.OrdinalIgnoreCase))
        {
            var audio = FindSiblingAudio(path);
            return audio is null ? null : ReadFileSeconds(audio);
        }
        return ReadFileSeconds(path);
    }

    private static double? ReadFileSeconds(string path)
    {
        try
        {
            using var file = TagLib.File.Create(path, TagLib.ReadStyle.Average);
            var seconds = file.Properties.Duration.TotalSeconds;
            return seconds > 0.5 && double.IsFinite(seconds) ? seconds : null;
        }
        catch { return null; }
    }

    private static double? ReadZipSeconds(string zipPath, CancellationToken cancellationToken)
    {
        string? temp = null;
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var files = archive.Entries.Where(e => !string.IsNullOrWhiteSpace(e.Name)).ToArray();
            var cdg = files.FirstOrDefault(e => Path.GetExtension(e.Name).Equals(".cdg", StringComparison.OrdinalIgnoreCase));
            var audio = SelectZipAudio(files, cdg);
            if (audio is null) return null;

            cancellationToken.ThrowIfCancellationRequested();
            var tempRoot = Path.Combine(Path.GetTempPath(), "HazzKaraokeDuration");
            Directory.CreateDirectory(tempRoot);
            temp = Path.Combine(tempRoot, Guid.NewGuid().ToString("N") + Path.GetExtension(audio.Name));
            using (var input = audio.Open())
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.SequentialScan))
            {
                var buffer = new byte[128 * 1024];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                }
            }
            return ReadFileSeconds(temp);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
        finally
        {
            try { if (!string.IsNullOrWhiteSpace(temp) && File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static ZipArchiveEntry? SelectZipAudio(ZipArchiveEntry[] files, ZipArchiveEntry? cdg)
    {
        var audio = files.Where(e => AudioExtensions.Contains(Path.GetExtension(e.Name), StringComparer.OrdinalIgnoreCase)).ToArray();
        if (audio.Length == 0) return null;
        if (cdg is null) return audio[0];
        var stem = Path.GetFileNameWithoutExtension(cdg.Name);
        var folder = ZipFolder(cdg.FullName);
        return audio.FirstOrDefault(e => ZipFolder(e.FullName).Equals(folder, StringComparison.OrdinalIgnoreCase)
                                         && Path.GetFileNameWithoutExtension(e.Name).Equals(stem, StringComparison.OrdinalIgnoreCase))
            ?? audio.FirstOrDefault(e => Path.GetFileNameWithoutExtension(e.Name).Equals(stem, StringComparison.OrdinalIgnoreCase))
            ?? audio[0];
    }

    private static string? FindSiblingAudio(string cdgPath)
    {
        var basePath = Path.Combine(Path.GetDirectoryName(cdgPath) ?? string.Empty, Path.GetFileNameWithoutExtension(cdgPath));
        foreach (var ext in AudioExtensions)
        {
            var candidate = basePath + ext;
            if (File.Exists(candidate)) return candidate;
            candidate = basePath + ext.ToUpperInvariant();
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string ZipFolder(string fullName)
    {
        var normalized = fullName.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? string.Empty : normalized[..slash];
    }
}
