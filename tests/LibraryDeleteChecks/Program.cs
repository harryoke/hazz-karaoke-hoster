using HazzKaraokeHoster.Core.Models;
using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;

var root = Path.Combine(Path.GetTempPath(), $"hazz-library-delete-{Guid.NewGuid():N}");
var dbPath = Path.Combine(root, "test.db");
Directory.CreateDirectory(root);

try
{
    var keepPath = Path.Combine(root, "KEEP - Artist - Song.zip");
    var stalePath = Path.Combine(root, "STALE - Artist - Gone.zip");
    var dbOnlyPath = Path.Combine(root, "DBONLY - Artist - StillOnDisk.zip");
    File.WriteAllBytes(keepPath, Array.Empty<byte>());
    File.WriteAllBytes(stalePath, Array.Empty<byte>());
    File.WriteAllBytes(dbOnlyPath, Array.Empty<byte>());

    var db = new HazzDatabase(dbPath);
    var repo = new LibraryRepository(db);
    await repo.InitializeAsync();

    var keepId = await repo.UpsertSongAsync(new SongRecord(0, "Artist", "Song", "", "", keepPath, "ZIP", 0, DateTimeOffset.Now));
    var staleId = await repo.UpsertSongAsync(new SongRecord(0, "Artist", "Gone", "", "", stalePath, "ZIP", 0, DateTimeOffset.Now));
    var dbOnlyId = await repo.UpsertSongAsync(new SongRecord(0, "Artist", "StillOnDisk", "", "", dbOnlyPath, "ZIP", 0, DateTimeOffset.Now));

    await repo.SaveDurationAsync(keepId, 215);
    if ((await repo.FindByFilePathAsync(keepPath))?.DurationSeconds != 215) throw new Exception("Duration cache failed");
    await using (var setup = new SqliteConnection(db.ConnectionString))
    {
        await setup.OpenAsync();
        await using var insert = setup.CreateCommand();
        insert.CommandText = "INSERT INTO singers(id,display_name) VALUES(1,'Keep Singer'); INSERT INTO singer_history(singer_id,song_id,title,sung_at_utc) VALUES(1,$song,'Keep History',CURRENT_TIMESTAMP); INSERT INTO music_history(song_id,title) VALUES($song,'Keep Music History');";
        insert.Parameters.AddWithValue("$song", staleId);
        await insert.ExecuteNonQueryAsync();
    }

    var before = await repo.SearchAsync("Artist", 20);
    if (before.Count != 3) throw new Exception($"Expected three rows before delete, found {before.Count}.");

    File.Delete(stalePath);
    var deleted = await repo.DeleteSongsAsync(new[] { staleId, dbOnlyId });
    if (deleted != 2) throw new Exception($"Expected two deleted rows, got {deleted}.");
    if (File.Exists(stalePath)) throw new Exception("The test-deleted stale file unexpectedly returned.");
    if (!File.Exists(dbOnlyPath)) throw new Exception("DeleteSongsAsync deleted a physical file; database-only removal must keep it on disk.");
    if (!File.Exists(keepPath)) throw new Exception("Unrelated physical file was changed.");

    var after = await repo.SearchAsync("Artist", 20);
    if (after.Count != 1 || after[0].Id != keepId)
        throw new Exception("Deleted rows still appear in search or the wrong row was removed.");

    await using var connection = new SqliteConnection(db.ConnectionString);
    await connection.OpenAsync();
    await using var fts = connection.CreateCommand();
    fts.CommandText = "SELECT COUNT(*) FROM songs_fts WHERE songs_fts MATCH 'Gone*';";
    var ftsCount = Convert.ToInt64(await fts.ExecuteScalarAsync());
    if (ftsCount != 0) throw new Exception("FTS row remained after library deletion.");

    await using var history = connection.CreateCommand();
    history.CommandText = "SELECT (SELECT count(*) FROM singers WHERE display_name='Keep Singer') + (SELECT count(*) FROM singer_history WHERE title='Keep History' AND song_id IS NULL) + (SELECT count(*) FROM music_history WHERE title='Keep Music History' AND song_id IS NULL);";
    if (Convert.ToInt32(await history.ExecuteScalarAsync()) != 3) throw new Exception("Library removal lost singers or history");
    Console.WriteLine("Library database deletion, retained singers/history and duration cache checks passed.");
}
finally
{
    SqliteConnection.ClearAllPools();
    try { Directory.Delete(root, true); } catch { }
}
