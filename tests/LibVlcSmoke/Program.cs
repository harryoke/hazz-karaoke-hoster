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
                window.SetVideoEnginePreference(!AudienceVideoSurface.WindowsRequested);
                var video = (AudienceVideoSurface)window.FindName("AudienceMedia");
                window.SetKaraokeActive(true);
                window.LoadMutedVideo(Path.GetFullPath(args[0]));
                window.PlayVideo(TimeSpan.Zero);
                await Task.Delay(4500);
                Check(video.NativeActive != AudienceVideoSurface.WindowsRequested, "requested backend active");
                Check(video.Position.TotalSeconds > 1, "playback clock advances");
                foreach (var fill in new[] { true, false, true })
                {
                    video.StretchToFill = fill;
                    window.Width = fill ? 800 : 640;
                    window.UpdateLayout();
                    await Task.Delay(300);
                    var player = typeof(AudienceVideoSurface).GetField("_player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(video) as LibVLCSharp.Shared.MediaPlayer;
                    if (video.NativeActive) Check(fill ? !string.IsNullOrEmpty(player!.AspectRatio) : string.IsNullOrEmpty(player!.AspectRatio), "VLC fit/stretch aspect applied");
                    Check(((System.Windows.Controls.MediaElement)video.Children[0]).Stretch == (fill ? System.Windows.Media.Stretch.Fill : System.Windows.Media.Stretch.Uniform), "Windows sizing preserved for fallback");
                }
                window.PauseVideo(); await Task.Delay(700);
                var paused = video.Position;
                await Task.Delay(800);
                Check(Math.Abs((video.Position - paused).TotalMilliseconds) < 300, "pause holds position");
                window.PlayVideo(TimeSpan.FromSeconds(30)); await Task.Delay(2000);
                Check(video.Position.TotalSeconds is > 29 and < 35, "seek and resume");
                window.VideoSyncOffsetSeconds=1;
                window.RepositionVideo(TimeSpan.FromSeconds(30));await Task.Delay(400);
                Check(video.Position.TotalSeconds is >30.7 and <32,"positive AVS advances picture");
                window.VideoSyncOffsetSeconds=-1;
                window.RepositionVideo(TimeSpan.FromSeconds(30));await Task.Delay(400);
                Check(video.Position.TotalSeconds is >28.7 and <30,"negative AVS delays picture");
                window.PlayVideo(TimeSpan.Zero);await Task.Delay(600);
                Check(video.Position.TotalSeconds<0.3,"negative AVS holds first frame until audio catches up");
                window.SyncVideo(TimeSpan.FromSeconds(1.2));await Task.Delay(600);
                Check(video.Position.TotalSeconds>0.3,"negative AVS resumes after initial hold");
                window.VideoSyncOffsetSeconds=0;
                window.PlayVideo(TimeSpan.FromSeconds(10));await Task.Delay(600);
                using(var preview=new AudiencePreview { GetAudience=()=>window })
                {
                    var host=new Window { Content=preview, Width=480, Height=300, ShowActivated=false };
                    host.Show();await Task.Delay(1500);preview.Refresh();
                    var mirror=(AudienceVideoSurface)typeof(AudiencePreview).GetField("_video",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(preview)!;
                    Check(mirror is not null && mirror.Source==video.Source && Math.Abs((mirror.Position-video.Position).TotalSeconds)<1,"full preview mirrors video clock");
                    var silent=(System.Windows.Controls.MediaElement)mirror!.Children[0];
                    Check(silent.IsMuted && silent.Volume==0,"preview Windows layer is silent");
                    if(mirror.NativeActive)
                    {
                        var native=(LibVLCSharp.Shared.MediaPlayer)typeof(AudienceVideoSurface).GetField("_player",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(mirror)!;
                        Check(native.Mute,"preview VLC layer is silent");
                    }
                    preview.Dispose();host.Close();
                }
                if (!AudienceVideoSurface.WindowsRequested)
                {
                    window.SetVideoEnginePreference(false);
                    Check(video.NativeActive, "engine preference does not interrupt current video");
                    window.LoadMutedVideo(Path.GetFullPath(args[0])); window.PlayVideo(TimeSpan.FromSeconds(5));
                    await Task.Delay(2500);
                    Check(!video.NativeActive && video.Position.TotalSeconds > 5, "next load switches to Windows");
                    window.SetVideoEnginePreference(true);
                    window.LoadMutedVideo(Path.GetFullPath(args[0])); window.PlayVideo(TimeSpan.FromSeconds(5));
                    await Task.Delay(2500);
                    Check(video.NativeActive && video.Position.TotalSeconds > 5, "next load switches back to VLC");
                }
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
                    window.SetVideoEnginePreference(!AudienceVideoSurface.WindowsRequested);
                    window.LoadMutedVideo(Path.GetFullPath(args[0])); window.PlayVideo(TimeSpan.Zero);
                    await Task.Delay(500); window.Close(); window = null;
                }
                Console.WriteLine("PASS repeated native window/player disposal");
                if (!AudienceVideoSurface.WindowsRequested)
                {
                    window = new AudienceWindow { Width = 320, Height = 240, ShowActivated = false };
                    window.Show(); window.SetVideoEnginePreference(true); window.SetKaraokeActive(true);
                    var failedVideo = (AudienceVideoSurface)window.FindName("AudienceMedia");
                    bool failed = false;
                    failedVideo.MediaFailed += (_, _) => failed = true;
                    window.LoadMutedVideo(Path.Combine(Path.GetTempPath(), Guid.NewGuid()+"-missing.mp4"));
                    window.PlayVideo(TimeSpan.Zero);
                    for (int i=0;i<30 && !failed;i++) await Task.Delay(200);
                    Check(!failedVideo.NativeActive && failed, "unplayable VLC source falls back and reports Windows failure");
                    window.Close(); window = null;
                }
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
