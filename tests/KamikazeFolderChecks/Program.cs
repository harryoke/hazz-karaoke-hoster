using HazzKaraokeHoster.Playback;
using System.IO.Compression;
var root=Path.Combine(Path.GetTempPath(),"HazzKamikazeChecks",Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try {
 if(await KamikazeFolderPicker.PickAsync(root,null)!=null) throw new Exception("Empty folder must not fall back");
 Directory.CreateDirectory(Path.Combine(root,"nested"));
 File.WriteAllBytes(Path.Combine(root,"nested","outside.mp4"),[]);
 File.WriteAllBytes(Path.Combine(root,"ignore.txt"),[]);
 File.WriteAllBytes(Path.Combine(root,"broken.zip"),[]);
 File.WriteAllBytes(Path.Combine(root,"orphan.cdg"),[]);
 if(await KamikazeFolderPicker.PickAsync(root,null)!=null) throw new Exception("Nested or unsupported files selected");
 var only=Path.Combine(root,"one.mp4");File.WriteAllBytes(only,[]);
 if(await KamikazeFolderPicker.PickAsync(root,only)!=only) throw new Exception("Single-song folder must remain usable");
 for(int i=2;i<=4;i++) File.WriteAllBytes(Path.Combine(root,"song"+i+".wav"),[]);
 var cdg=Path.Combine(root,"pair.cdg");File.WriteAllBytes(cdg,[]);File.WriteAllBytes(Path.Combine(root,"pair.mp3"),[]);
 var seen=new HashSet<string>();string? last=null;
 for(int i=0;i<200;i++) {var pick=await KamikazeFolderPicker.PickAsync(root,last);if(pick==null||pick==last||Path.GetDirectoryName(pick)!=root||pick.EndsWith("pair.mp3")) throw new Exception("Invalid random choice or duplicate pair");seen.Add(pick);last=pick;}
 if(seen.Count!=5) throw new Exception("Not exactly five eligible songs");
 using(var zip=ZipFile.Open(Path.Combine(root,"valid.zip"),ZipArchiveMode.Create)) zip.CreateEntry("song.mp3");
 using var cancelled=new CancellationTokenSource();cancelled.Cancel();
 try { await KamikazeFolderPicker.PickAsync(root,null,cancelled.Token);throw new Exception("Cancellation ignored"); }catch(OperationCanceledException){}
 try { await KamikazeFolderPicker.PickAsync(Path.Combine(root,"missing"),null);throw new Exception("Missing folder silently accepted"); }catch(DirectoryNotFoundException){}
 Console.WriteLine("PASS: folder-only selection, five-song pool, pair deduplication, no subfolders/fallback, one-song repeat, invalid ZIP, cancellation and unavailable folder.");
} finally { Directory.Delete(root,true); }
