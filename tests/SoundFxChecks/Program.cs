using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Text.Json;
using HazzKaraokeHoster.App;

class Program
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static void Check(bool ok,string message) { if(!ok) throw new Exception(message);Console.WriteLine("PASS "+message); }
 [STAThread] static int Main()
 {
  var app=new Application();int result=0;
  app.Startup+=async(_,_)=> {try { await Run(); }catch(Exception ex){Console.WriteLine(ex);result=1;}finally{app.Shutdown();}};
  app.Run();return result;
 }
 static async Task Run()
 {
  var config=new SoundFxSettings();config.Normalize();Check(config.Pads.Count==9 && config.KamikazeSlot==-1,"nine pads; Kamikaze off by default");
  config.Pads[2].Label="Applause";config.Pads[2].Colour="#ffee00";config.Pads[2].FilePath="C:/test.wav";config.KamikazeSlot=2;
  var copy=JsonSerializer.Deserialize<SoundFxSettings>(JsonSerializer.Serialize(config))!;copy.Normalize();Check(copy.Pads[2].Label=="Applause" && copy.KamikazeSlot==2,"configuration round trip preserves labels and Kamikaze selection");
  var deck=new RoutedMusicElement { Volume=.7,Attenuation=.2 };
  Check(Math.Abs(((MediaElement)deck).Volume-.14)<.001 && deck.Volume==.7,"ducking is separate from stored fader volume");
  deck.Volume=.3;deck.Attenuation=1;Check(Math.Abs(((MediaElement)deck).Volume-.3)<.001,"restore respects fader changes while ducked");
  deck.Volume=0;deck.Attenuation=0;deck.Attenuation=1;Check(deck.Volume==0,"duck restore does not unmute stopped deck");
  var host=(MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
  void Set(string name,object value)=>typeof(MainWindow).GetField(name,Flags)!.SetValue(host,value);
  object? Call(string name,params object[] values)=>typeof(MainWindow).GetMethod(name,Flags)!.Invoke(host,values);
  Set("_soundFx",config);Set("SoundFxButtons",new WrapPanel());Set("SoundFxPlayerHost",new Grid());Set("SearchStatus",new TextBlock());
  var a=new RoutedMusicElement { Volume=.5 };var b=new RoutedMusicElement { Volume=.4 };var quick=new RoutedMusicElement { Volume=.8 };
  Set("DeckAMedia",a);Set("DeckBMedia",b);Set("QuickMusicMedia",quick);
  await (Task)Call("PlaySoundFxAsync",2,false)!;Check(a.Attenuation==1,"missing file leaves music untouched");
  var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid()+".wav");
  System.IO.File.WriteAllText(path,"invalid audio");config.Pads[0].FilePath=path;config.OutputDeviceId="NONEXISTENT-HAZZ-FX-TEST";
  a.Attenuation=b.Attenuation=quick.Attenuation=0;
  await (Task)Call("PlaySoundFxAsync",0,true)!;
  Check(a.Attenuation==1 && b.Attenuation==1 && quick.Attenuation==1,"failed output restores all music duck levels");
  a.Attenuation=b.Attenuation=quick.Attenuation=0;
  var stop1=(Task)Call("StopSoundFxAsync")!;await Task.Delay(30);var stop2=(Task)Call("StopSoundFxAsync")!;
  await Task.WhenAll(stop1,stop2);Check(a.Attenuation==1 && a.Volume==.5,"repeated stop cancels stale fade and restores current fader");
  // Exercise real opening/end events with digital silence; never play a test tone.
  var silent=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid()+".wav");
  using(var writer=new NAudio.Wave.WaveFileWriter(silent,new NAudio.Wave.WaveFormat(44100,16,1))) writer.Write(new byte[176400],0,176400);
  config.Pads[0].FilePath=silent;config.Pads[0].Volume=0;config.OutputDeviceId=null;
  var grid=(Grid)typeof(MainWindow).GetField("SoundFxPlayerHost",Flags)!.GetValue(host)!;
  var window=new Window { Content=grid,Width=100,Height=100,ShowActivated=false,ShowInTaskbar=false,Opacity=0 };
  window.Show();
  var play=(Task)Call("PlaySoundFxAsync",0,true)!;
  for(int i=0;i<100 && (int)typeof(MainWindow).GetField("_soundFxPlaying",Flags)!.GetValue(host)!<0 && !play.IsCompleted;i++) await Task.Delay(100);
  Check(a.Attenuation==0 && !play.IsCompleted,"valid soundbite starts after music fades down");
  a.Volume=.25;await play;Check(a.Attenuation==1 && a.Volume==.25,"natural soundbite end restores current music level");
  var first=(Task)Call("PlaySoundFxAsync",0,false)!;await Task.Delay(80);var replacement=(Task)Call("PlaySoundFxAsync",0,false)!;
  await Task.WhenAll(first,replacement);Check(a.Attenuation==1 && grid.Children.Count==0,"rapid replacement disposes old player and restores music");
  Call("DisposeSoundFx");window.Close();System.IO.File.Delete(silent);System.IO.File.Delete(path);deck.Close();a.Close();b.Close();quick.Close();
 }
}
