using HazzKaraokeHoster.Core.Models;
using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;

var root = Path.Combine(Path.GetTempPath(), $"hazz-filename-{Guid.NewGuid():N}");
var dbPath = Path.Combine(root, "test.db");
Directory.CreateDirectory(root);

try
{
    var numericPath = Path.Combine(root, "ZMP3G04 - Adamski - Killer.zip");
    var letterPath = Path.Combine(root, "ZPBINDIE - Blur - Parklife.zip");
    var ordinaryPath = Path.Combine(root, "ABBA - The Winner Takes It All - Live.zip");
    File.WriteAllBytes(numericPath, Array.Empty<byte>());
    File.WriteAllBytes(letterPath, Array.Empty<byte>());
    File.WriteAllBytes(ordinaryPath, Array.Empty<byte>());

    var db = new HazzDatabase(dbPath);
    await db.InitializeAsync();
    var importer = new LibraryImportService(db);

    await ExpectIndexed(importer.IndexFileAsync(numericPath, LibraryImportMode.Karaoke), numericPath);
    await ExpectIndexed(importer.IndexFileAsync(letterPath, LibraryImportMode.Karaoke), letterPath);
    await ExpectIndexed(importer.IndexFileAsync(ordinaryPath, LibraryImportMode.Karaoke), ordinaryPath);

    await AssertSong(db.ConnectionString, numericPath, "Adamski", "Killer", "ZMP", "ZMP3G04");
    await AssertSong(db.ConnectionString, letterPath, "Blur", "Parklife", "ZPB", "ZPBINDIE");
    await AssertSong(db.ConnectionString, ordinaryPath, "ABBA", "The Winner Takes It All - Live", "", "");

    // Simulate a row created by the older parser, then make a fresh database instance run
    // startup repair. This proves users do not need a full 800k-track rescan just to repair
    // already-indexed ZPB catalogue rows.
    var oldBadPath = Path.Combine(root, "ZPBINDIE - Ash - Girl From Mars.zip");
    File.WriteAllBytes(oldBadPath, Array.Empty<byte>());
    await using (var connection = new SqliteConnection(db.ConnectionString))
    {
        await connection.OpenAsync();
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
INSERT INTO songs(artist,title,manufacturer,disc_id,file_path,format,media_kind)
VALUES('ZPBINDIE','Ash - Girl From Mars','','',$path,'ZIP','Karaoke');
""";
        insert.Parameters.AddWithValue("$path", oldBadPath);
        await insert.ExecuteNonQueryAsync();
    }

    var repairedDb = new HazzDatabase(dbPath);
    await repairedDb.InitializeAsync();
    await AssertSong(repairedDb.ConnectionString, oldBadPath, "Ash", "Girl From Mars", "ZPB", "ZPBINDIE");

    Console.WriteLine("All karaoke filename parsing checks passed.");
}
finally
{
    SqliteConnection.ClearAllPools();
    try { Directory.Delete(root, true); } catch { }
}

static async Task ExpectIndexed(Task<SingleFileIndexResult> task, string path)
{
    var result = await task;
    if (result.Outcome != SingleFileIndexOutcome.Indexed)
        throw new Exception($"Expected {path} to index, got {result.Outcome}: {result.Message}");
}

static async Task AssertSong(string connectionString, string path, string artist, string title, string maker, string disc)
{
    await using var connection = new SqliteConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT artist,title,manufacturer,disc_id FROM songs WHERE file_path=$path LIMIT 1";
    command.Parameters.AddWithValue("$path", path);
    await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) throw new Exception($"Missing indexed row for {path}");

    var actual = (Artist: reader.GetString(0), Title: reader.GetString(1), Maker: reader.GetString(2), Disc: reader.GetString(3));
    var expected = (Artist: artist, Title: title, Maker: maker, Disc: disc);
    if (actual != expected)
        throw new Exception($"Unexpected parse for {Path.GetFileName(path)}. Expected {expected}, got {actual}");

    Console.WriteLine($"PASS: {Path.GetFileName(path)} -> {actual.Artist} | {actual.Title} | {actual.Maker} | {actual.Disc}");
}
