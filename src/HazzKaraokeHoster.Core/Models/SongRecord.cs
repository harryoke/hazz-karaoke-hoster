namespace HazzKaraokeHoster.Core.Models;

public sealed record SongRecord(
    long Id,
    string Artist,
    string Title,
    string Manufacturer,
    string DiscId,
    string FilePath,
    string Format,
    long FileSize,
    DateTimeOffset? DateAdded,
    double CdgSyncSeconds = 0,
    int PreferredKey = 0,
    string MediaKind = "Karaoke",
    double? DurationSeconds = null)
{
    public string DurationText => DurationSeconds is double seconds && seconds > 0
        ? TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss")
        : string.Empty;
}
