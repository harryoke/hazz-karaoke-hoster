namespace HazzKaraokeHoster.Core.Models;

public sealed record BpmStudioImportPreview(
    string SourcePath,
    int PlaylistFiles,
    int HistoryFiles,
    int ArchiveGroupFiles,
    IReadOnlyList<string> SampleFiles);

public sealed record BpmStudioImportProgress(
    string Phase,
    int FilesProcessed,
    int TotalFiles,
    string CurrentFile,
    long ItemsProcessed,
    long MusicTracksLinkedOrIndexed,
    int UnsupportedFiles,
    double? PercentOverride = null)
{
    public double Percent => PercentOverride is double explicitPercent
        ? Math.Clamp(explicitPercent, 0, 100)
        : (TotalFiles <= 0 ? 0 : Math.Clamp(FilesProcessed * 100.0 / TotalFiles, 0, 100));
}

public sealed record BpmStudioImportResult(
    int PlaylistsImported,
    int HistoryListsImported,
    long PlaylistItemsImported,
    long HistoryItemsImported,
    long MusicTracksLinkedOrIndexed,
    int UnsupportedFiles,
    IReadOnlyList<string> Warnings);

public sealed record MusicPlaylistSummary(long Id, string Name, string SourceType, int TrackCount, DateTimeOffset? ImportedUtc);

public sealed record MusicPlaylistItem(
    long Id,
    long PlaylistId,
    int Position,
    long? SongId,
    string FilePath,
    string Artist,
    string Title);

public sealed record MusicPlaylistSaveItem(
    long? SongId,
    string FilePath,
    string Artist,
    string Title);

public sealed record MusicHistoryEntry(
    long Id,
    string SourceList,
    int Position,
    long? SongId,
    string FilePath,
    string Artist,
    string Title,
    DateTimeOffset? PlayedAtUtc,
    double? DurationSeconds)
{
    public DateTimeOffset? PlayedAtLocal => PlayedAtUtc?.ToLocalTime();
    public string DurationText => DurationSeconds is double seconds && seconds > 0
        ? (seconds >= 3600
            ? $"{(int)(seconds / 3600)}:{(int)(seconds / 60) % 60:00}:{(int)seconds % 60:00}"
            : $"{(int)(seconds / 60):00}:{(int)seconds % 60:00}")
        : "--:--";
}
