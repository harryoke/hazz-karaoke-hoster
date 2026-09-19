namespace HazzKaraokeHoster.Playback;

public sealed class CdgTimingController
{
    public const double StepSeconds = 0.25;
    public const double MinSeconds = -10.0;
    public const double MaxSeconds = 10.0;

    /// <summary>
    /// Offset added to the CD+G clock. Positive values advance the graphics;
    /// negative values delay them.
    /// </summary>
    public double OffsetSeconds { get; private set; }
    public event EventHandler<double>? OffsetChanged;

    public void Earlier() => Set(OffsetSeconds - StepSeconds);
    public void Later() => Set(OffsetSeconds + StepSeconds);
    public void Reset() => Set(0);

    public void Set(double seconds)
    {
        var snapped = Math.Round(seconds / StepSeconds, MidpointRounding.AwayFromZero) * StepSeconds;
        snapped = Math.Clamp(snapped, MinSeconds, MaxSeconds);
        if (Math.Abs(snapped - OffsetSeconds) < 0.0001) return;
        OffsetSeconds = snapped;
        OffsetChanged?.Invoke(this, OffsetSeconds);
    }

    /// <summary>
    /// Returns the CD+G stream time corresponding to the current audio time.
    /// A +0.25s offset advances the graphics clock by 0.25s.
    /// </summary>
    public TimeSpan GetGraphicsTime(TimeSpan audioTime)
        => audioTime + TimeSpan.FromSeconds(OffsetSeconds);
}
