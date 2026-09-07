namespace HazzKaraokeHoster.Playback;

public enum DeckPlaybackState { Empty, Ready, Playing, Paused, Stopped, Faulted }

public sealed class DeckState
{
    public string? FilePath { get; set; }
    public string DisplayTitle { get; set; } = "No track loaded";
    public DeckPlaybackState State { get; set; } = DeckPlaybackState.Empty;
    public double Volume { get; set; } = 0.85;
    public int KeyChange { get; set; }
}
