using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;

var root = Directory.CreateTempSubdirectory("hazz-singer-history-");
try
{
    var target = new HazzDatabase(Path.Combine(root.FullName, "hazz.db"));
    await target.InitializeAsync();
    var media = Path.Combine(root.FullName, "ABBA - Waterloo.mp3");
    await File.WriteAllBytesAsync(media, [1]);

    await using (var connection = new SqliteConnection(target.ConnectionString))
    {
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO songs(artist,title,file_path,format,media_kind) VALUES('ABBA','Waterloo',$path,'MP3','Karaoke')";
        command.Parameters.AddWithValue("$path", media);
        await command.ExecuteNonQueryAsync();
    }

    var csv = Path.Combine(root.FullName, "CompuHost-singer-history.csv");
    await File.WriteAllTextAsync(csv,
        "Singer,Artist,SongTitle,FilePath,SungAt,KeyChange,SyncSeconds,TimesSung\n" +
        $"Alice,ABBA,Waterloo,\"{media}\",2026-09-01T20:30:00Z,2,0.35,3\n" +
        "Bob,Unknown Artist,Missing Song,C:\\Missing\\song.zip,2026-09-02T21:00:00Z,-1,0,1\n");

    var importer = new ExternalSingerHistoryImportService(target);
    var preview = await importer.PreviewAsync(csv);
    Require(preview.DetectedSource == "CompuHost", "source detection");
    Require(preview.SampleRows.Count == 2, "preview rows");
    Require(preview.DetectedMapping.Contains("Singer=Singer") && preview.DetectedMapping.Contains("Title=SongTitle"), "column mapping");

    var first = await importer.ImportAsync(csv);
    Require(first.HistoryRowsImported == 2 && first.SingersImported == 2, "CSV history and singers");
    Require(first.MatchedSongs == 1 && first.UnmatchedSongs == 1, "matched and preserved unmatched songs");

    var second = await importer.ImportAsync(csv);
    Require(second.HistoryRowsImported == 2, "repeat import result");
    await using (var connection = new SqliteConnection(target.ConnectionString))
    {
        await connection.OpenAsync();
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM singer_history";
        Require(Convert.ToInt64(await count.ExecuteScalarAsync()) == 2, "idempotent reimport");

        await using var values = connection.CreateCommand();
        values.CommandText = "SELECT h.key_change,h.cdg_sync_seconds,h.times_sung,h.song_id,s.display_name FROM singer_history h JOIN singers s ON s.id=h.singer_id WHERE s.display_name='Alice'";
        await using var reader = await values.ExecuteReaderAsync();
        Require(await reader.ReadAsync(), "Alice history exists");
        Require(reader.GetInt32(0) == 2 && Math.Abs(reader.GetDouble(1) - 0.35) < 0.001 && reader.GetInt32(2) == 3, "key, sync and times retained");
        Require(!reader.IsDBNull(3), "known library song linked");
    }

    var json = Path.Combine(root.FullName, "KaraFun-history.json");
    await File.WriteAllTextAsync(json, """
{"history":[{"singerName":"Carol","artist":"ABBA","songTitle":"Waterloo","playedAt":"2026-09-03T22:00:00Z"}]}
""");
    Require((await importer.PreviewAsync(json)).SampleRows.Single().Singer == "Carol", "JSON preview");
    Require((await importer.ImportAsync(json)).MatchedSongs == 1, "JSON import");

    var sqlite = Path.Combine(root.FullName, "OpenKJ-history.db");
    await using (var source = new SqliteConnection($"Data Source={sqlite}"))
    {
        await source.OpenAsync();
        await using var create = source.CreateCommand();
        create.CommandText = "CREATE TABLE singer_history(singer TEXT,artist TEXT,title TEXT,sung_at TEXT); INSERT INTO singer_history VALUES('Dave','ABBA','Waterloo','2026-09-04T20:00:00Z')";
        await create.ExecuteNonQueryAsync();
    }
    Require((await importer.PreviewAsync(sqlite)).DetectedSource == "OpenKJ", "SQLite program detection");
    Require((await importer.ImportAsync(sqlite)).MatchedSongs == 1, "SQLite history import");

    Console.WriteLine("PASS: singer-history CSV, JSON and SQLite preview/import; song linking, unmatched preservation, metadata and idempotent reimport.");
}
finally
{
    SqliteConnection.ClearAllPools();
    try { root.Delete(true); } catch { }
}

static void Require(bool value, string check)
{
    if (!value) throw new InvalidOperationException("Failed: " + check);
}
