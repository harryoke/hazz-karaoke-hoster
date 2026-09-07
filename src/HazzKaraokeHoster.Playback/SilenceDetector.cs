using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace HazzKaraokeHoster.Playback;

/// <summary>
/// Fast read-only audio scan used by the Karaoke Deck to locate the next audible section.
/// It never edits or rewrites media. The caller decides how much pre-roll to retain.
/// </summary>
public static class SilenceDetector
{
    public static Task<TimeSpan?> FindNextAudibleAsync(
        string path,
        TimeSpan start,
        double thresholdDb = -45.0,
        TimeSpan? maxScan = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => FindNextAudible(path, start, thresholdDb, maxScan ?? TimeSpan.FromMinutes(3), cancellationToken), cancellationToken);

    private static TimeSpan? FindNextAudible(string path, TimeSpan start, double thresholdDb, TimeSpan maxScan, CancellationToken cancellationToken)
    {
        using var reader = new MediaFoundationReader(path);
        if (reader.TotalTime <= TimeSpan.Zero) return null;
        if (start < TimeSpan.Zero) start = TimeSpan.Zero;
        if (start >= reader.TotalTime) return null;
        reader.CurrentTime = start;

        var sample = reader.ToSampleProvider();
        var channels = Math.Max(1, sample.WaveFormat.Channels);
        var sampleRate = Math.Max(8000, sample.WaveFormat.SampleRate);
        const double blockSeconds = 0.05; // 50 ms
        var framesPerBlock = Math.Max(128, (int)(sampleRate * blockSeconds));
        var buffer = new float[framesPerBlock * channels];
        var threshold = Math.Pow(10.0, thresholdDb / 20.0);
        var requiredBlocks = 3; // about 150 ms of real signal avoids transient noise triggering a jump
        var consecutive = 0;
        TimeSpan? candidate = null;
        var scanEnd = start + maxScan;
        if (scanEnd > reader.TotalTime) scanEnd = reader.TotalTime;

        while (reader.CurrentTime < scanEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var blockStart = reader.CurrentTime;
            var read = sample.Read(buffer, 0, buffer.Length);
            if (read <= 0) break;

            double peak = 0;
            for (var i = 0; i < read; i++)
            {
                var a = Math.Abs(buffer[i]);
                if (a > peak) peak = a;
            }

            if (peak >= threshold)
            {
                if (consecutive == 0) candidate = blockStart;
                consecutive++;
                if (consecutive >= requiredBlocks) return candidate;
            }
            else
            {
                consecutive = 0;
                candidate = null;
            }
        }
        return null;
    }
}
