namespace HazzKaraokeHoster.Playback;

public enum KaraokePackageKind
{
    AudioOnly,
    CdgPair,
    Video
}

/// <summary>
/// Resolved karaoke media. ZIP packages are extracted to a private temporary
/// directory which is deleted when the package is disposed.
/// </summary>
public sealed class KaraokePackage : IDisposable
{
    public required KaraokePackageKind Kind { get; init; }
    public required string SourcePath { get; init; }
    public required string PlaybackPath { get; init; }
    public string? CdgPath { get; init; }
    public string? TemporaryDirectory { get; init; }
    public string DisplayTitle => Path.GetFileNameWithoutExtension(SourcePath);

    public void Dispose()
    {
        if (string.IsNullOrWhiteSpace(TemporaryDirectory) || !Directory.Exists(TemporaryDirectory)) return;
        try { Directory.Delete(TemporaryDirectory, recursive: true); }
        catch { /* Cleanup is best effort; stale temp folders are harmless and can be swept next launch. */ }
    }
}
