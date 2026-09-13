using HazzKaraokeHoster.App;
using HazzKaraokeHoster.Playback;
using NAudio.Wave;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using HazzKaraokeHoster.Core.Interfaces;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0) { VideoChecks(Path.GetFullPath(args[0])); return; }
        foreach (var channels in new[] { 1, 2 })
        foreach (var tempo in new[] { 0.75, 1.0, 1.25 })
        foreach (var key in new[] { 0, 3 })
        {
            var tone = new Tone(channels);
            var provider = new TempoSampleProvider(tone);
            provider.Configure(tempo, key);
            var output = new List<float>(); var buffer = new float[1024 * channels];
            int read;
            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.AddRange(buffer.Take(read));
                if (output.Count > 48000 * channels * 10) throw new Exception("DSP failed to terminate.");
            }
            Require(output.All(float.IsFinite), "Invalid output sample");
            double duration = output.Count / (48000.0 * channels);
            Require(Math.Abs(duration - 4 / tempo) < 0.08, $"Duration at {tempo}: {duration}");
            int start = 48000 * channels, end = Math.Min(output.Count - channels, 3 * 48000 * channels), crossings = 0;
            for (int i = start + channels; i < end; i += channels)
                if (output[i - channels] <= 0 && output[i] > 0) crossings++;
            double frequency = crossings / ((end - start) / (48000.0 * channels));
            double expected = 440 * Math.Pow(2, key / 12.0);
            Require(Math.Abs(frequency - expected) < 4, $"Pitch at tempo {tempo}, key {key}: {frequency}");
            provider.Seek(() => tone.Frame = 0);
            Require(provider.Read(buffer, 0, buffer.Length) == buffer.Length, "Seek after EOF failed");
            Console.WriteLine($"PASS {channels} channels / {tempo:P0} / key {key}: {duration:F3}s, {frequency:F1}Hz");
        }
        var directory = Path.Combine(Path.GetTempPath(), "HazzTempoChecks-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var file = Path.Combine(directory, "preferences.json");
            var track = Path.Combine(directory, "song.mp3");
            var preferences = new TempoPreferences(file);
            preferences.Save(track, null, 0.9);
            preferences.Save(track, "venue-a:Mary", 1.1);
            preferences = new TempoPreferences(file);
            Require(preferences.Load(track) == 0.9, "Track default persistence failed");
            Require(preferences.Load(track, "venue-a:MARY") == 1.1, "Singer precedence failed");
            Require(preferences.Load(track, "venue-b:Mary") == 0.9, "Venue isolation failed");
            Require(preferences.Load(Path.Combine(directory, "next.mp3")) == 1, "Next track inherited tempo");
            preferences.Save(track, "venue-a:Mary", 1);
            Require(new TempoPreferences(file).Load(track, "venue-a:Mary") == 1, "Saved reset failed");
            File.WriteAllText(file, "broken json");
            try { new TempoPreferences(file).Save(track, null, 1); throw new Exception("Corrupt preferences overwritten"); }
            catch (System.Text.Json.JsonException) { }
            var media = new RoutedMusicElement { Tempo = 0.85 };
            Require(media.SpeedRatio == 0.85, "Music clock tempo mismatch");
            media.Stop(); Require(media.Tempo == 1 && media.SpeedRatio == 1, "Stop did not reset tempo"); media.Close();
            var surface = new AudienceVideoSurface { Tempo = 1.2 };
            Require(surface.Tempo == 1.2, "Audience rate failed"); surface.Close();
            Console.WriteLine("PASS preferences, venue isolation, reset, corrupt-file protection and WPF clocks");
            var main = new MainWindow();
            main.Measure(new Size(1696, 1116)); main.Arrange(new Rect(0, 0, 1696, 1116)); main.UpdateLayout();
            var screenshot = new System.Windows.Media.Imaging.RenderTargetBitmap(1696, 1116, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var root = (FrameworkElement)main.Content;
            root.Measure(new Size(1696, 1116)); root.Arrange(new Rect(0, 0, 1696, 1116)); root.UpdateLayout();
            screenshot.Render(root);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(screenshot));
            using (var snapshot = File.Create(Path.Combine(AppContext.BaseDirectory, "console-v1.2.png"))) png.Save(snapshot);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            void Field(string name, object value) => typeof(MainWindow).GetField(name, flags)!.SetValue(main, value);
            Field("_restoringLiveShowState", true); Field("_restoringMusicDeckQueues", true);
            Field("_singers", DispatchProxy.Create<ISingerRepository, SingerProxy>());
            var combo = (ComboBox)main.FindName("SingerNameBox");
            Require(main.FindName("ManualSongBox") is null && main.FindName("GlobalTempoButton") is Button, "Reclaimed controls missing");
            using var source = new HwndSource(new HwndSourceParameters("Hazz hidden keyboard test") { Width = 1, Height = 1, WindowStyle = 0 });
            combo.Text = "New Test Singer";
            var enter = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            combo.RaiseEvent(enter);
            var singers = (DataGrid)main.FindName("QueueList");
            Require(enter.Handled && singers.Items.Count == 1 && combo.Text.Length == 0, "Enter did not add singer");
            combo.Text = "New Test Singer";
            combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 1, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Require(singers.Items.Count == 1, "Enter duplicated singer");
            var deckType = typeof(MainWindow).GetNestedType("MusicDeckId", BindingFlags.NonPublic)!;
            var deck1 = Enum.Parse(deckType, "Deck1"); var deck2 = Enum.Parse(deckType, "Deck2");
            var list = (ListBox)main.FindName("DeckAPlaylist");
            var playing = new MusicQueueItem { FilePath = track };
            list.Items.Add(playing); list.Items.Add(new MusicQueueItem { FilePath = track + "2" });
            var other = (ListBox)main.FindName("DeckBPlaylist"); other.Items.Add(new MusicQueueItem { FilePath = track + "3" });
            Field("_deck1CurrentItem", playing); Field("_resumeMusicDeck", deck1); Field("_resumeMusicItem", playing);
            typeof(MainWindow).GetMethod("ClearMusicPlaylist", flags)!.Invoke(main, new[] { deck1 });
            Require(list.Items.Count == 0 && other.Items.Count == 1, "Clear affected wrong list");
            Require(ReferenceEquals(typeof(MainWindow).GetField("_deck1CurrentItem", flags)!.GetValue(main), playing), "Clear interrupted current track");
            Require(typeof(MainWindow).GetField("_resumeMusicItem", flags)!.GetValue(main) is null, "Cleared track could resume after karaoke");
            typeof(MainWindow).GetMethod("ClearMusicPlaylist", flags)!.Invoke(main, new[] { deck2 });
            Require(other.Items.Count == 0, "Deck 2 clear failed");
            Console.WriteLine("PASS Enter adds/does not duplicate, removed song box, global tempo button and clearing both playlists");
        }
        finally { Directory.Delete(directory, true); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void VideoChecks(string path)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            try
            {
                foreach (var native in new[] { false, true })
                {
                    var surface = new AudienceVideoSurface { UseLibVlc = native, Source = new Uri(path) };
                    var window = new Window { Title = "Hazz muted video tempo check", Content = surface, Width = 320, Height = 240, ShowActivated = false };
                    window.Show(); surface.Play();
                    await Task.Delay(1800);
                    Require(surface.NativeActive == native, "Wrong video engine");
                    foreach (var rate in new[] { 0.75, 1.25 })
                    {
                        surface.Position = TimeSpan.FromSeconds(1); surface.Tempo = rate;
                        await Task.Delay(600);
                        var before = surface.Position; var watch = System.Diagnostics.Stopwatch.StartNew();
                        await Task.Delay(2000);
                        double actual = (surface.Position - before).TotalSeconds / watch.Elapsed.TotalSeconds;
                        Require(Math.Abs(actual - rate) < 0.18, $"{native} video rate {rate}: {actual}");
                        Console.WriteLine($"PASS {(native ? "VLC" : "Windows")} video {rate:P0}: clock ratio {actual:F2}");
                    }
                    surface.Pause(); await Task.Delay(300); var paused = surface.Position; await Task.Delay(500);
                    Require(Math.Abs((surface.Position - paused).TotalSeconds) < 0.2, "Tempo pause drift");
                    surface.Position = TimeSpan.FromSeconds(2); surface.Play(); await Task.Delay(800);
                    Require(surface.Position.TotalSeconds is > 2.3 and < 3.6, "Tempo seek/resume failed");
                    surface.Close(); window.Close();
                }
                Console.WriteLine("PASS both video engines: tempo, pause and seek"); app.Shutdown(0);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); app.Shutdown(1); Environment.ExitCode = 1; }
        };
        app.Run();
    }
    public class SingerProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == "UpsertSingerAsync"
            ? Task.FromResult(123L) : throw new Exception("Unexpected database access: " + method.Name);
    }
    sealed class Tone(int channels) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, channels);
        public int Frame;
        public int Read(float[] buffer, int offset, int count)
        {
            int frames = Math.Min(count / channels, 48000 * 4 - Frame);
            for (int i = 0; i < frames; i++, Frame++)
                for (int c = 0; c < channels; c++) buffer[offset + i * channels + c] = (float)(0.4 * Math.Sin(2 * Math.PI * 440 * Frame / 48000));
            return frames * channels;
        }
    }
}
