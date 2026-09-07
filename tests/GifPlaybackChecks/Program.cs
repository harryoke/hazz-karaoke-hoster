using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HazzKaraokeHoster.App;
using HazzKaraokeHoster.Core.Models;
using XamlAnimatedGif;
class Check {
[STAThread] static void Main() {
var app=new Application();
var w=new AudienceWindow { Opacity=0, ShowActivated=false, ShowInTaskbar=false };
w.Loaded += async (_,_) => {
try {
var bytes=Convert.FromHexString("47494638396101000100800000FF00000000FF21FF0B4E45545343415045322E30030100000021F90400280000002C000000000100010000020244010021F90400280000002C00000000010001000002024C01003B");
var path=Path.Combine(AppContext.BaseDirectory,"animated.gif");File.WriteAllBytes(path,bytes);
var settings=new AudienceOverlaySettings { BackgroundImageEnabled=true,BackgroundImagePath=path,BackgroundGifSpeed=1 };
w.Apply(settings);
var img=(Image)typeof(AudienceWindow).GetField("SingerBackgroundGif",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
async Task<int> Frames(double speed) {
settings.BackgroundGifSpeed=speed;w.Apply(settings);await Task.Delay(300);
var anim=AnimationBehavior.GetAnimator(img) ?? throw new Exception("GIF did not load");
int count=0; EventHandler h=(_,_)=>count++;anim.CurrentFrameChanged+=h;await Task.Delay(1700);anim.CurrentFrameChanged-=h;return count;
}
int normal=await Frames(1),fast=await Frames(2),slow=await Frames(.5);
if(!(fast>normal && normal>slow && slow>0))throw new Exception($"Speed failure: {normal}/{fast}/{slow}");
if(!File.ReadAllBytes(path).SequenceEqual(bytes))throw new Exception("Original GIF changed");
settings.BackgroundImageEnabled=false;w.Apply(settings);await Task.Delay(100);
if(AnimationBehavior.GetAnimator(img)!=null)throw new Exception("GIF not released on disable");
settings.BackgroundImageEnabled=true;settings.BackgroundImagePath="";settings.BackgroundFolderPath=AppContext.BaseDirectory;w.Apply(settings);await Task.Delay(500);
if(AnimationBehavior.GetAnimator(img)==null)throw new Exception("Folder GIF did not animate");
var timer=(DispatcherTimer)typeof(AudienceWindow).GetField("_slideshowTimer",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!;
if(timer.Interval!=TimeSpan.FromMinutes(1))throw new Exception("Slide interval changed");
settings.BackgroundFolderPath="";settings.BackgroundImagePath="";w.Apply(settings);await Task.Delay(100);
if(AnimationBehavior.GetAnimator(img)!=null)throw new Exception("GIF not released on clear");
File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"result.txt"), $"PASS GIF runtime: normal={normal}, 2x={fast}, 0.5x={slow} frames; original intact; folder animation; 60s slots; disable/clear cleanup.");
} catch(Exception ex) {File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"result.txt"),ex.ToString());Environment.ExitCode=1;}
finally{w.Close();app.Shutdown();}
}; app.Run(w);
}}

