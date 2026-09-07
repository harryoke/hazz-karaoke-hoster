namespace HazzKaraokeHoster.Core.Models;

public sealed record RecentSingerPerformance(
    long SingerId,
    string SingerName,
    DateTimeOffset SungAt,
    long? SongId,
    string Artist,
    string Title);
