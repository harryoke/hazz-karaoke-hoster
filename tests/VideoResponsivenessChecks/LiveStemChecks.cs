using System.Diagnostics;
using System.IO;
using System.Reflection;
using HazzKaraokeHoster.App;
using HazzKaraokeHoster.Playback;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

internal static class LiveStemChecks
{
static void Check(bool ok, string description)
{
    if (!ok) throw new Exception(description);
    Console.WriteLine("PASS " + description);
}

public static async Task Run(string[] args)
{
var fixture = Path.GetFullPath(args[0]);
var engine = Path.GetFullPath(args[1]);
using (var reader = new MediaFoundationReader(fixture))
{
    var provider = new LiveStemSampleProvider(reader, reader.ToSampleProvider());
    int channels = provider.WaveFormat.Channels;
    var buffer = new float[2048 * channels];
    var original = new float[buffer.Length];
    var n = provider.Read(original, 0, original.Length);
    provider.Mask = 8;
    reader.Position = 0;
    Check(provider.Read(buffer, 0, n) == n && original.AsSpan(0, n).SequenceEqual(buffer.AsSpan(0, n)),
        "no ready stem block means unchanged original");
    var samples = new float[4 * 4096 * 2];
    for (int i = 0; i < 4; i++) Array.Fill(samples, 0.05f * (i + 1), i * 8192, 8192);
    provider.Publish([new(0, 4096, samples)]);
    foreach (var (mask, expected) in new[] { (8, 0.20f), (1, 0.05f), (7, 0.30f), (9, 0.25f), (0, 0f) })
    {
        provider.Mask = mask;
        reader.Position = 0;
        provider.Read(buffer, 0, n);
        Check(Math.Abs(buffer[n - 1] - expected) < 0.001f, $"stem mask {mask} mixes only selected parts");
    }
    provider.Mask = 15;
    reader.Position = 0;
    provider.Read(buffer, 0, n);
    Check(Math.Abs(original[n - 1] - buffer[n - 1]) < 0.001f, "restore returns to original audio");
}

var type = typeof(RoutedMusicElement).Assembly.GetType("HazzKaraokeHoster.App.LiveStemWorker")!;
var run = type.GetMethod("RunAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
using var modelReader = new MediaFoundationReader(fixture);
var sink = new LiveStemSampleProvider(modelReader, modelReader.ToSampleProvider()) { Mask = 8 };
using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var clock = Stopwatch.StartNew();
var job = (Task)run.Invoke(null, [fixture, sink, cancel.Token, engine])!;
try
{
    var buffer = new float[2048 * sink.WaveFormat.Channels];
    async Task WaitForCoverage(double seconds)
    {
        while (!sink.Covered)
        {
            if (job.IsCompleted) await job;
            cancel.Token.ThrowIfCancellationRequested();
            modelReader.CurrentTime = TimeSpan.FromSeconds(seconds);
            sink.Read(buffer, 0, buffer.Length);
            if (!sink.Covered) await Task.Delay(60, cancel.Token);
        }
    }
    await WaitForCoverage(0);
    Check(buffer.All(float.IsFinite) && buffer.Any(x => x != 0), $"real worker supplies finite vocals after {clock.Elapsed.TotalSeconds:F2}s");
    int uninterrupted = 0;
    modelReader.CurrentTime = TimeSpan.Zero;
    while (sink.Playhead < 6 * LiveStemBlock.Rate)
    {
        int count = sink.Read(buffer, 0, buffer.Length);
        if (count == 0 || !sink.Covered) break;
        uninterrupted++;
        await Task.Delay(TimeSpan.FromSeconds(count / (double)(sink.WaveFormat.SampleRate * sink.WaveFormat.Channels)), cancel.Token);
    }
    Check(sink.Covered && sink.Playhead >= 6 * LiveStemBlock.Rate && uninterrupted > 50,
        "real rolling model keeps coverage across a block boundary at playback speed");
    modelReader.CurrentTime = TimeSpan.FromSeconds(8.25);
    sink.Read(buffer, 0, buffer.Length);
    await WaitForCoverage(8.25);
    Check(sink.Playhead > 8 * LiveStemBlock.Rate, "rolling coverage follows a forward seek");
    modelReader.CurrentTime = TimeSpan.Zero;
    sink.Read(buffer, 0, buffer.Length);
    await WaitForCoverage(0);
    Check(sink.Playhead < LiveStemBlock.Rate, "rolling coverage follows a backward seek");
}
finally
{
    cancel.Cancel();
    try { await job.WaitAsync(TimeSpan.FromSeconds(10)); } catch (OperationCanceledException) { }
}
Check(job.IsCompleted, "cancellation stops the model process");

}
}
