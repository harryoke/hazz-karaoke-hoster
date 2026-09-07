namespace HazzKaraokeHoster.Core.Models;

public enum LibraryImportMode
{
    Karaoke,
    Music,
    Auto
}

public sealed record LibraryImportOptions(
    IReadOnlyList<string> RootPaths,
    LibraryImportMode Mode,
    bool IncludeSubfolders = true)
{
    public LibraryImportOptions(string rootPath, LibraryImportMode mode, bool includeSubfolders = true)
        : this(new[] { rootPath }, mode, includeSubfolders) { }
}

public sealed record LibraryImportProgress(
    long FilesScanned,
    long RecordsImported,
    long Skipped,
    string CurrentPath,
    long CompanionAudioIgnored = 0,
    long Unsupported = 0,
    long Errors = 0,
    int CurrentRoot = 0,
    int RootCount = 0);

public sealed record LibraryImportResult(
    long FilesScanned,
    long RecordsImported,
    long Skipped,
    long Errors,
    TimeSpan Elapsed,
    IReadOnlyList<string> Warnings,
    long CompanionAudioIgnored = 0,
    long Unsupported = 0,
    int RootsProcessed = 0);
