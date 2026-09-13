using NAudio.Wave;
using SoundTouch;

namespace HazzKaraokeHoster.Playback;

// One lock serializes decoding, seeking and DSP changes with the audio callback.
public sealed class TempoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly SoundTouchProcessor _processor;
    private readonly object _gate = new();
    private readonly float[] _input;
    private bool _ended;
    public WaveFormat WaveFormat => _source.WaveFormat;

    public TempoSampleProvider(ISampleProvider source)
    {
        _source = source;
        _processor = new SoundTouchProcessor { SampleRate = WaveFormat.SampleRate, Channels = WaveFormat.Channels };
        _input = new float[2048 * WaveFormat.Channels];
    }

    public void Configure(double tempo, int semitones = 0)
    {
        lock (_gate)
        {
            _processor.Tempo = double.IsFinite(tempo) ? Math.Clamp(tempo, 0.75, 1.25) : 1;
            _processor.PitchSemiTones = Math.Clamp(semitones, -6, 6);
        }
    }

    public void Seek(Action seek)
    {
        lock (_gate) { seek(); _processor.Clear(); _ended = false; }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            int channels = WaveFormat.Channels, written = 0;
            count -= count % channels;
            while (written < count)
            {
                var destination = buffer.AsSpan(offset + written, count - written);
                int received = _processor.ReceiveSamples(destination, (count - written) / channels) * channels;
                written += received;
                if (written == count || (_ended && received == 0)) break;
                if (received > 0) continue;
                int read = _source.Read(_input, 0, _input.Length);
                if (read == 0) { _processor.Flush(); _ended = true; }
                else _processor.PutSamples(_input.AsSpan(0, read), read / channels);
            }
            return written;
        }
    }
}
