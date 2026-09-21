using System.IO;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HazzKaraokeHoster.App;

internal static class Check
{
 [STAThread] static int Main(string[] args)
 {
  try { Run(args); return 0; } catch(Exception e) { Console.WriteLine(e); return 1; }
 }
 static void Require(bool pass, string message) { if(!pass) throw new Exception(message); }
 static IEnumerable<DependencyObject> Walk(DependencyObject o)
 {
  yield return o;
  foreach(var c in LogicalTreeHelper.GetChildren(o).OfType<DependencyObject>())
   foreach(var d in Walk(c)) yield return d;
 }
 static bool Available(DependencyObject o)
 {
  for(DependencyObject? p=o;p!=null;p=LogicalTreeHelper.GetParent(p))
   if(p is UIElement e && e.Visibility != Visibility.Visible) return false;
  return true;
 }
 static void Run(string[] args)
 {
  var app = new Application();
  // Simulate shutdown before initialization without constructing a live host,
  // opening its database, or running its startup handlers. The guarded methods
  // must return before touching any uninitialized controls or persistence.
  var earlyHost=(MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
  foreach(var method in new[]{"SaveMainLayout","SaveMusicDeckQueuesNow","SaveLiveShowStateNow"})
   typeof(MainWindow).GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
    .Invoke(earlyHost,method=="SaveLiveShowStateNow"?new object[]{true}:null);
  Console.WriteLine("PASS: early shutdown cannot save empty interface, playlist or singer recovery state.");
  var doc=XDocument.Load(args[0]); XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
  doc.Root!.Attribute(x+"Class")!.Remove();
  // Render the production view without connecting live-show event handlers,
  // databases, device outputs or the user's saved settings.
  foreach(var el in doc.Descendants().ToArray())
  {
   if(el.Name.LocalName=="EventSetter") { el.Remove();continue; }
   foreach(var a in el.Attributes().ToArray())
    if(!a.IsNamespaceDeclaration && a.Name.NamespaceName=="" &&
       (System.Text.RegularExpressions.Regex.IsMatch(a.Value,@"^[A-Za-z][A-Za-z0-9]*_[A-Za-z0-9_]+$") || a.Value=="OverlayChanged" || a.Value=="CdgPresentationChanged")) a.Remove();
   if(el.Name.LocalName=="Image" && el.Attribute("Source") is {} source)
    source.Value=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(args[0])!, source.Value));
  }
  var markup=doc.ToString().Replace("clr-namespace:HazzKaraokeHoster.App\"", "clr-namespace:HazzKaraokeHoster.App;assembly=Hazz Karaoke Hoster\"");
  var w=(Window)XamlReader.Parse(markup);
  T Find<T>(string n) => (T)w.FindName(n);
  var viewport=Find<Grid>("HostViewport"); var layout=Find<Grid>("HostLayout");
  var all=Walk(layout).OfType<Control>().ToArray();
  foreach(var name in new[]{"DeckAPlaylist","DeckBPlaylist"})
   for(int i=0;i<7;i++) Find<ListBox>(name).Items.Add(new { NumberText=(i+1).ToString(), DisplayArtist=new[]{"ABBA","Queen","The Killers"}[i%3], DisplayTitle=new[]{"Dancing Queen","Don't Stop Me Now","Mr. Brightside"}[i%3], DurationText="03:45", IsFavourite=i==0, IsNowPlaying=i==1, IsPlayedThisSession=i==2 });
  for(int i=0;i<5;i++) Find<DataGrid>("QueueList").Items.Add(new { SingerName=new[]{"Alex","Sam","Chris","Taylor","Jamie"}[i], StatusText="Ready", NextSongTitle="Example karaoke song", NextArtist="Example artist", SongCount=2, NextKey=0, NextSync=0d });
  var engine=new ConsoleSkinLayout(w);
  if(args.Contains("--render-stress"))
  {
   var audience=new AudienceWindow();
   var preview=Find<AudiencePreview>("FullAudiencePreview");
   if(!args.Contains("--no-preview")) preview.GetAudience=()=>audience;
   var folder=args.First(x=>x.StartsWith("--assets=")).Substring(9);
   var assets=Directory.GetFiles(folder).OrderBy(x=>x).ToArray();
   w.Width=1100;w.Height=720;w.ShowActivated=false;w.ShowInTaskbar=false;w.Show();
   var frame=new System.Windows.Threading.DispatcherFrame();int ticks=0;
   var timer=new System.Windows.Threading.DispatcherTimer { Interval=TimeSpan.FromMilliseconds(500) };
   timer.Tick+=(_,_)=> {
    engine.Apply(new[]{"Classic","Midnight","Copper","Daylight"}[ticks%4],false,false);
    if(ticks%6==0) {
     var path=assets[(ticks/6)%assets.Length];Console.WriteLine("ASSET: "+path);
     audience.Apply(new HazzKaraokeHoster.Core.Models.AudienceOverlaySettings { BackgroundImageEnabled=true,BackgroundImagePath=path, ScrollerText="WELCOME TO KARAOKE WITH HAZZ • Want To Sing ... Just Tell Me Your Name & The Song You Would Like To Perform...",ScrollerFontFamily="Coolsville",ScrollerFontSize=42, SecondScrollerEnabled=true,SecondScrollerText="Hazz is cool....",SecondScrollerFontFamily="Alex Brush",SecondScrollerFontSize=62 });
    }
    w.Width=1100+(ticks%2)*100;preview.Refresh();
    if(ticks%6==5) {
     Directory.CreateDirectory(args[1]);
     var capture=new RenderTargetBitmap(800,450,96,96,PixelFormats.Pbgra32);
     var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()) dc.DrawRectangle(new VisualBrush(preview),null,new Rect(0,0,800,450));
     capture.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(capture));
     using var output=File.Create(Path.Combine(args[1],"asset-"+(ticks/6)+".png"));encoder.Save(output);
    }
    if(++ticks>=60){timer.Stop();frame.Continue=false;}
   };
   timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);
   preview.Dispose();audience.Close();w.Close();
   Console.WriteLine("PASS: 60 live skin/size changes with image/GIF/video backgrounds.");return;
  }

  Directory.CreateDirectory(args[1]);
  var records=new List<string>();
  foreach(var skin in new[]{"Classic","Midnight","Copper","Daylight","Classic","Copper","Midnight","Daylight","Classic"})
  foreach(var scale in new[]{1d,1.5})
  foreach(var mode in new[]{"Normal","Single","Karaoke","Focus"})
  {
   bool focus=mode=="Focus", single=mode is "Single" or "Focus", karaoke=mode=="Karaoke";
   var singers=Find<Border>("SingerListPanel");
   if(singers.Parent is Panel singerParent) singerParent.Children.Remove(singers);
   Find<Grid>(focus?"KaraokeFocusSingerHost":"SingerListHome").Children.Add(singers);
   Find<Grid>("KaraokeFocusSingerHost").Visibility=focus?Visibility.Visible:Visibility.Collapsed;
   Find<RowDefinition>("SingerListRow").MinHeight=focus?0:105;
   Find<RowDefinition>("SingerListRow").Height=focus?new GridLength(0):new GridLength(1,GridUnitType.Star);
   Find<Grid>("MusicDeckAPanel").Visibility=karaoke?Visibility.Collapsed:Visibility.Visible;
   Find<Grid>("MusicDeckBPanel").Visibility=karaoke||focus?Visibility.Collapsed:Visibility.Visible;
   Find<Border>("DeckBPlayerControlsPanel").Visibility=single?Visibility.Collapsed:Visibility.Visible;
   Find<Border>("DeckBSideListControlsPanel").Visibility=single?Visibility.Visible:Visibility.Collapsed;
   engine.Apply(skin,karaoke,single);
   Require(all.ToHashSet().SetEquals(Walk(layout).OfType<Control>()),skin+": control instances changed");
   if(focus) Require(Grid.GetColumn(Find<Grid>("KaraokeFocusSingerHost"))==Grid.GetColumn(Find<Grid>("MusicDeckBPanel")) && Grid.GetRow(Find<Grid>("KaraokeFocusSingerHost"))==Grid.GetRow(Find<Grid>("MusicDeckBPanel")), "Focus overlaps a different skin panel");
   foreach(var key in w.Resources.Keys.OfType<string>().Where(k=>k.StartsWith("HostFont")).ToArray())
    w.Resources[key]=double.Parse(key[8..].Replace('_','.'),System.Globalization.CultureInfo.InvariantCulture)*scale;
   foreach(var n in new[]{"DeckAPlayerRow","DeckBPlayerRow","KaraokeDeckRow"}) Find<RowDefinition>(n).Height=GridLength.Auto;
   foreach(var size in new[]{(1920,1080),(1366,768),(1024,768)})
   {
    double fit=Math.Min(1,Math.Min(size.Item1/1696d,size.Item2/1116d));
    layout.Width=Math.Max(1680,size.Item1/fit-16);layout.Height=Math.Max(1100,size.Item2/fit-16);
    viewport.Measure(new Size(size.Item1,size.Item2));viewport.Arrange(new Rect(0,0,size.Item1,size.Item2));viewport.UpdateLayout();
    var faults=new List<string>();
    foreach(var b in Walk(layout).OfType<Button>().Where(Available))
    {
     var bounds=b.TransformToAncestor(viewport).TransformBounds(new Rect(b.RenderSize));
     if(bounds.Left < -1 || bounds.Top < -1 || bounds.Right > size.Item1+1 || bounds.Bottom > size.Item2+1 || bounds.Width<1 || bounds.Height<1)
      faults.Add(b.Content+": outside viewport");
     for(var parent=VisualTreeHelper.GetParent(b);parent!=null && parent!=viewport;parent=VisualTreeHelper.GetParent(parent))
      if(parent is ScrollViewer sv && Available(sv))
      { var r=b.TransformToAncestor(sv).TransformBounds(new Rect(b.RenderSize)); if(r.Bottom>sv.ActualHeight+2 || r.Right>sv.ActualWidth+2) faults.Add(b.Content+": scroll clipped"); }
    }
    Require(faults.Count==0,$"{skin}/{mode}/{scale}/{size}: "+string.Join(",",faults));
    if(scale==1 && mode=="Normal" && size.Item1==1920)
    {
     var bitmap=new RenderTargetBitmap(size.Item1,size.Item2,96,96,PixelFormats.Pbgra32); bitmap.Render(viewport);
     var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(args[1],skin+".png"));png.Save(file);
    }
   }
   records.Add($"{skin}/{mode}/{scale}: PASS — same {all.Length} controls, 1920/1366/1024 widths");
  }
  // Inspect the actual audience settings popup without running host startup.
  engine.Apply("Classic",false,false);
  Find<CheckBox>("SecondScrollerCheck").IsChecked=true;
  Find<TextBox>("SecondScrollerTextBox").Text="VENUE DRINKS OFFERS • ASK AT THE BAR";
  foreach(var name in new[]{"ScrollerFontCombo","SecondScrollerFontCombo"}) { Find<ComboBox>(name).Items.Add("Segoe UI"); Find<ComboBox>(name).SelectedIndex=0; }
  var settingsFit=Find<Border>("AudienceSettingsPanel");
  var scroll=Find<ScrollViewer>("AudienceSettingsScroll");
  foreach(var scale in new[]{1d,1.5})
  {
   foreach(var key in w.Resources.Keys.OfType<string>().Where(k=>k.StartsWith("HostFont")).ToArray())
    w.Resources[key]=double.Parse(key[8..].Replace('_','.'),System.Globalization.CultureInfo.InvariantCulture)*scale;
   settingsFit.Width=900;settingsFit.MaxHeight=500;
   settingsFit.Measure(new Size(1366,728));settingsFit.Arrange(new Rect(settingsFit.DesiredSize));settingsFit.UpdateLayout();
   var border=settingsFit;
   Require(scroll.ScrollableHeight>0,"Small-screen settings must scroll rather than shrink");
   Require(settingsFit.ActualHeight<=500,"Settings exceed small-screen bounds");
   foreach(var name in new[]{"TransparentCdgCheck","CdgColourCombo","CdgBackgroundOpacitySlider","CdgLyricsOpacitySlider","CloseAudienceSettingsButton","KaraokeSizingCombo","ScrollerRotationCheck","ScrollerMessageCheck","ScrollerSizeSlider","SecondScrollerTextBox","SecondScrollerFontCombo","SecondScrollerSizeSlider","SecondScrollerSpeedSlider","SecondScrollerInsetSlider","SecondScrollerColorButton","BackgroundGifSpeedSlider"})
   {
    var control=Find<FrameworkElement>(name);
    if(name!="CloseAudienceSettingsButton") {
     var content=(FrameworkElement)scroll.Content;
     var y=control.TransformToAncestor(content).Transform(new Point()).Y;
     scroll.ScrollToVerticalOffset(y);settingsFit.UpdateLayout();
    }
    var b=control.TransformToAncestor(border).TransformBounds(new Rect(control.RenderSize));
    Require(Math.Abs(b.Height-control.ActualHeight)<0.1,name+" was shrunk");
    Require(b.Width>0 && b.Left>=0 && b.Right<=border.ActualWidth+1 && b.Top>=0 && b.Bottom<=border.ActualHeight+1,name+" clipped in settings at "+scale);
   }
   scroll.ScrollToTop();settingsFit.UpdateLayout();
   var bitmap=new RenderTargetBitmap((int)Math.Ceiling(settingsFit.ActualWidth),(int)Math.Ceiling(settingsFit.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(settingsFit);
   var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(args[1],$"AudienceSettings-{scale}.png"));png.Save(file);
  }
  Console.WriteLine("PASS: new audience controls and existing GIF controls fit at both GUI text sizes.");
  File.WriteAllLines(Path.Combine(args[1],"checks.txt"),records);
  Console.WriteLine(string.Join(Environment.NewLine,records));
 }
}
