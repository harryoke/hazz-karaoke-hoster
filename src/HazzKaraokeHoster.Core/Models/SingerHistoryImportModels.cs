namespace HazzKaraokeHoster.Core.Models;

public sealed record SingerHistoryImportPreviewRow(string Singer, string Artist, string Title, string FilePath, string SungAt, int KeyChange, int TimesSung);

public sealed record SingerHistoryImportPreview(
    string DetectedSource,
    string SourcePath,
    string DetectedMapping,
    IReadOnlyList<SingerHistoryImportPreviewRow> SampleRows,
    bool HasMoreRows,
    IReadOnlyList<string> Warnings);

public sealed record SingerHistoryImportProgress(long RowsRead, long Imported, long MatchedSongs, long UnmatchedSongs, long Errors, string CurrentSinger);

public sealed record SingerHistoryImportResult(
    string DetectedSource,
    long RowsRead,
    long SingersImported,
    long HistoryRowsImported,
    long MatchedSongs,
    long UnmatchedSongs,
    long Errors,
    IReadOnlyList<string> Warnings);
