namespace HazzKaraokeHoster.Playback;

// The decoder clock leads audible playback by the queued audio and DSP look-ahead.
// Allow that lead and reject brief or alternating clock spikes before seeking.
public sealed class KaraokeDriftFilter
{
    private int _count;
    private int _direction;

    public void Reset() { _count = 0; _direction = 0; }

    public bool NeedsCorrection(double driftMilliseconds)
    {
        var direction = driftMilliseconds > 1000 ? 1 : driftMilliseconds < -650 ? -1 : 0;
        if (direction == 0 || direction != _direction) _count = 0;
        _direction = direction;
        if (direction == 0 || ++_count < 3) return false;
        Reset();
        return true;
    }
}
