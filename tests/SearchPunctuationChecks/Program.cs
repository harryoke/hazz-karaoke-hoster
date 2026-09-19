using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;

var temp = Path.Combine(Path.GetTempPath(), $"hazz-search-{Guid.NewGuid():N}.db");
try
{
    var db = new HazzDatabase(temp);
    await db.InitializeAsync();
    await using (var connection = new SqliteConnection(db.ConnectionString))
    {
        await connection.OpenAsync();
        foreach (var (artist, title, path) in new[]
        {
            ("4 Non Blondes", "What's Up", @"C:\\Karaoke\\whats-up.zip"),
            ("Marvin Gaye", "Let's Get It On", @"C:\\Karaoke\\lets-get-it-on.zip"),
            ("Guns N' Roses", "Sweet Child O' Mine", @"C:\\Karaoke\\sweet-child.zip"),
            ("The Rolling Stones", "(I Can't Get No) Satisfaction", @"C:\\Karaoke\\satisfaction.zip"),
            ("AC/DC", "Thunderstruck", @"C:\\Karaoke\\thunderstruck.zip"),
            ("Wham!", "Wake Me Up Before You Go-Go", @"C:\\Karaoke\\wake-me-up.zip")
        })
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO songs(artist,title,file_path,format,media_kind) VALUES($artist,$title,$path,'ZIP','Karaoke');";
            insert.Parameters.AddWithValue("$artist", artist);
            insert.Parameters.AddWithValue("$title", title);
            insert.Parameters.AddWithValue("$path", path);
            await insert.ExecuteNonQueryAsync();
        }
    }

    var repository = new LibraryRepository(db);
    var checks = new[]
    {
        "What's Up",
        "Let's Get It On",
        "Sweet Child O' Mine",
        "(I Can't Get No) Satisfaction",
        "AC/DC",
        "Wake Me Up Before You Go-Go"
    };

    foreach (var query in checks)
    {
        var rows = await repository.SearchAsync(query, 50);
        if (rows.Count == 0) throw new Exception($"No result for punctuation search: {query}");
        Console.WriteLine($"PASS: {query} -> {rows[0].Artist} - {rows[0].Title}");
    }

    Console.WriteLine("All punctuation search checks passed.");
}
finally
{
    SqliteConnection.ClearAllPools();
    try { File.Delete(temp); } catch { }
    try { File.Delete(temp + "-wal"); } catch { }
    try { File.Delete(temp + "-shm"); } catch { }
}
