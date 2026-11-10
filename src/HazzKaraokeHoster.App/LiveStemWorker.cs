using System.Diagnostics;
using System.Runtime.InteropServices;
using HazzKaraokeHoster.Playback;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace HazzKaraokeHoster.App;

internal static class LiveStemWorker
{
    internal static string EngineDirectory => Path.Combine(AppContext.BaseDirectory, "LiveStems");
    internal static bool Available => HasEngine(EngineDirectory);
    private static bool HasEngine(string engine) => File.Exists(Path.Combine(engine, "python", "python.exe")) &&
        File.Exists(Path.Combine(engine, "live_stems.py")) &&
        File.Exists(Path.Combine(engine, "hdemucs_high_trained.pt"));

    internal static async Task RunAsync(string path, LiveStemSampleProvider sink, CancellationToken token, string? engineDirectory = null)
    {
        token.ThrowIfCancellationRequested();
        var engine = engineDirectory ?? EngineDirectory;
        if (!HasEngine(engine)) throw new InvalidOperationException("Install the optional Live Stems engine beside Hazz.");
        var start = new ProcessStartInfo(Path.Combine(engine, "python", "python.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = engine,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("-B");
        start.ArgumentList.Add(Path.Combine(engine, "live_stems.py"));
        using var process = Process.Start(start) ?? throw new IOException("Could not start the Live Stems engine.");
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
        void StopProcess()
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
        using var registration = token.Register(StopProcess);
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            var input = process.StandardInput.BaseStream;
            var output = process.StandardOutput.BaseStream;
            var header = new byte[4];
            await output.ReadExactlyAsync(header, token).AsTask().WaitAsync(TimeSpan.FromSeconds(45), token).ConfigureAwait(false);
            if (!header.AsSpan().SequenceEqual("HST1"u8)) throw new InvalidDataException("Unexpected Live Stems engine protocol.");

            using var decoder = new SequentialStereoDecoder(path);
            var blockSize = LiveStemBlock.BlockFrames;
            const int context = LiveStemBlock.Rate / 2;
            var work = new float[(blockSize + 2 * context) * 2];
            var payload = new byte[work.Length * sizeof(float)];
            var result = new byte[payload.Length * 4];
            var blocks = new SortedDictionary<long, LiveStemBlock>();
            long eof = long.MaxValue;
            while (!token.IsCancellationRequested)
            {
                var current = sink.Playhead / blockSize * blockSize;
                // Keep only a bounded rolling window and prioritize the current playhead after seeking.
                foreach (long key in blocks.Keys.Where(k => k < current - blockSize || k > current + 3L * blockSize).ToArray())
                    blocks.Remove(key);
                var next = current;
                while (blocks.ContainsKey(next)) next += blockSize;
                if (next >= eof || next > current + 3L * blockSize || sink.Mask is 0 or 15)
                {
                    await Task.Delay(80, token).ConfigureAwait(false);
                    continue;
                }

                long left = Math.Max(0, next - context);
                var prefix = (int)(next - left);
                int frames = decoder.ReadAt(left, work, token) / 2;
                var keep = Math.Min(blockSize, frames - prefix);
                if (keep <= 0) { eof = Math.Min(eof, next); continue; }
                if (keep < blockSize) eof = Math.Min(eof, next + keep);

                BitConverter.TryWriteBytes(header, frames);
                int byteCount = frames * 2 * sizeof(float);
                MemoryMarshal.AsBytes(work.AsSpan(0, frames * 2)).CopyTo(payload);
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
                budget.CancelAfter(TimeSpan.FromSeconds(30));
                using var killOnTimeout = budget.Token.Register(StopProcess);
                await input.WriteAsync(header, budget.Token).ConfigureAwait(false);
                await input.WriteAsync(payload.AsMemory(0, byteCount), budget.Token).ConfigureAwait(false);
                await input.FlushAsync(budget.Token).ConfigureAwait(false);
                await output.ReadExactlyAsync(result.AsMemory(0, byteCount * 4), budget.Token).ConfigureAwait(false);
                var samples = new float[keep * 2 * 4];
                for (int stem = 0; stem < 4; stem++)
                    Buffer.BlockCopy(result, (stem * frames + prefix) * 2 * sizeof(float),
                        samples, stem * keep * 2 * sizeof(float), keep * 2 * sizeof(float));
                blocks[next] = new LiveStemBlock(next, keep, samples);
                sink.Publish(blocks.Values.ToArray());
            }
        }
        catch (EndOfStreamException)
        {
            StopProcess();
            var message = await error.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            throw new IOException(string.IsNullOrWhiteSpace(message) ? "Live Stems worker exited." : message.Trim()[..Math.Min(240, message.Trim().Length)]);
        }
        finally
        {
            StopProcess();
            await process.WaitForExitAsync().ConfigureAwait(false);
            await error.ConfigureAwait(false);
            sink.Publish([]);
        }
    }

    // Read sequentially from the start for sample alignment, even with compressed
    // formats whose native seek lands on a nearby packet rather than the exact sample.
    private sealed class SequentialStereoDecoder : IDisposable
    {
        private readonly string _path;
        private MediaFoundationReader? _reader;
        private ISampleProvider? _samples;
        private long _cursor;
        private readonly float[] _skip = new float[16384];
        private readonly float[] _tail = new float[LiveStemBlock.Rate * 4];
        private int _tailCount;

        public SequentialStereoDecoder(string path) => _path = path;
        private void Open()
        {
            _reader?.Dispose();
            _reader = new MediaFoundationReader(_path);
            ISampleProvider samples = _reader.ToSampleProvider();
            if (samples.WaveFormat.Channels == 1) samples = new MonoToStereoSampleProvider(samples);
            if (samples.WaveFormat.Channels != 2) throw new InvalidDataException("Live Stems requires mono or stereo audio.");
            _samples = samples.WaveFormat.SampleRate == LiveStemBlock.Rate ? samples
                : new WdlResamplingSampleProvider(samples, LiveStemBlock.Rate);
            _cursor = 0;
            _tailCount = 0;
        }

        public int ReadAt(long startFrame, float[] target, CancellationToken token)
        {
            long start = startFrame * 2;
            if (_samples is null || start < _cursor - _tailCount) Open();
            while (_cursor < start)
            {
                token.ThrowIfCancellationRequested();
                int read = _samples!.Read(_skip, 0, (int)Math.Min(_skip.Length, start - _cursor));
                if (read <= 0) return 0;
                _cursor += read;
                _tailCount = 0;
            }
            int copied = (int)(_cursor - start);
            if (copied > 0) Array.Copy(_tail, _tailCount - copied, target, 0, copied);
            int total = copied;
            while (total < target.Length)
            {
                token.ThrowIfCancellationRequested();
                int read = _samples!.Read(target, total, target.Length - total);
                if (read <= 0) break;
                total += read;
                _cursor += read;
            }
            _tailCount = Math.Min(total, _tail.Length);
            Array.Copy(target, total - _tailCount, _tail, 0, _tailCount);
            return total;
        }

        public void Dispose() => _reader?.Dispose();
    }
}
