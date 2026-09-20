namespace HazzKaraokeHoster.Core.Models;

public sealed record CdgPresentationSettings
{
    public bool Enabled { get; set; }
    // -1 follows the CD+G memory-preset colour; 0–15 selects a palette entry.
    public int BackgroundColour { get; set; } = -1;
    public double BackgroundOpacity { get; set; } = 0.6;
    public double LyricsOpacity { get; set; } = 1;
}
