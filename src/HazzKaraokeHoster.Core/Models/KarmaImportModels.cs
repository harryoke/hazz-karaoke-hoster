namespace HazzKaraokeHoster.Core.Models;

public sealed record KarmaImportResult(
    int SingersImported,
    int HistoryRowsImported,
    int SongsMatched,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> SourceTables);

public sealed record KarmaImportPreview(
    string SourceType,
    string SourcePath,
    IReadOnlyList<string> TablesOrFiles,
    IReadOnlyList<string> Warnings);
