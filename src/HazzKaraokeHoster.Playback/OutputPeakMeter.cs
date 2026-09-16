using NAudio.Wave;

namespace HazzKaraokeHoster.Playback;

/// <summary>Observes samples without changing them; the UI consumes a peak snapshot.</summary>
public sealed class OutputPeakMeter(ISampleProvider source) : ISampleProvider
{
    private float _peak;
    public WaveFormat WaveFormat => source.WaveFormat;
    public float ConsumePeak() => Interlocked.Exchange(ref _peak, 0);
    public int Read(float[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        float peak = 0;
        for (int i = offset; i < offset + read; i++)
            if (float.IsFinite(buffer[i])) peak = Math.Max(peak, Math.Abs(buffer[i]));
        float previous;
        do { previous = Volatile.Read(ref _peak); if (previous >= peak) break; }
        while (Interlocked.CompareExchange(ref _peak, peak, previous) != previous);
        return read;
    }
}
