using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;

var root = Path.Combine(Path.GetTempPath(), "HazzVenueCheck-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var db = new HazzDatabase(Path.Combine(root, "test.db"));
await db.InitializeAsync();
void Sql(string sql) { using var c = new SqliteConnection(db.ConnectionString); c.Open(); using var q = c.CreateCommand(); q.CommandText = sql; q.ExecuteNonQuery(); }
long Count(string table) { using var c = new SqliteConnection(db.ConnectionString); c.Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT COUNT(*) FROM " + table; return (long)q.ExecuteScalar()!; }
void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
Sql("INSERT INTO songs(id,file_path,title) VALUES(1,'test.mp3','Song'); INSERT INTO singers(id,display_name) VALUES(1,'Alice'); INSERT INTO singer_history(singer_id,song_id,sung_at_utc) VALUES(1,1,'2026-09-09');");
var store = new VenueSingerStore(db);
var venue = Path.Combine(root, "venue.db");
await store.SaveAsync(venue);
await store.ReplaceAsync(null, false);
Check(Count("singers") == 0 && Count("singer_history") == 0 && Count("songs") == 1, "blank singers preserves library");
Sql("INSERT INTO singers(id,display_name) VALUES(2,'Bob');");
await store.ReplaceAsync(venue, true);
Check(Count("singers") == 1 && Count("singer_history") == 1, "restore names and history");
await store.ReplaceAsync(venue, false);
Check(Count("singers") == 1 && Count("singer_history") == 0, "fresh history keeps names");
try { await store.ReplaceAsync(Path.Combine(root,"missing.db"), true); throw new Exception("missing accepted"); } catch (FileNotFoundException) { }
Check(Count("singers") == 1, "missing snapshot does not clear singers");
var invalid = Path.Combine(root, "invalid.db");
using (var c = new SqliteConnection("Data Source=" + invalid)) { c.Open(); }
try { await store.ReplaceAsync(invalid, true); throw new Exception("invalid accepted"); } catch (SqliteException) { }
Check(Count("singers") == 1, "invalid snapshot rolls back deletion");
Sql("DELETE FROM songs;");
await store.ReplaceAsync(venue, true);
Check(Count("singer_history") == 1, "history survives missing library song");
Sql("INSERT INTO singers(id,display_name) VALUES(2,'Bob'); INSERT INTO singer_history(singer_id,sung_at_utc) VALUES(2,'2026-09-09');");
await store.KeepAsync(new long[] { 1 });
Check(Count("singers") == 1 && Count("singer_history") == 1, "keep selected cascades only removed singer history");
await store.KeepAsync(Array.Empty<long>(), true);
Check(Count("singers") == 1 && Count("singer_history") == 0, "clear history preserves names");
Console.WriteLine("Venue singer checks complete. Test files: " + root);
var backups = Path.Combine(root, "backups");
await CompressedBackup.CreateAsync(db, backups);
using (var zip = System.IO.Compression.ZipFile.OpenRead(Directory.GetFiles(backups,"*.zip").Single()))
{
    Check(zip.GetEntry("hazz-hoster.db") is not null, "compressed backup includes database");
    var restored = Path.Combine(root,"restored.db");
    using (var input = zip.GetEntry("hazz-hoster.db")!.Open()) using (var output = File.Create(restored)) input.CopyTo(output);
    using var c = new SqliteConnection("Data Source=" + restored); c.Open(); using var q = c.CreateCommand(); q.CommandText = "PRAGMA integrity_check";
    Check((string)q.ExecuteScalar()! == "ok", "extracted backup passes SQLite integrity check");
}
var retention = Path.Combine(root,"retention"); Directory.CreateDirectory(retention);
foreach (var day in new[]{1,2,3,4}) File.WriteAllBytes(Path.Combine(retention,$"hazz-2026-01-0{day}.zip"),new byte[100]);
File.WriteAllText(Path.Combine(retention,"manual.zip"),"keep"); File.WriteAllText(Path.Combine(retention,"hazz-2026-01-01.db"),"legacy");
CompressedBackup.Prune(retention,250);
Check(Directory.GetFiles(retention,"hazz-*.zip").Length==2 && File.Exists(Path.Combine(retention,"manual.zip")) && File.Exists(Path.Combine(retention,"hazz-2026-01-01.db")),"budget retention preserves manual and legacy backups");


// Database backups may replace an older snapshot, but never the active database.
var directBackup = Path.Combine(root, "direct-backup.db");
await db.BackupAsync(directBackup);
File.WriteAllText(directBackup, "old backup marker");
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    try { await db.BackupAsync(directBackup, cancelled.Token); throw new Exception("Cancelled backup succeeded"); }
    catch (OperationCanceledException) { }
}
Check(File.ReadAllText(directBackup) == "old backup marker", "cancelled backup preserves previous target");
await db.BackupAsync(directBackup);
using (var snapshot = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = directBackup, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
{
    snapshot.Open();
    using var query = snapshot.CreateCommand();
    query.CommandText = "PRAGMA integrity_check";
    Check((string)query.ExecuteScalar()! == "ok", "replacement database backup passes integrity check");
    query.CommandText = "SELECT COUNT(*) FROM singers";
    Check((long)query.ExecuteScalar()! == Count("singers"), "replacement backup retains singer data");
}
foreach (var protectedPath in new[] { db.DatabasePath, Path.Combine(root, ".", "test.db"), db.DatabasePath + "-wal", db.DatabasePath + "-shm", db.DatabasePath + "-journal" })
{
    try { await db.BackupAsync(protectedPath); throw new Exception("Live database destination accepted: " + protectedPath); }
    catch (ArgumentException) { }
}
Check(Count("singers") == 1, "live database and journal destinations rejected without data loss");
var blockedTarget = Path.Combine(root, "blocked.db");
Directory.CreateDirectory(blockedTarget);
File.WriteAllText(Path.Combine(blockedTarget, "keep.txt"), "keep");
try { await db.BackupAsync(blockedTarget); throw new Exception("Directory destination accepted"); }
catch (IOException) { }
catch (UnauthorizedAccessException) { }
Check(File.ReadAllText(Path.Combine(blockedTarget, "keep.txt")) == "keep" && !Directory.GetFiles(root, "*.tmp").Any(),
    "failed publication preserves destination and cleans staging files");

// Old .pending/.snapshot files must not block today's backup or be removed by it.
var interruptedFolder = Path.Combine(root, "interrupted-backups");
Directory.CreateDirectory(interruptedFolder);
var dailyTarget = Path.Combine(interruptedFolder, "hazz-" + DateTime.Now.ToString("yyyy-MM-dd") + ".zip");
File.WriteAllText(dailyTarget + ".pending", "interrupted ZIP");
File.WriteAllText(dailyTarget + ".snapshot", "interrupted snapshot");
await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => CompressedBackup.CreateAsync(db, interruptedFolder)));
using (var zip = System.IO.Compression.ZipFile.OpenRead(dailyTarget))
    Check(zip.GetEntry("hazz-hoster.db") is not null, "concurrent requests publish one complete daily backup despite stale files");
Check(File.ReadAllText(dailyTarget + ".pending") == "interrupted ZIP" && File.ReadAllText(dailyTarget + ".snapshot") == "interrupted snapshot",
    "backup requests only clean up their own working files");
Check(Directory.GetFiles(interruptedFolder).Length == 3, "completed ZIP backup leaves no new working files");
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    try { await CompressedBackup.CreateAsync(db, interruptedFolder, cancelled.Token); throw new Exception("Cancelled ZIP backup succeeded"); }
    catch (OperationCanceledException) { }
}
await CompressedBackup.CreateAsync(db, Path.Combine(root, "after-cancellation"));
Check(true, "ZIP backup remains usable after cancellation");
SqliteConnection.ClearAllPools();
Directory.Delete(root, recursive: true);
