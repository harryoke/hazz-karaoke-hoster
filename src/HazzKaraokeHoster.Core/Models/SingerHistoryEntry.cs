namespace HazzKaraokeHoster.Core.Models;

public sealed record SingerHistoryEntry(
    long Id,
    long SingerId,
    long? SongId,
    string Artist,
    string Title,
    string FilePath,
    DateTimeOffset SungAt,
    int KeyChange,
    double CdgSyncSeconds,
    int TimesSung = 1);
