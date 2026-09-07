namespace HazzKaraokeHoster.Core.Models;

// Legacy v0.4 single-song queue record. New UI uses SingerQueueEntry + SingerSongEntry.
public sealed class QueueEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public long? SingerId { get; init; }
    public string SingerName { get; set; } = string.Empty;
    public long? SongId { get; set; }
    public string SongTitle { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int KeyChange { get; set; }
    public double CdgSyncSeconds { get; set; }
    public override string ToString() => string.IsNullOrWhiteSpace(SongTitle) ? SingerName : $"{SingerName} — {SongTitle}";
}
