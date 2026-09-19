using System.IO.Compression;

namespace HazzKaraokeHoster.Playback;

public static class KamikazeFolderPicker
{
    public static Task<string?> PickAsync(string folder, string? previous, CancellationToken token = default)
        => Task.Run(() => Pick(folder, previous, token), token);

    private static string? Pick(string folder, string? previous, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var files = Directory.EnumerateFiles(Path.GetFullPath(folder), "*", SearchOption.TopDirectoryOnly).ToArray();
        var audioStems = files.Where(p => MediaFileClassifier.Classify(p) is HazzMediaKind.Audio or HazzMediaKind.Mp3Audio)
            .Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cdgStems = files.Where(p => MediaFileClassifier.Classify(p) == HazzMediaKind.CdgGraphics)
            .Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var choices = new List<string>();
        foreach (var path in files)
        {
            token.ThrowIfCancellationRequested();
            var stem = Path.GetFileNameWithoutExtension(path);
            var kind = MediaFileClassifier.Classify(path);
            var eligible = kind switch
            {
                HazzMediaKind.CdgGraphics => audioStems.Contains(stem),
                HazzMediaKind.Audio or HazzMediaKind.Mp3Audio => !cdgStems.Contains(stem),
                HazzMediaKind.Video => true,
                HazzMediaKind.ZipKaraoke => HasAudio(path),
                _ => false
            };
            if (eligible) choices.Add(path);
        }
        if (choices.Count > 1 && !string.IsNullOrWhiteSpace(previous))
            choices.RemoveAll(p => string.Equals(p, previous, StringComparison.OrdinalIgnoreCase));
        return choices.Count == 0 ? null : choices[Random.Shared.Next(choices.Count)];
    }

    private static bool HasAudio(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            return zip.Entries.Any(e => !string.IsNullOrEmpty(e.Name) &&
                MediaFileClassifier.Classify(e.Name) is HazzMediaKind.Audio or HazzMediaKind.Mp3Audio);
        }
        catch (InvalidDataException) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
