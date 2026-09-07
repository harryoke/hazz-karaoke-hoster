using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;

var root = Path.Combine(Path.GetTempPath(), "HazzLongShowChecks", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var databasePath = Path.Combine(root, "long-show.db");

try
{
    var database = new HazzDatabase(databasePath);
    await database.InitializeAsync();

    const int writers = 4;
    const int batchesPerWriter = 10;
    const int rowsPerBatch = 25;
    await Task.WhenAll(Enumerable.Range(0, writers).Select(writer => Task.Run(async () =>
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync();
        for (var batch = 0; batch < batchesPerWriter; batch++)
        {
            await using var transaction = await connection.BeginTransactionAsync();
            for (var row = 0; row < rowsPerBatch; row++)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = "INSERT INTO songs(artist,title,file_path,media_kind) VALUES($artist,$title,$path,'Music')";
                command.Parameters.AddWithValue("$artist", $"Writer {writer}");
                command.Parameters.AddWithValue("$title", $"Batch {batch} row {row}");
                command.Parameters.AddWithValue("$path", Path.Combine(root, $"{writer}-{batch}-{row}.mp3"));
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
    })));

    await using var verify = new SqliteConnection(database.ConnectionString);
    await verify.OpenAsync();
    await using var command = verify.CreateCommand();
    command.CommandText = "SELECT (SELECT COUNT(*) FROM songs), (SELECT * FROM pragma_journal_mode)";
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    var count = reader.GetInt64(0);
    var journal = reader.GetString(1);
    var defaultTimeout = new SqliteConnectionStringBuilder(database.ConnectionString).DefaultTimeout;
    var expected = writers * batchesPerWriter * rowsPerBatch;
    if (count != expected || !string.Equals(journal, "wal", StringComparison.OrdinalIgnoreCase) || defaultTimeout != 15)
        throw new InvalidOperationException($"Unexpected result: rows={count}, journal={journal}, default_timeout={defaultTimeout}");

    Console.WriteLine($"PASS: {count:N0} concurrent writes; WAL enabled; 15-second contention handling active.");
}
finally
{
    SqliteConnection.ClearAllPools();
    try { Directory.Delete(root, recursive: true); } catch { }
}
