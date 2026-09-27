using System.IO;
using HazzKaraokeHoster.Data;
using HazzKaraokeHoster.Core.Models;
using Microsoft.Data.Sqlite;

static class Release201Checks
{
    static void Check(bool ok,string text) { if(!ok)throw new Exception(text); Console.WriteLine("PASS "+text); }
    public static async Task Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"hazz-v201-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var db=new HazzDatabase(Path.Combine(root,"test.db")); await db.InitializeAsync(); var repo=new SingerRepository(db); var manager=new SingerDatabaseManager(db.DatabasePath);
            var alice=await repo.UpsertSingerAsync("Alice","original note"); var al=await repo.UpsertSingerAsync("Ali","second note"); var bob=await repo.UpsertSingerAsync("Bob");
            await repo.AddHistoryAsync(alice,null,"A","One","one.mp3",DateTimeOffset.UtcNow,2,.25);
            await repo.AddHistoryAsync(al,null,"B","Two","two.mp3",DateTimeOffset.UtcNow,-1,-.5);
            Check((await manager.SearchAsync("li")).Count==2,"search saved names");
            await manager.MergeAsync(alice,[alice,al]);
            var merged=(await manager.SearchAsync("Alice")).Single();
            Check(merged.Performances==2 && merged.Notes.Contains("second note") && (await manager.SearchAsync("")).Count==2,"merge keeps target and both histories/notes");
            var history=await repo.GetHistoryAsync(alice); Check(history.Any(h=>h.KeyChange==2)&&history.Any(h=>h.KeyChange==-1),"merge preserves song key settings");
            var snapshot=Path.Combine(root,"venue.db"); await new VenueSingerStore(db).SaveAsync(snapshot); var venue=new SingerDatabaseManager(snapshot);
            await venue.DeleteAsync([alice,bob],false); Check((await venue.SearchAsync("")).Count==0 && (await venue.HistoryAsync(alice)).Count==0,"snapshot deletion clears history without relying on foreign keys");
            await venue.CopyFromAsync(db.DatabasePath,[alice]); await venue.CopyFromAsync(db.DatabasePath,[alice]);
            var copied=(await venue.SearchAsync("")).Single(); Check(copied.Performances==2,"repeated venue copy does not duplicate performance events");
            await manager.DeleteAsync([alice],true); Check((await manager.SearchAsync("Alice")).Single().Performances==0,"history-only removal keeps singer");
            await manager.CopyFromAsync(snapshot,[copied.Id]); Check((await manager.HistoryAsync(alice)).Count==2,"venue-to-current copy matches name and preserves history");
            var chosen=(await manager.HistoryAsync(alice)).First().Id; await manager.DeleteAsync([chosen],true,true); Check((await manager.HistoryAsync(alice)).Count==1,"individual selected-history deletion");
            var before=(await manager.SearchAsync("")).Count; bool failed=false;
            try { await manager.CopyFromAsync(snapshot,[copied.Id,999999]); } catch(InvalidOperationException) { failed=true; }
            Check(failed && (await manager.SearchAsync("")).Count==before && (await manager.HistoryAsync(alice)).Count==1,"failed multi-singer copy rolls back whole destination transaction");
            await manager.CopyFromAsync(snapshot,[copied.Id]); await venue.DeleteAsync([copied.Id],false);
            Check((await manager.HistoryAsync(alice)).Count==2 && (await venue.SearchAsync("")).Count==0,"move saves destination before removing source");
            foreach(var name in CrossfadeCurve.Names)
            {
                Check(CrossfadeCurve.Gains(name,0)==(1d,0d)&&CrossfadeCurve.Gains(name,1)==(0d,1d),name+" endpoints");
                var previous=(Outgoing:1d,Incoming:0d);
                for(int i=0;i<=1000;i++) { var g=CrossfadeCurve.Gains(name,i/1000d); if(g.Outgoing<0 || g.Outgoing>1 || g.Incoming<0 || g.Incoming>1 || g.Outgoing>previous.Outgoing+1e-10 || g.Incoming<previous.Incoming-1e-10)throw new Exception("curve bounds/monotonicity"); previous=g; }
            }
            Check(CrossfadeCurve.Gains("unknown",.25)==(.75,.25),"unknown preset uses original linear fade");
            var power=CrossfadeCurve.Gains("Equal power",.5); Check(Math.Abs(power.Outgoing*power.Outgoing+power.Incoming*power.Incoming-1)<1e-9,"equal power midpoint");
            Check(CrossfadeCurve.Gains("Fade out then in",.5)==(0d,0d),"non-overlapping midpoint");
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(root,true); }
    }
}
