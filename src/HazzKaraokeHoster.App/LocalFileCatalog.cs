using HazzKaraokeHoster.Playback;

namespace HazzKaraokeHoster.App;

public sealed record LocalMediaFile(string Name, string FullPath, string Type);
public sealed record LocalFolderListing(LocalMediaFile[] Files, int MatchingCount, string? Error);

/// <summary>Read-only, nonrecursive filesystem browsing. No indexing, metadata decoding or playback.</summary>
public static class LocalFileCatalog
{
    public const int VisibleLimit = 5000;
    public const int FolderLimit = 2000;
    public const int DropLimit = 500;
    public static bool Supported(string? path, bool musicOnly = false)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var kind = MediaFileClassifier.Classify(path);
            return musicOnly ? kind is HazzMediaKind.Mp3Audio or HazzMediaKind.Audio or HazzMediaKind.Video : kind != HazzMediaKind.Unknown;
        }
        catch (ArgumentException) { return false; }
    }
    public static string[] Drives() => DriveInfo.GetDrives().Where(d => d.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.CDRom)
        .Select(d => d.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public static string[] Folders(string path, CancellationToken token)
    {
        var paths = new List<string>();
        foreach (string folder in Directory.EnumerateDirectories(path, "*", new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System }))
        { token.ThrowIfCancellationRequested(); paths.Add(folder); if (paths.Count >= FolderLimit) break; }
        return paths.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static LocalFolderListing Files(string path, string filter, CancellationToken token)
    {
        var files = new List<LocalMediaFile>(); int matched = 0;
        try
        {
            foreach (string file in Directory.EnumerateFiles(path))
            {
                token.ThrowIfCancellationRequested();
                if (!Supported(file)) continue;
                string name = Path.GetFileName(file);
                if (!name.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                matched++;
                if (files.Count >= VisibleLimit) continue;
                string type = MediaFileClassifier.Classify(file) switch
                { HazzMediaKind.CdgGraphics => "CD+G graphics", HazzMediaKind.ZipKaraoke => "Karaoke ZIP", HazzMediaKind.Video => "Video", _ => "Audio" };
                files.Add(new(name, file, type));
            }
            return new(files.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray(), matched, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new([], 0, ex is UnauthorizedAccessException ? "Access to this folder is denied." : "Folder unavailable: " + ex.Message); }
    }
    public static string[] ExistingMusicFiles(IEnumerable<string> paths, CancellationToken token)
    {
        var result = new List<string>();
        foreach (string path in paths)
        { token.ThrowIfCancellationRequested(); if (Supported(path, musicOnly: true) && File.Exists(path)) result.Add(Path.GetFullPath(path)); }
        return result.ToArray();
    }
    public static int SafeInsertIndex(int requested, int count, int currentIndex) => Math.Clamp(Math.Max(requested, currentIndex + 1), 0, count);
}
