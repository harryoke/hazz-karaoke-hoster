namespace HazzKaraokeHoster.Core.Models;

public sealed class AudienceSingerDisplayItem
{
    public int Position { get; init; }
    public string SingerName { get; init; } = string.Empty;
    public string SongText { get; init; } = string.Empty;
}
