using NAudio.Wave;
namespace HazzKaraokeHoster.Playback;

public static class AudioNormalization
{
    public static volatile bool Enabled;
    private static double _targetDb = -18;
    public static double TargetDb { get => Volatile.Read(ref _targetDb); set => Volatile.Write(ref _targetDb, double.IsFinite(value) ? Math.Clamp(value, -24, -12) : -18); }
}

// Linked-channel RMS levelling. No allocations or file access on the audio callback.
public sealed class NormalizingSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private double _energy;
    private double _gain = 1;
    private double _limiter = 1;
    private readonly double _meterRate;
    private readonly double _gainRate;
    private readonly double _releaseRate;
    public WaveFormat WaveFormat => _source.WaveFormat;
    public NormalizingSampleProvider(ISampleProvider source)
    {
        _source = source;
        _meterRate = 1 - Math.Exp(-1.0 / (WaveFormat.SampleRate * 0.4));
        _gainRate = 1 - Math.Exp(-1.0 / (WaveFormat.SampleRate * 2.0));
        _releaseRate = 1 - Math.Exp(-1.0 / (WaveFormat.SampleRate * 0.2));
    }
    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        var enabled = AudioNormalization.Enabled;
        var target = Math.Pow(10, AudioNormalization.TargetDb / 20);
        var channels = WaveFormat.Channels;
        for (var i = offset; i < offset + read; i += channels)
        {
            var end = Math.Min(i + channels, offset + read);
            double power = 0, peak = 0;
            for (var c = i; c < end; c++) { if (!float.IsFinite(buffer[c])) buffer[c] = 0; power += buffer[c] * buffer[c]; peak = Math.Max(peak, Math.Abs(buffer[c])); }
            _energy += _meterRate * (power / (end - i) - _energy);
            // Silence gate avoids boosting near-silent passages; gain is limited to -24 / +12 dB.
            var desired = enabled && _energy > 0.00001 ? Math.Clamp(target / Math.Sqrt(_energy), 0.0630957, 3.9810717) : 1;
            _gain += _gainRate * (desired - _gain);
            var wantedLimiter = enabled ? Math.Min(1, 0.8912509 / Math.Max(0.000001, peak * _gain)) : 1;
            _limiter = wantedLimiter < _limiter ? wantedLimiter : _limiter + _releaseRate * (wantedLimiter - _limiter);
            for (var c = i; c < end; c++) buffer[c] = (float)(buffer[c] * _gain * _limiter);
        }
        return read;
    }
}
