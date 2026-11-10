using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using HazzKaraokeHoster.App;
using HazzKaraokeHoster.Playback;

internal static class Program
{
    static object Commands(AudienceVideoSurface s) => typeof(AudienceVideoSurface).GetField("_commands", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!;
    static void Queue(AudienceVideoSurface s, Action a) { var q=Commands(s); q.GetType().GetMethod("Enqueue", [typeof(Action)])!.Invoke(q, [a]); }
    static Task Drain(AudienceVideoSurface s) { var q=Commands(s); return (Task)q.GetType().GetMethod("DrainAsync",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(q,null)!; }
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS "+message); }
    static async Task Until(Func<bool> ready,int ms=12000) { var watch=Stopwatch.StartNew();while(!ready() && watch.ElapsedMilliseconds<ms) await Task.Delay(50); if(!ready()) throw new Exception("Timed out waiting for video"); }
    [STAThread] static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--live-stems")
        {
            Task.Run(() => LiveStemChecks.Run(args[1..])).GetAwaiter().GetResult();
            return 0;
        }
        if(args[0]=="--make-mp4")
        {
            LibVLCSharp.Shared.Core.Initialize(System.IO.Path.Combine(AppContext.BaseDirectory,"libvlc","win-x64"));
            using var engine=new LibVLCSharp.Shared.LibVLC("--no-audio","--no-video-title-show");
            using var player=new LibVLCSharp.Shared.MediaPlayer(engine);
            using var source=new LibVLCSharp.Shared.Media(engine,new Uri(System.IO.Path.GetFullPath(args[1])));
            source.AddOption(":sout=#transcode{vcodec=mp4v,vb=300}:std{access=file,mux=mp4,dst='"+System.IO.Path.GetFullPath(args[2]).Replace('\\','/')+"'}");
            using var done=new ManualResetEventSlim();player.EndReached+=(_,_)=>done.Set();
            if(!player.Play(source) || !done.Wait(TimeSpan.FromSeconds(55))) return 1;
            player.Stop();Console.WriteLine("MP4 fixture generated");return 0;
        }
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Startup+=async (_,_)=>
        {
            AudienceVideoSurface? s=null; Window? w=null;
            try
            {
                var drift = new KaraokeDriftFilter();
                Check(!drift.NeedsCorrection(550) && !drift.NeedsCorrection(550) && !drift.NeedsCorrection(550), "queued audio lead does not trigger seeking");
                Check(!drift.NeedsCorrection(-900) && !drift.NeedsCorrection(0) && !drift.NeedsCorrection(-900), "transient clock jumps do not trigger seeking");
                Check(!drift.NeedsCorrection(-900) && drift.NeedsCorrection(-900), "persistent drift still corrects");
                Check(!drift.NeedsCorrection(1500) && !drift.NeedsCorrection(-900) && !drift.NeedsCorrection(1500), "alternating drift does not trigger seeking");
                var baselineAudio = args.Contains("--baseline-audio");
                using var devices=new NAudio.CoreAudioApi.MMDeviceEnumerator();
                using var device=devices.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render,NAudio.CoreAudioApi.Role.Multimedia);
                var prepared = baselineAudio ? new RealtimePitchAudioPlayer() : await RealtimePitchAudioPlayer.PrepareAsync(args[1],device.ID);
                if (baselineAudio) prepared.TryLoad(args[1],out _);
                try
                {
                    Check(prepared.IsLoaded,"background Media Foundation audio preparation on selected WASAPI output");
                    Check(prepared.TotalTime.TotalSeconds>3,"prepared audio duration retained");
                    prepared.SetSemitones(2);prepared.SetTempo(1.1);prepared.Play(TimeSpan.Zero);
                    await Task.Delay(300);prepared.Pause();
                    Check(prepared.Position>TimeSpan.Zero,"prepared pipeline plays after UI handoff");
                    prepared.Position=TimeSpan.FromSeconds(1);prepared.Play();await Task.Delay(200);prepared.Stop();
                    Check(prepared.Position<TimeSpan.FromMilliseconds(20),"prepared pipeline seek and stop");
                }
                finally { if(baselineAudio) prepared.Dispose(); else await Task.Run(prepared.Dispose); }
                if (baselineAudio) {app.Shutdown(0);return;}
                using(var failed=await RealtimePitchAudioPlayer.PrepareAsync(args[1]+"-missing",null))
                    Check(!failed.IsLoaded && failed.LastLoadError is not null,"failed preparation preserves fallback error");
                using(var cancelled=new CancellationTokenSource())
                {
                    cancelled.Cancel();var observed=false;
                    try{using var unused=await RealtimePitchAudioPlayer.PrepareAsync(args[1],null,cancelled.Token);}
                    catch(OperationCanceledException){observed=true;}
                    Check(observed,"cancelled preparation never reaches live deck");
                }
                s=new AudienceVideoSurface { UseLibVlc=true };
                w=new Window {Content=s,Width=320,Height=200,ShowActivated=false}; w.Show();
                var ticks=0;double maxGap=0;var sw=Stopwatch.StartNew();double last=sw.Elapsed.TotalMilliseconds;
                var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(25)};
                timer.Tick+=(_,_)=>{var now=sw.Elapsed.TotalMilliseconds;maxGap=Math.Max(maxGap,now-last);last=now;ticks++;};timer.Start();
                // Deliberately hold the real decoder queue for ten seconds. Play,
                // clock reads and controls must return while native work is stalled.
                Queue(s,()=>Thread.Sleep(10000));
                var call=Stopwatch.StartNew();s.Source=new Uri(System.IO.Path.GetFullPath(args[0]));s.Play();
                _=s.Position;_=s.Duration;s.Pause();s.Play();
                Check(call.ElapsedMilliseconds<500,"load/play/pause return during stalled decoder");
                await Task.Delay(10500);
                Check(ticks>100 && maxGap<1500,$"dispatcher stays responsive during 10s stall ({ticks} ticks, max gap {maxGap:F0}ms)");
                await Until(()=>s.Position.TotalSeconds>0.3);
                Check(s.NativeActive,"native video opens after delayed startup");
                s.Pause();await Task.Delay(400);var pause=s.Position;await Task.Delay(400);
                Check(Math.Abs((s.Position-pause).TotalMilliseconds)<200,"pause holds cached clock");
                s.Position=TimeSpan.FromSeconds(10);s.Play();await Until(()=>s.Position.TotalSeconds>10.3);
                Check(s.Position.TotalSeconds<15,"seek and resume after startup");
                s.Tempo=1.25;await Task.Delay(600);var before=s.Position;var rate=Stopwatch.StartNew();await Task.Delay(1600);
                Check(Math.Abs((s.Position-before).TotalSeconds/rate.Elapsed.TotalSeconds-1.25)<0.25,"native tempo retained");
                s.Stop();s.Play();await Until(()=>s.Position.TotalSeconds>0.2 && s.Position.TotalSeconds<5);
                Check(true,"stop then restart same source");
                s.Stop();s.Source=null;s.Source=new Uri(System.IO.Path.GetFullPath(args[0]));s.Play();
                await Until(()=>s.Position.TotalSeconds>0.2);
                Check(true,"rapid source changes discard stale startup");
                Queue(s,()=>throw new InvalidOperationException("injected worker failure"));
                var continued=false;Queue(s,()=>continued=true);await Drain(s);
                Check(continued,"worker survives a failed command");
                Queue(s,()=>Thread.Sleep(1000));call.Restart();s.Close();w.Close();
                Check(call.ElapsedMilliseconds<500,"close returns without waiting for native work");
                await Drain(s);timer.Stop();
                Check(true,"ordered native cleanup completes");
                Console.WriteLine("ALL VIDEO RESPONSIVENESS CHECKS PASSED");app.Shutdown(0);
            }
            catch(Exception ex){Console.Error.WriteLine(ex);s?.Close();w?.Close();Environment.ExitCode=1;app.Shutdown(1);}
        };
        return app.Run();
    }
}
