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
}catch(Exception ex){results.Add(ex.ToString());}
File.WriteAllLines(args[1],results);
}}
