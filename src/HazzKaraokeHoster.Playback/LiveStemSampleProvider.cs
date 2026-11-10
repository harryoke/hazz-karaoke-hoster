using NAudio.Wave;

namespace HazzKaraokeHoster.Playback;

public sealed record LiveStemBlock(long Start, int Frames, float[] Samples)
{
    public const int Rate = 44100;
    public const int BlockFrames = 4 * Rate;
}

// Original playback owns the clock. Inference publishes immutable blocks; the
// audio callback never waits for the model, reads its pipes, or opens a file.
public sealed class LiveStemSampleProvider(WaveStream reader, ISampleProvider original) : ISampleProvider
{
    private LiveStemBlock[] _blocks = [];
    private int _mask = 15;
    private long _playhead;
    private int _covered;
    private float _blend;
    private float _lastLeft, _lastRight;
    private readonly float[] _gains = [1, 1, 1, 1];

    public WaveFormat WaveFormat => original.WaveFormat;
    public long Playhead => Interlocked.Read(ref _playhead);
    public bool Covered => Volatile.Read(ref _covered) == 1;
    public int Mask { get => Volatile.Read(ref _mask); set => Volatile.Write(ref _mask, value & 15); }
    public void Publish(LiveStemBlock[] blocks) => Volatile.Write(ref _blocks, blocks);

    public int Read(float[] buffer, int offset, int count)
    {
        var sourceFrame = reader.Position / reader.WaveFormat.BlockAlign;
        int read = original.Read(buffer, offset, count);
        var channels = WaveFormat.Channels;
        var rate = WaveFormat.SampleRate;
        Interlocked.Exchange(ref _playhead, (sourceFrame + read / channels) * LiveStemBlock.Rate / rate);
        var mask = Mask;
        if (channels is < 1 or > 2 || (mask == 15 && _blend == 0))
        {
            Volatile.Write(ref _covered, 0);
            return read;
        }

        var blocks = Volatile.Read(ref _blocks);
        LiveStemBlock? block = null;
        var step = 100f / rate; // ten millisecond de-click ramp
        bool covered = false;
        for (int i = 0; i + channels <= read; i += channels, sourceFrame++)
        {
            long scaled = sourceFrame * LiveStemBlock.Rate;
            long frame = scaled / rate;
            if (block is null || frame < block.Start || frame >= block.Start + block.Frames)
            {
                block = null;
                foreach (var candidate in blocks)
                    if (frame >= candidate.Start && frame < candidate.Start + candidate.Frames) { block = candidate; break; }
            }

            covered = block is not null;
            bool use = mask == 0 || (covered && mask != 15);
            _blend = Move(_blend, use ? 1 : 0, step);
            if (_blend == 0) continue;
            if (mask == 0) _lastLeft = _lastRight = 0;
            else if (block is not null)
            {
                var j = (int)(frame - block.Start) * 2;
                var stride = block.Frames * 2;
                var fraction = (float)(scaled % rate) / rate;
                float left = 0, right = 0;
                for (int stem = 0; stem < 4; stem++)
                {
                    var gain = _gains[stem] = Move(_gains[stem], (mask & (1 << stem)) != 0 ? 1 : 0, step);
                    var p = stem * stride + j;
                    var next = j + 2 < stride ? p + 2 : p;
                    var samples = block.Samples;
                    left += gain * (samples[p] + fraction * (samples[next] - samples[p]));
                    right += gain * (samples[p + 1] + fraction * (samples[next + 1] - samples[p + 1]));
                }
                _lastLeft = Math.Clamp(left, -1, 1);
                _lastRight = Math.Clamp(right, -1, 1);
            }

            var k = offset + i;
            buffer[k] += _blend * ((channels == 1 ? (_lastLeft + _lastRight) * 0.5f : _lastLeft) - buffer[k]);
            if (channels == 2) buffer[k + 1] += _blend * (_lastRight - buffer[k + 1]);
        }
        Volatile.Write(ref _covered, covered && mask != 15 ? 1 : 0);
        return read;
    }

    private static float Move(float value, float target, float step)
        => value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);
}
