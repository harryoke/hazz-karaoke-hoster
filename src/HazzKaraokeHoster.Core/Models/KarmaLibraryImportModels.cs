namespace HazzKaraokeHoster.Core.Models;

public sealed record KarmaLibraryColumnMapping(
    string TableName,
    string PathColumn,
    string? FolderColumn,
    string? ArtistColumn,
    string? TitleColumn,
    string? ManufacturerColumn,
    string? DiscIdColumn);

public sealed record KarmaLibraryPreviewRow(
    string Artist,
    string Title,
    string Manufacturer,
    string DiscId,
    string FilePath);

public sealed record KarmaLibraryInspection(
    string SourcePath,
    KarmaLibraryColumnMapping? SuggestedMapping,
    long EstimatedRows,
    IReadOnlyList<KarmaLibraryPreviewRow> PreviewRows,
    IReadOnlyList<string> TablesAndColumns,
    IReadOnlyList<string> Warnings);

public sealed record KarmaLibraryImportProgress(
    long RowsRead,
    long RecordsImported,
    long MissingFiles,
    long Errors,
    string CurrentPath);

public sealed record KarmaLibraryImportResult(
    long RowsRead,
    long RecordsImported,
    long MissingFiles,
    long Errors,
    IReadOnlyList<string> WatchRootsAdded,
    IReadOnlyList<string> Warnings);

public sealed record LibraryRootRecord(
    long Id,
    string Path,
    string MediaKind,
    bool IncludeSubfolders);

public enum SingleFileIndexOutcome
{
    Indexed,
    CompanionAudioIgnored,
    Unsupported,
    Missing,
    Error
}

public sealed record SingleFileIndexResult(
    SingleFileIndexOutcome Outcome,
    string Path,
    string? Message = null);
