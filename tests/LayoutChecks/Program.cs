using System.IO;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
class Check {
[STAThread] static void Main(string[] args) {
var app=new Application(); app.Resources[typeof(ListBoxItem)]=new Style(typeof(ListBoxItem));
var results=new List<string>();
try {
var doc=XDocument.Load(args[0]);XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
doc.Root!.Attribute(x+"Class")?.Remove();
doc.Root.SetAttributeValue(XNamespace.Xmlns+"local", "clr-namespace:HazzKaraokeHoster.App;assembly=Hazz Karaoke Hoster");
foreach(var el in doc.Descendants().Where(el=>el.Name.NamespaceName=="clr-namespace:HazzKaraokeHoster.App"))
    el.Name=XName.Get(el.Name.LocalName,"clr-namespace:HazzKaraokeHoster.App;assembly=Hazz Karaoke Hoster");
foreach(var el in doc.Descendants().ToList()) {
if(el.Name.LocalName=="RoutedMusicElement") el.Name=el.Name.NamespaceName.Contains("clr-namespace") ? XName.Get("MediaElement","http://schemas.microsoft.com/winfx/2006/xaml/presentation") : el.Name;
if(el.Name.LocalName=="EventSetter"){el.Remove();continue;}
var t=typeof(Control).Assembly.GetType("System.Windows.Controls."+el.Name.LocalName) ?? typeof(Window).Assembly.GetType("System.Windows."+el.Name.LocalName);
foreach(var a in el.Attributes().ToList()) {
if(a.Name.LocalName.Contains("TextChanged") || t?.GetEvent(a.Name.LocalName)!=null || a.Value.Contains("_Click") || a.Value=="OverlayChanged")a.Remove();
}
if(el.Name.LocalName=="Image" && el.Attribute("Source")!=null)el.Attribute("Source")!.Remove();
}
var w=(Window)XamlReader.Parse(doc.ToString());w.Opacity=0;w.ShowActivated=false;w.ShowInTaskbar=false;
double textScale=args.Length>2?double.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture):1;
foreach(var key in w.Resources.Keys.OfType<string>().Where(k=>k.StartsWith("HostFont")).ToArray())
    w.Resources[key]=double.Parse(key[8..].Replace('_','.'),System.Globalization.CultureInfo.InvariantCulture)*textScale;
w.Resources["HostRowHeight"]=32*textScale;
w.Show();
((DataGrid)w.FindName("QueueList")).Items.Add(new {StatusText="Ready",SingerName="Sample singer",NextSongTitle="Example song",NextArtist="Example artist",SongCount=2,NextKey=0,NextSync=0.0});
var viewport=(Grid)w.FindName("HostViewport");var layout=(Grid)w.FindName("HostLayout");
foreach(var name in new[]{"DeckAPlaylist","DeckBPlaylist","QueueList"}) {
var control=(Control)w.FindName(name); double expected=(name=="QueueList"?14:13)*textScale;
if(Math.Abs(control.FontSize-expected)>0.01)throw new Exception(name+" did not adopt text size");
}
foreach(var name in new[]{"DeckAPlayerRow","DeckBPlayerRow","KaraokeDeckRow"})((RowDefinition)w.FindName(name)).Height=GridLength.Auto;
IEnumerable<DependencyObject> Walk(DependencyObject o){yield return o;foreach(var c in LogicalTreeHelper.GetChildren(o).OfType<DependencyObject>())foreach(var d in Walk(c))yield return d;}
foreach(var size in new[]{(1920,1080),(1366,768),(1024,768),(800,600),(853,480)}) {
w.Width=size.Item1;w.Height=size.Item2;w.UpdateLayout();double width=viewport.ActualWidth,height=viewport.ActualHeight,scale=Math.Min(1,Math.Min(width/1696,height/1116));layout.Width=Math.Max(1680,width/scale-16);layout.Height=Math.Max(1100,height/scale-16);w.UpdateLayout();
w.Dispatcher.Invoke(()=>{}, System.Windows.Threading.DispatcherPriority.ApplicationIdle); w.UpdateLayout();
var failures=new List<string>();int count=0;
foreach(var b in Walk(layout).OfType<Button>().Where(b=>b.IsVisible)) {
count++;var rect=b.TransformToAncestor(viewport).TransformBounds(new Rect(b.RenderSize));
if(rect.Left < -1 || rect.Top < -1 || rect.Right>width+1 || rect.Bottom>height+1)failures.Add(b.Content?.ToString()??"button");
for(DependencyObject? p=VisualTreeHelper.GetParent(b);p!=null && p!=viewport;p=VisualTreeHelper.GetParent(p))if(p is ScrollViewer sv && sv.IsVisible) {var local=b.TransformToAncestor(sv).TransformBounds(new Rect(b.RenderSize));if(local.Bottom>sv.ActualHeight+1 || local.Right>sv.ActualWidth+1)failures.Add("scroll-clipped "+b.Content);}
}
if(size.Item1==1366){var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)width,(int)height,96,96,PixelFormats.Pbgra32);bitmap.Render(viewport);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var fs=File.Create(Path.Combine(Path.GetDirectoryName(args[1])!,"layout-preview.png"));png.Save(fs);}
results.Add($"{size}: {count} buttons, "+(failures.Count==0?"PASS":string.Join(",",failures)));
}
w.Close();
var host = new HazzKaraokeHoster.App.MainWindow();
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var hostType = host.GetType();
var first = new HazzKaraokeHoster.App.MusicQueueItem { FilePath = "deck1.wav", IsNowPlaying = true };
var second = new HazzKaraokeHoster.App.MusicQueueItem { FilePath = "deck2.wav", IsNowPlaying = true };
hostType.GetField("_deck1CurrentItem", flags)!.SetValue(host, first);
hostType.GetField("_deck2CurrentItem", flags)!.SetValue(host, second);
var deckA = (HazzKaraokeHoster.App.RoutedMusicElement)host.FindName("DeckAMedia");
var deckB = (HazzKaraokeHoster.App.RoutedMusicElement)host.FindName("DeckBMedia");
var volumeA = ((Slider)host.FindName("DeckAVolume")).Value;
var volumeB = ((Slider)host.FindName("DeckBVolume")).Value;
var fader = (Slider)host.FindName("VisualCrossfaderSlider");
var auto = (CheckBox)host.FindName("AutoCrossfadeCheck");
hostType.GetField("_crossfadeActive", flags)!.SetValue(host, true);
foreach (var position in new[] { 0d, 0.5, 1d, 0d })
{
    hostType.GetMethod("SetManualCrossfader", flags)!.Invoke(host, [position]);
    if (auto.IsChecked != false || (bool)hostType.GetField("_crossfadeActive", flags)!.GetValue(host)! ||
        Math.Abs(deckA.Volume - volumeA * (1 - position)) > 0.001 ||
        Math.Abs(deckB.Volume - volumeB * position) > 0.001 ||
        Math.Abs(fader.Value - position * 100) > 0.001 || !first.IsNowPlaying || !second.IsNowPlaying)
        throw new Exception("Manual fader must change gains without ending either track.");
}
var deck2 = Enum.Parse(hostType.GetNestedType("MusicDeckId", System.Reflection.BindingFlags.NonPublic)!, "Deck2");
hostType.GetMethod("HandleMusicDeckEnded", flags)!.Invoke(host, [deck2]);
if (!first.IsNowPlaying || second.IsNowPlaying || deckA.Volume != volumeA || deckB.Volume != 0)
    throw new Exception("Ending a muted deck changed the playing deck or fader.");
results.Add("Manual crossfade endpoints, midpoint, automatic takeover and independent track end: PASS");
}catch(Exception ex){results.Add(ex.ToString());}
File.WriteAllLines(args[1],results);
if(results.Any(r=>!r.EndsWith("PASS")))Environment.ExitCode=1;
}}
