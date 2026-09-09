using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

/// <summary>Transactional singer-only snapshots. The media library is never replaced.</summary>
public sealed class VenueSingerStore(HazzDatabase database)
{
    private const string SingerColumns = "id,display_name,notes,created_utc,last_seen_utc";
    private const string HistoryColumns = "id,singer_id,song_id,artist,title,file_path,sung_at_utc,key_change,cdg_sync_seconds,times_sung,imported_from";

    public Task KeepAsync(IReadOnlyCollection<long> ids, bool historyOnly = false) => Task.Run(() =>
    {
        using var c = new SqliteConnection(database.ConnectionString); c.Open();
        using var tx = c.BeginTransaction();
        using var q = c.CreateCommand(); q.Transaction = tx;
        q.CommandText = "CREATE TEMP TABLE keep_singers(id INTEGER PRIMARY KEY)"; q.ExecuteNonQuery();
        q.CommandText = "INSERT OR IGNORE INTO keep_singers VALUES($id)";
        var p = q.Parameters.Add("$id", SqliteType.Integer);
        foreach (var id in ids) { p.Value = id; q.ExecuteNonQuery(); }
        q.Parameters.Clear();
        q.CommandText = historyOnly ? "DELETE FROM singer_history" : "DELETE FROM singers WHERE id NOT IN (SELECT id FROM keep_singers)";
        q.ExecuteNonQuery();
        q.CommandText = "DROP TABLE keep_singers"; q.ExecuteNonQuery();
        tx.Commit();
    });

    public Task SaveAsync(string path) => Task.Run(() =>
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var c = new SqliteConnection(database.ConnectionString))
            {
                c.Open();
                using var attach = c.CreateCommand();
                attach.CommandText = "ATTACH DATABASE $path AS venue";
                attach.Parameters.AddWithValue("$path", temp);
                attach.ExecuteNonQuery();
                using var tx = c.BeginTransaction();
                using var copy = c.CreateCommand();
                copy.Transaction = tx;
                copy.CommandText = $"CREATE TABLE venue.singers AS SELECT {SingerColumns} FROM main.singers; CREATE TABLE venue.singer_history AS SELECT {HistoryColumns} FROM main.singer_history;";
                copy.ExecuteNonQuery();
                tx.Commit();
                using var detach = c.CreateCommand();
                detach.CommandText = "DETACH DATABASE venue";
                detach.ExecuteNonQuery();
            }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    });

    public Task ReplaceAsync(string? path, bool includeHistory) => Task.Run(() =>
    {
        if (path is not null && !File.Exists(path)) throw new FileNotFoundException("This venue has no saved singer list yet. Use Save Singer List first.", path);
        using var c = new SqliteConnection(database.ConnectionString);
        c.Open();
        if (path is not null)
        {
            using var attach = c.CreateCommand();
            attach.CommandText = "ATTACH DATABASE $path AS venue";
            attach.Parameters.AddWithValue("$path", path);
            attach.ExecuteNonQuery();
        }
        try
        {
            using var tx = c.BeginTransaction();
            using var command = c.CreateCommand();
            command.Transaction = tx;
            command.CommandText = "DELETE FROM singer_history; DELETE FROM singers;";
            command.ExecuteNonQuery();
            if (path is not null)
            {
                command.CommandText = $"INSERT INTO main.singers({SingerColumns}) SELECT {SingerColumns} FROM venue.singers;";
                command.ExecuteNonQuery();
                if (includeHistory)
                {
                    // A saved song may have been removed from the shared library since saving.
                    command.CommandText = $"INSERT INTO main.singer_history({HistoryColumns}) SELECT h.id,h.singer_id,CASE WHEN EXISTS(SELECT 1 FROM main.songs s WHERE s.id=h.song_id) THEN h.song_id ELSE NULL END,h.artist,h.title,h.file_path,h.sung_at_utc,h.key_change,h.cdg_sync_seconds,h.times_sung,h.imported_from FROM venue.singer_history h;";
                    command.ExecuteNonQuery();
                }
            }
            tx.Commit();
        }
        finally
        {
            if (path is not null)
            {
                using var detach = c.CreateCommand();
                detach.CommandText = "DETACH DATABASE venue";
                detach.ExecuteNonQuery();
            }
        }
    });
}
