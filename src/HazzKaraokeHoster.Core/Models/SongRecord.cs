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
    string MediaKind = "Karaoke");
