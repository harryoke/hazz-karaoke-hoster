using System.Windows;
using System.IO;
using HazzKaraokeHoster.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("Pass a local video path."); return 2; }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            AudienceWindow? window = null;
            try
            {
                window = new AudienceWindow { Width = 640, Height = 400, ShowActivated = false };
                window.Show();
                var video = (AudienceVideoSurface)window.FindName("AudienceMedia");
                window.SetKaraokeActive(true);
                window.LoadMutedVideo(Path.GetFullPath(args[0]));
                window.PlayVideo(TimeSpan.Zero);
                await Task.Delay(4500);
                Check(video.NativeActive != AudienceVideoSurface.WindowsRequested, "requested backend active");
                Check(video.Position.TotalSeconds > 1, "playback clock advances");
                window.PauseVideo(); await Task.Delay(700);
                var paused = video.Position;
                await Task.Delay(800);
                Check(Math.Abs((video.Position - paused).TotalMilliseconds) < 300, "pause holds position");
                window.PlayVideo(TimeSpan.FromSeconds(30)); await Task.Delay(2000);
                Check(video.Position.TotalSeconds is > 29 and < 35, "seek and resume");
                window.ClearKaraokeVisual(); window.SetKaraokeActive(false);
                window.ShowMusicVideo(Path.GetFullPath(args[0]), TimeSpan.FromSeconds(10), true);
                await Task.Delay(3000);
                var music = (AudienceVideoSurface)window.FindName("MusicVideoMedia");
                Check(music.Position.TotalSeconds > 10, "music video after karaoke");
                Check(!music.NativeActive || music.Overlay != null, "overlays moved to native music surface");
                window.ClearMusicVideo();
                Check(music.Source == null && music.Overlay == null, "clear restores overlays");
                window.Close(); window = null;
                for (var i = 0; i < 3; i++)
                {
                    window = new AudienceWindow { Width = 320, Height = 240, ShowActivated = false };
                    window.Show(); window.SetKaraokeActive(true);
                    window.LoadMutedVideo(Path.GetFullPath(args[0])); window.PlayVideo(TimeSpan.Zero);
                    await Task.Delay(500); window.Close(); window = null;
                }
                Console.WriteLine("PASS repeated native window/player disposal");
                app.Shutdown(0);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); window?.Close(); app.Shutdown(1); }
        };
        return app.Run();
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL " + name);
        Console.WriteLine("PASS " + name);
    }
}
