using System.IO;
using System.IO.Compression;
using HazzKaraokeHoster.Core.Models;
using HazzKaraokeHoster.Data;
using HazzKaraokeHoster.Playback;
using Microsoft.Data.Sqlite;
using NAudio.Wave;

static class Program
{
    static void Check(bool pass, string name) { if (!pass) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static async Task Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "hazz-v19-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var db = new HazzDatabase(Path.Combine(root,"test.db")); await db.InitializeAsync(); var repo = new LibraryRepository(db);
            SongRecord Song(string artist,string title,string name,string kind="Karaoke") => new(0,artist,title,"Sunfly","SF1",Path.Combine(root,name),Path.GetExtension(name),3,null,1.25,2,kind,180);
            var current = Song("Elton John","Your Song","original.cdg"); await repo.UpsertSongAsync(current);
            await repo.UpsertSongAsync(Song("John, Elton","Your Song","reverse.cdg"));
            await repo.UpsertSongAsync(Song("Your Song","Elton John","swapped.cdg"));
            await repo.UpsertSongAsync(Song("Elton John","Your Song (Live)","live.cdg"));
            await repo.UpsertSongAsync(Song("Someone Else","Your Song","other.cdg"));
            await repo.UpsertSongAsync(Song("Elton John","Rocket Man","other-song.cdg"));
            var alternatives = await repo.FindAlternativesAsync(current.Artist,current.Title,current.FilePath);
            Check(alternatives.Count==3,"alternatives include reversed artist, swapped fields and version suffix, exclude other song/artist/current");
            Check(AlternativeMatch.Score("Beyoncé","Halo",Song("Beyonce","Halo","x"))==1,"accent-insensitive matching");
            var singerSong = new SingerSongEntry { KeyChange=3,CdgSyncSeconds=1.5,SongTitle="Old", FilePath="old" }; var identity=singerSong.Id;
            singerSong.ReplaceRecording(alternatives[0]);
            Check(singerSong.KeyChange==3 && singerSong.CdgSyncSeconds==1.5 && singerSong.Id==identity,"replacement retains queued identity/key/sync");
            var valid = Path.Combine(root,"valid.zip");
            using(var zip=ZipFile.Open(valid,ZipArchiveMode.Create)) foreach(var name in new[]{"song.cdg","song.mp3"}) { using var w = new StreamWriter(zip.CreateEntry(name).Open()); w.Write("abc"); }
            await repo.UpsertSongAsync(Song("Test","Valid ZIP","valid.zip"));
            Check(LibraryHealthService.InspectFile(valid,"Karaoke",default)==null,"valid ZIP streams and CRC pass without extraction");
            var corrupt=Path.Combine(root,"corrupt.zip");
            using(var zip=ZipFile.Open(corrupt,ZipArchiveMode.Create)) { using var w=new StreamWriter(zip.CreateEntry("song.cdg",CompressionLevel.NoCompression).Open()); w.Write("abcdefg"); }
            var bytes=File.ReadAllBytes(corrupt); var at=30+BitConverter.ToUInt16(bytes,26)+BitConverter.ToUInt16(bytes,28); bytes[at]=(byte)'z'; File.WriteAllBytes(corrupt,bytes);
            bool bad=false;try { LibraryHealthService.InspectFile(corrupt,"Karaoke",default); } catch(InvalidDataException) { bad=true; }
            Check(bad,"ZIP CRC corruption detected");
            File.WriteAllText(Path.Combine(root,"orphan.cdg"),"abc"); await repo.UpsertSongAsync(Song("Test","No audio","orphan.cdg"));
            File.WriteAllText(Path.Combine(root,"orphan.mp3"),"abc"); await repo.UpsertSongAsync(Song("Test","No graphics","lonely.mp3"));File.WriteAllText(Path.Combine(root,"lonely.mp3"),"abc");
            File.WriteAllText(Path.Combine(root,"audioonly.mp3"),"abc");await repo.UpsertSongAsync(Song("Test","Music audio","audioonly.mp3","Music"));
            var health = new LibraryHealthService(db);var issues=await health.ScanAsync(null,default);
            Check(issues.Any(i=>i.Problem=="Missing file") && issues.Any(i=>i.Problem=="Possible duplicate") && issues.Any(i=>i.Problem=="Missing CDG partner"),"health findings identify missing/duplicate/missing CDG");
            Check(!issues.Any(i=>i.Path.EndsWith("audioonly.mp3")),"normal music MP3 needs no CDG");
            File.Delete(Path.Combine(root,"orphan.mp3"));Check(LibraryHealthService.InspectFile(Path.Combine(root,"orphan.cdg"),"Karaoke",default)?.Kind=="Missing audio partner","missing audio partner");
            using(var cancelled=new CancellationTokenSource()) { cancelled.Cancel(); bool caught=false;try{await health.ScanAsync(null,cancelled.Token);}catch(OperationCanceledException){caught=true;}Check(caught,"scan cancellation"); }
            var old=Path.Combine(root,"old");var next=Path.Combine(root,"new");Directory.CreateDirectory(next);var oldPath=Path.Combine(old,"move.mp3");var newPath=Path.Combine(next,"move.mp3");File.WriteAllText(newPath,"abc");
            var id=await repo.UpsertSongAsync(Song("Move","Track","move.mp3","Music") with {FilePath=oldPath});
            Check(LibraryHealthService.MappedPath(oldPath,old,next)==newPath && LibraryHealthService.MappedPath(Path.Combine(root,"older","x.mp3"),old,next)==null,"mapping honours folder boundaries");
            await using(var conn=new SqliteConnection(db.ConnectionString)) { await conn.OpenAsync(); using var cmd=conn.CreateCommand();cmd.CommandText="INSERT INTO singers(display_name) VALUES('Tester'); INSERT INTO singer_history(singer_id,song_id,file_path,sung_at_utc) VALUES(1,$id,$path,CURRENT_TIMESTAMP);";cmd.Parameters.AddWithValue("$id",id);cmd.Parameters.AddWithValue("$path",oldPath);await cmd.ExecuteNonQueryAsync(); }
            await health.ApplyRelinksAsync(new[]{new LibraryRelink(id,oldPath,newPath)},default);
            Check((await repo.FindByFilePathAsync(newPath))?.Id==id,"relink keeps song ID");
            Check((await repo.SearchByKindAsync("Move Track","Music")).Any(s=>s.FilePath==newPath),"relink updates FTS search");
            await using(var conn=new SqliteConnection(db.ConnectionString)) {await conn.OpenAsync();using var cmd=conn.CreateCommand();cmd.CommandText="SELECT file_path FROM singer_history WHERE song_id=$id";cmd.Parameters.AddWithValue("$id",id);Check((string?)await cmd.ExecuteScalarAsync()==newPath,"relink preserves and updates history");}
            var second=Path.Combine(old,"second.mp3");var dest=Path.Combine(next,"second.mp3");File.WriteAllText(dest,"abc");var sid=await repo.UpsertSongAsync(Song("Move","Second","second.mp3","Music") with{FilePath=second});
            bool rollback=false;try{await health.ApplyRelinksAsync(new[]{new LibraryRelink(sid,second,dest),new LibraryRelink(999999,Path.Combine(old,"bad.mp3"),newPath)},default);}catch(IOException){rollback=true;}
            Check(rollback && await repo.FindByFilePathAsync(second)!=null && await repo.FindByFilePathAsync(dest)==null,"failed batch rolls back all paths");
            var tempo = new HazzKaraokeHoster.App.TempoPreferences(Path.Combine(root,"tempo.json"));
            tempo.Save(oldPath,null,1.1);tempo.Save(oldPath,"venue:singer",.9);tempo.CopyPath(oldPath,newPath);
            Check(tempo.Load(newPath)==1.1 && tempo.Load(newPath,"venue:singer")==.9 && tempo.Load(oldPath)==1.1,"relink copies global/singer tempo and retains originals");
            AudioChecks();
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(root,true); }
    }
    static void AudioChecks()
    {
        AudioNormalization.Enabled=true; AudioNormalization.TargetDb=-18; AudioNormalization.MaxBoostDb=12;
        double Run(float amplitude) { var dsp=new NormalizingSampleProvider(new Tone(amplitude)); var buffer=new float[960];double sum=0;int count=0;
            for(int block=0;block<1500;block++){dsp.Read(buffer,0,buffer.Length);CheckFinite(buffer);if(block>1400)foreach(var x in buffer){sum+=x*x;count++;}}
            return Math.Sqrt(sum/count); }
        var quiet=Run(.06f);var loud=Run(.6f);Check(Math.Abs(20*Math.Log10(loud/quiet))<.5,"quiet/loud tracks converge within 0.5 dB");
        Run(2f); // Deliberately overloaded source exercises the peak limiter.
        AudioNormalization.MaxBoostDb=0;Check(Run(.01f)<.008,"zero boost cap does not amplify quiet audio");
        AudioNormalization.Enabled=false;var unity=Run(.2f);Check(Math.Abs(unity-.2/Math.Sqrt(2))<.001,"disabled matching leaves fresh audio unchanged");
        Console.WriteLine("PASS normalization finite samples and per-player peak ceiling");
    }
    static void CheckFinite(float[] b){if(b.Any(x=>!float.IsFinite(x)||Math.Abs(x)>.892))throw new Exception("invalid/over-limit output");}
    sealed class Tone(float amplitude):ISampleProvider {long position;public WaveFormat WaveFormat=>WaveFormat.CreateIeeeFloatWaveFormat(48000,2);public int Read(float[] b,int offset,int count){for(int i=0;i<count;i+=2){float v=amplitude*(float)Math.Sin(2*Math.PI*440*position++/48000);b[offset+i]=v;b[offset+i+1]=v;}return count;}}
}
