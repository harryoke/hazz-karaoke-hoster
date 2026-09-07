using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
var db = new HazzDatabase(Path.Combine(AppContext.BaseDirectory, "test-" + Guid.NewGuid() + ".db"));
await db.InitializeAsync();
using var c = new SqliteConnection(db.ConnectionString); c.Open();
using (var tx = c.BeginTransaction()) {
using var cmd = c.CreateCommand(); cmd.Transaction = tx;
cmd.CommandText = "INSERT INTO singers(display_name,last_seen_utc,notes) VALUES($name,$date,'keep notes')";
var n = cmd.Parameters.AddWithValue("$name", ""); var d = cmd.Parameters.AddWithValue("$date", "");
for(int i=0;i<20000;i++){n.Value=$"Singer {i:D5}"; d.Value=i<10000?"2026-01-01 00:00:00":"2026-09-01 00:00:00";cmd.ExecuteNonQuery();} tx.Commit(); }
var repo = new SingerRepository(db);
var sw=Stopwatch.StartNew(); var recent=await repo.SearchSingersAsync("",100000); sw.Stop();
if(recent.Count!=100 || recent[0].DisplayName!="Singer 10000") throw new Exception("Cap/order failed");
var found=await repo.SearchSingersAsync("01999"); if(found.Count!=1 || found[0].DisplayName!="Singer 01999")throw new Exception("Full database search failed");
await repo.AddHistoryAsync(found[0].Id,null,"Artist","Title","test.mp3",DateTimeOffset.UtcNow,2,0.25);
if((await repo.GetHistoryAsync(found[0].Id)).Count!=1 || (await repo.GetSingerAsync(found[0].Id))?.Notes!="keep notes")throw new Exception("History/details failed");
using var cancel=new CancellationTokenSource();cancel.Cancel();
try{await repo.SearchSingersAsync("",100,cancel.Token);throw new Exception("Cancellation failed");}catch(OperationCanceledException){}
await db.InitializeAsync();
using var count=c.CreateCommand();count.CommandText="SELECT count(*) FROM singers";if(Convert.ToInt32(count.ExecuteScalar())!=20000)throw new Exception("Data preservation failed");
using var plan=c.CreateCommand();plan.CommandText="EXPLAIN QUERY PLAN SELECT id,display_name,notes FROM singers WHERE ''='' OR instr(lower(display_name),'')>0 ORDER BY last_seen_utc DESC,display_name COLLATE NOCASE,id LIMIT 100";
using var reader=plan.ExecuteReader();while(reader.Read())Console.WriteLine(reader.GetString(3));
Console.WriteLine($"PASS: 20,000 singers; capped recent query {sw.ElapsedMilliseconds}ms; full search, ordering, cancellation, history/details, repeat migration and preservation.");
