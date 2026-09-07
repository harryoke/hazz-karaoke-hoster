using System.IO.Compression;

namespace HazzKaraokeHoster.Playback;

public static class KaraokePackageLoader
{
    private static readonly string[] AudioExtensions = [".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".aif", ".aiff"];

    public static Task<KaraokePackage> LoadAsync(string sourcePath, CancellationToken cancellationToken = default)
        => Task.Run(() => Load(sourcePath, cancellationToken), cancellationToken);

    private static KaraokePackage Load(string sourcePath, CancellationToken cancellationToken)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Karaoke file not found.", sourcePath);

        var kind = MediaFileClassifier.Classify(sourcePath);
        return kind switch
        {
            HazzMediaKind.ZipKaraoke => LoadZip(sourcePath, cancellationToken),
            HazzMediaKind.CdgGraphics => LoadCdgFile(sourcePath),
            HazzMediaKind.Mp3Audio or HazzMediaKind.Audio => LoadAudio(sourcePath),
            HazzMediaKind.Video => new KaraokePackage
            {
                Kind = KaraokePackageKind.Video,
                SourcePath = sourcePath,
                PlaybackPath = sourcePath
            },
            _ => throw new NotSupportedException($"Unsupported karaoke file type: {Path.GetExtension(sourcePath)}")
        };
    }

    private static KaraokePackage LoadCdgFile(string cdgPath)
    {
        var audio = FindSiblingAudio(cdgPath)
            ?? throw new FileNotFoundException("The CD+G graphics file was found, but no matching audio file with the same filename was found.", cdgPath);
        return new KaraokePackage
        {
            Kind = KaraokePackageKind.CdgPair,
            SourcePath = cdgPath,
            PlaybackPath = audio,
            CdgPath = cdgPath
        };
    }

    private static KaraokePackage LoadAudio(string audioPath)
    {
        var siblingCdg = Path.ChangeExtension(audioPath, ".cdg");
        if (File.Exists(siblingCdg))
        {
            return new KaraokePackage
            {
                Kind = KaraokePackageKind.CdgPair,
                SourcePath = audioPath,
                PlaybackPath = audioPath,
                CdgPath = siblingCdg
            };
        }

        return new KaraokePackage
        {
            Kind = KaraokePackageKind.AudioOnly,
            SourcePath = audioPath,
            PlaybackPath = audioPath
        };
    }

    private static KaraokePackage LoadZip(string zipPath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var files = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToArray();
        var cdgEntries = files.Where(e => Path.GetExtension(e.Name).Equals(".cdg", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (cdgEntries.Length == 0) throw new InvalidDataException("This ZIP does not contain a .cdg graphics file.");

        var selectedCdg = cdgEntries[0];
        var cdgStem = Path.GetFileNameWithoutExtension(selectedCdg.Name);
        var cdgFolder = GetZipFolder(selectedCdg.FullName);

        var audioEntries = files.Where(e => AudioExtensions.Contains(Path.GetExtension(e.Name), StringComparer.OrdinalIgnoreCase)).ToArray();
        var selectedAudio = audioEntries.FirstOrDefault(e =>
                GetZipFolder(e.FullName).Equals(cdgFolder, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileNameWithoutExtension(e.Name).Equals(cdgStem, StringComparison.OrdinalIgnoreCase))
            ?? audioEntries.FirstOrDefault(e => Path.GetFileNameWithoutExtension(e.Name).Equals(cdgStem, StringComparison.OrdinalIgnoreCase))
            ?? audioEntries.FirstOrDefault();

        if (selectedAudio is null) throw new InvalidDataException("This ZIP contains CD+G graphics but no supported audio file.");

        cancellationToken.ThrowIfCancellationRequested();
        var tempRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Hazz Karaoke Hoster", "Temp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var cdgOut = Path.Combine(tempRoot, "track.cdg");
            var audioOut = Path.Combine(tempRoot, "track" + Path.GetExtension(selectedAudio.Name).ToLowerInvariant());
            ExtractEntry(selectedCdg, cdgOut, cancellationToken);
            ExtractEntry(selectedAudio, audioOut, cancellationToken);

            return new KaraokePackage
            {
                Kind = KaraokePackageKind.CdgPair,
                SourcePath = zipPath,
                PlaybackPath = audioOut,
                CdgPath = cdgOut,
                TemporaryDirectory = tempRoot
            };
        }
        catch
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
            throw;
        }
    }

    private static void ExtractEntry(ZipArchiveEntry entry, string destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var input = entry.Open();
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.SequentialScan);
        var buffer = new byte[128 * 1024];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
        }
    }

    private static string? FindSiblingAudio(string cdgPath)
    {
        var basePath = Path.Combine(Path.GetDirectoryName(cdgPath) ?? string.Empty, Path.GetFileNameWithoutExtension(cdgPath));
        foreach (var ext in AudioExtensions)
        {
            var candidate = basePath + ext;
            if (File.Exists(candidate)) return candidate;
            var upperCandidate = basePath + ext.ToUpperInvariant();
            if (File.Exists(upperCandidate)) return upperCandidate;
        }
        return null;
    }

    private static string GetZipFolder(string fullName)
    {
        var normalized = fullName.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? string.Empty : normalized[..slash];
    }
}
