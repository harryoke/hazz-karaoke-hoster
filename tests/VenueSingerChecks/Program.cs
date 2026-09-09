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
