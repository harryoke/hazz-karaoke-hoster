namespace HazzKaraokeHoster.Playback;

public static class PlaybackBufferSettings
{
    // Request more headroom for decoding, DSP and short scheduling stalls.
    // Shared WASAPI can negotiate a different actual device buffer.
    public const int LatencyMilliseconds = 240;
}
