using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HazzKaraokeHoster.App;
class Check
{
 static int count;
 static void Require(bool ok,string text) { if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);count++; }
 [STAThread] static void Main(string[] args)
 {
  var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
  app.Startup+=async(_,_)=>{try{await Run(Path.GetFullPath(args[0]));Console.WriteLine($"ALL {count} CHECKS PASSED");}catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}finally{app.Shutdown();}};
  app.Run();
 }
 static async Task Run(string root)
 {
  Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(root,"B folder"));Directory.CreateDirectory(Path.Combine(root,"A folder"));
  foreach(string name in new[]{"Artist - Song.MP3","video.mp4","karaoke.cdg","karaoke.zip","notes.txt","audio.flac"})File.WriteAllText(Path.Combine(root,name),"fixture");
  Require(LocalFileCatalog.Folders(root,CancellationToken.None).Select(Path.GetFileName).SequenceEqual(new[]{"A folder","B folder"}),"folder tree sorts child folders without recursively scanning their contents");
  var listing=LocalFileCatalog.Files(root,"",CancellationToken.None);
  Require(listing.Files.Length==5 && listing.Error is null,"audio/video/CDG/ZIP files listed; unrelated documents omitted");
  Require(LocalFileCatalog.Files(root,"SONG",CancellationToken.None).Files.Single().Name=="Artist - Song.MP3","filename filtering is case insensitive");
  Require(LocalFileCatalog.Files(Path.Combine(root,"missing"),"",CancellationToken.None).Error is not null,"missing/disconnected folders return a controlled message");
  Require(LocalFileCatalog.Supported("x.MP3",true) && LocalFileCatalog.Supported("x.mp4",true) && !LocalFileCatalog.Supported("x.cdg",true) && !LocalFileCatalog.Supported("x.zip",true),"music drops accept audio/video and reject karaoke containers/graphics");
  Require(LocalFileCatalog.Supported("x.cdg") && LocalFileCatalog.Supported("x.zip") && !LocalFileCatalog.Supported(null),"karaoke/file browser support includes CDG and ZIP");
  using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;
  try{LocalFileCatalog.Files(root,"",cancelled.Token);}catch(OperationCanceledException){stopped=true;}
  Require(stopped,"directory enumeration honours cancellation");
  var music=LocalFileCatalog.ExistingMusicFiles(listing.Files.Select(x=>x.FullPath).Append(Path.Combine(root,"gone.mp3")),CancellationToken.None);
  Require(music.Length==3,"drop validation skips unsupported and disappeared files");
  var data=new DataObject(DataFormats.FileDrop,music);
  Require(((string[])data.GetData(DataFormats.FileDrop)).SequenceEqual(music),"browser payload is standard Windows file-drop data");
  Require(LocalFileCatalog.SafeInsertIndex(0,3,0)==1 && LocalFileCatalog.SafeInsertIndex(99,3,-1)==3,"drop positions cannot precede a playing row or exceed queue bounds");
  var host=new MainWindow();var current=MusicQueueItem.FromPath("current.mp3");current.IsNowPlaying=true;host.Current=current;host.DeckAPlaylist.Items.Add(current);
  var song=Path.Combine(root,"Artist - Song.MP3");await host.TestDrop(new[]{song,Path.Combine(root,"karaoke.zip"),Path.Combine(root,"gone.mp3")});
  Require(ReferenceEquals(host.DeckAPlaylist.Items[0],current) && host.DeckAPlaylist.Items.Count==2,"production drop insertion protects the current track");
  Require(((MusicQueueItem)host.DeckAPlaylist.Items[1]).FilePath==song && host.Dirty && host.Status.Contains("skipped 2"),"production insertion persists queue changes and reports skipped files");
  await host.TestDrop(new[]{song,song},deck2:true);
  Require(host.DeckBPlaylist.Items.Count==2 && ((MusicQueueItem)host.DeckBPlaylist.Items[1]).Number==2,"second deck receives multiple copied queue entries including repeats");
  int before=host.DeckAPlaylist.Items.Count;await host.TestDrop(Enumerable.Repeat(song,501).ToArray());Require(host.DeckAPlaylist.Items.Count==before,"oversized drops are rejected without partially changing queues");host.Close();
  var window=new LocalFilesWindow { ShowInTaskbar=false,ShowActivated=false,Opacity=0 };window.Show();
  await window.OpenFolderAsync(root);var grid=(DataGrid)window.FindName("FilesGrid");Require(grid.Items.Count==5,"production browser window loads folder files asynchronously");
  var tree=(TreeView)window.FindName("FolderTree");
  var makeNode=typeof(LocalFilesWindow).GetMethod("Node",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
  var folderNode=(TreeViewItem)makeNode.Invoke(null,new object?[]{root,"Test music folder"})!;tree.Items.Add(folderNode);
  Require(folderNode.Items.Count==1 && ((TreeViewItem)folderNode.Items[0]).Tag is null,"folder nodes start with a lazy expansion placeholder");
  folderNode.IsExpanded=true;
  for(int i=0;i<100 && (folderNode.Items.Count!=2 || ((TreeViewItem)folderNode.Items[0]).Tag is null);i++)await Task.Delay(10);
  Require(folderNode.Items.Count==2 && ((TreeViewItem)folderNode.Items[0]).Header.ToString()=="A folder","expanding a production tree node asynchronously loads its child folders");
  folderNode.IsSelected=true;await Task.Delay(30);
  var slow=window.OpenFolderAsync(Path.Combine(root,"B folder"));var latest=window.OpenFolderAsync(root);await Task.WhenAll(slow,latest);
  Require(grid.Items.Count==5,"a stale folder result cannot overwrite a newer navigation");
  await window.OpenFolderAsync(Path.Combine(root,"missing"));Require(grid.Items.Count==0 && ((TextBlock)window.FindName("StatusText")).Text.Contains("unavailable"),"failed navigation clears stale file rows");
  await window.OpenFolderAsync(root);((TextBox)window.FindName("FilterBox")).Text="Song";await window.OpenFolderAsync(root);Require(grid.Items.Count==1,"browser filename filter works through the real controls");
  ((TextBox)window.FindName("FilterBox")).Text="";await window.OpenFolderAsync(root);window.Opacity=1;window.UpdateLayout();
  var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var output=File.Create(Path.Combine(root,"browser-preview.png")))encoder.Save(output);
  window.Opacity=0;var closing=window.OpenFolderAsync(root);window.Close();await closing;Require(true,"closing the browser cancels navigation without a disposed-token error");
  var large=Path.Combine(root,"large");Directory.CreateDirectory(large);
  for(int i=0;i<5002;i++)File.WriteAllText(Path.Combine(large,$"track{i:00000}.mp3"),"");
  var bounded=LocalFileCatalog.Files(large,"",CancellationToken.None);Require(bounded.Files.Length==5000 && bounded.MatchingCount==5002,"large folders have a bounded visible file list with an accurate match count");
  Require(LocalFileCatalog.Files(large,"track05001",CancellationToken.None).Files.Length==1,"filtering can find a file beyond the visible listing limit");
 }
}
