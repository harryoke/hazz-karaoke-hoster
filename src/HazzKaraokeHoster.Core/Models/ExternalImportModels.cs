namespace HazzKaraokeHoster.Core.Models;

public enum ExternalMediaKindMode
{
    Auto,
    Karaoke,
    Music
}

public sealed record ExternalImportPreview(
    string DetectedSource,
    string SourcePath,
    long EstimatedRecords,
    int PlaylistsDetected,
    IReadOnlyList<ExternalImportPreviewRow> SampleRows,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> DetectedObjects);

public sealed record ExternalImportPreviewRow(
    string Artist,
    string Title,
    string FilePath,
    string SuggestedMediaKind,
    string? Manufacturer = null,
    string? DiscId = null);

public sealed record ExternalImportProgress(
    long RowsRead,
    long RecordsImported,
    long PlaylistsImported,
    long MissingFiles,
    long Errors,
    string CurrentPath);

public sealed record ExternalWatchRoot(string Path, string MediaKind);

public sealed record ExternalImportResult(
    string DetectedSource,
    long RowsRead,
    long RecordsImported,
    long KaraokeImported,
    long MusicImported,
    long PlaylistsImported,
    long PlaylistItemsImported,
    int VirtualFoldersImported,
    long VirtualFolderTrackLinksImported,
    long MissingFiles,
    long Errors,
    IReadOnlyList<ExternalWatchRoot> WatchRoots,
    IReadOnlyList<string> Warnings);
