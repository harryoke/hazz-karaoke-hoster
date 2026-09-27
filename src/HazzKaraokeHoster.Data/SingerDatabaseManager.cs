using Microsoft.Data.Sqlite;
namespace HazzKaraokeHoster.Data;

public sealed record SavedSingerRow(long Id, string Name, string Notes, long Performances);
public sealed record SavedHistoryRow(long Id, string Artist, string Title, string Date, long Times);

// Works with the live database and the existing singer-only venue snapshots.
public sealed class SingerDatabaseManager(string path)
{
    private SqliteConnection Open()
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false, ForeignKeys = true }.ToString()); c.Open(); return c;
    }
    public Task<List<SavedSingerRow>> SearchAsync(string text) => Task.Run(() =>
    {
        using var c = Open(); using var q = c.CreateCommand();
        q.CommandText = "SELECT s.id,s.display_name,coalesce(s.notes,''),coalesce(h.n,0) FROM singers s LEFT JOIN (SELECT singer_id,sum(times_sung) n FROM singer_history GROUP BY singer_id) h ON h.singer_id=s.id WHERE instr(lower(s.display_name),lower($text))>0 ORDER BY s.display_name COLLATE NOCASE";
        q.Parameters.AddWithValue("$text", text.Trim()); using var r = q.ExecuteReader(); var rows = new List<SavedSingerRow>();
        while(r.Read()) rows.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt64(3))); return rows;
    });
    public Task<List<SavedHistoryRow>> HistoryAsync(long id) => Task.Run(() =>
    {
        using var c = Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT id,artist,title,sung_at_utc,times_sung FROM singer_history WHERE singer_id=$id ORDER BY sung_at_utc DESC"; q.Parameters.AddWithValue("$id",id);
        using var r=q.ExecuteReader(); var rows=new List<SavedHistoryRow>(); while(r.Read()) rows.Add(new(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetInt64(4))); return rows;
    });
    public Task DeleteAsync(long[] ids, bool historyOnly, bool historyRows = false) => Task.Run(() =>
    {
        using var c=Open(); using var tx=c.BeginTransaction(); using var q=c.CreateCommand(); q.Transaction=tx; q.Parameters.Add("$id",SqliteType.Integer);
        foreach(var id in ids.Distinct())
        {
            q.Parameters["$id"].Value=id;
            q.CommandText=historyRows ? "DELETE FROM singer_history WHERE id=$id" : "DELETE FROM singer_history WHERE singer_id=$id"; q.ExecuteNonQuery();
            if(!historyOnly && !historyRows) { q.CommandText="DELETE FROM singers WHERE id=$id"; q.ExecuteNonQuery(); }
        }
        tx.Commit();
    });
    public Task MergeAsync(long keep, long[] remove) => Task.Run(() =>
    {
        using var c=Open(); using var tx=c.BeginTransaction(); using var q=c.CreateCommand(); q.Transaction=tx;
        q.CommandText="SELECT count(*) FROM singers WHERE id=$keep"; q.Parameters.AddWithValue("$keep",keep);
        if(Convert.ToInt64(q.ExecuteScalar())!=1) throw new InvalidOperationException("Choose an existing singer to keep.");
        q.Parameters.Add("$id",SqliteType.Integer);
        foreach(var id in remove.Distinct().Where(x=>x!=keep))
        {
            q.Parameters["$id"].Value=id;
            q.CommandText="UPDATE singers SET notes=trim(coalesce(notes,'') || char(10) || coalesce((SELECT notes FROM singers WHERE id=$id),'')), last_seen_utc=max(coalesce(last_seen_utc,''),coalesce((SELECT last_seen_utc FROM singers WHERE id=$id),'')) WHERE id=$keep"; q.ExecuteNonQuery();
            q.CommandText="UPDATE singer_history SET singer_id=$keep WHERE singer_id=$id"; q.ExecuteNonQuery();
            q.CommandText="DELETE FROM singers WHERE id=$id"; q.ExecuteNonQuery();
        }
        tx.Commit();
    });
    public Task CopyFromAsync(string source, long[] ids) => Task.Run(() =>
    {
        if(Path.GetFullPath(source).Equals(Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose a different destination list.");
        if(!File.Exists(source)) throw new FileNotFoundException("Source list is missing.",source);
        using var c=Open(); using var attach=c.CreateCommand(); attach.CommandText="ATTACH DATABASE $path AS origin"; attach.Parameters.AddWithValue("$path",source); attach.ExecuteNonQuery();
        using var tx=c.BeginTransaction(); using var q=c.CreateCommand(); q.Transaction=tx;
        q.CommandText="SELECT count(*) FROM sqlite_master WHERE type='table' AND name='songs'"; bool live=Convert.ToInt64(q.ExecuteScalar())>0;
        q.Parameters.Add("$id",SqliteType.Integer); q.Parameters.Add("$target",SqliteType.Integer);
        foreach(var id in ids.Distinct())
        {
            q.Parameters["$id"].Value=id; q.Parameters["$target"].Value=0;
            q.CommandText="SELECT id FROM main.singers WHERE lower(trim(display_name))=(SELECT lower(trim(display_name)) FROM origin.singers WHERE id=$id) ORDER BY id LIMIT 1";
            var found=q.ExecuteScalar();
            if(found is null)
            {
                q.CommandText="INSERT INTO main.singers(id,display_name,notes,created_utc,last_seen_utc) SELECT (SELECT coalesce(max(id),0)+1 FROM main.singers),display_name,notes,created_utc,last_seen_utc FROM origin.singers WHERE id=$id";
                if(q.ExecuteNonQuery()!=1) throw new InvalidOperationException("Source singer no longer exists.");
                q.CommandText="SELECT max(id) FROM main.singers"; found=q.ExecuteScalar();
            }
            q.Parameters["$target"].Value=Convert.ToInt64(found);
            // Snapshot tables predate constraints: allocate IDs explicitly and make repeated copies idempotent.
            var song=live ? "CASE WHEN EXISTS(SELECT 1 FROM main.songs s WHERE s.id=h.song_id AND s.file_path=h.file_path) THEN h.song_id ELSE NULL END" : "h.song_id";
            q.CommandText=$"INSERT INTO main.singer_history(id,singer_id,song_id,artist,title,file_path,sung_at_utc,key_change,cdg_sync_seconds,times_sung,imported_from) SELECT (SELECT coalesce(max(id),0) FROM main.singer_history)+row_number() OVER(ORDER BY h.id),$target,{song},h.artist,h.title,h.file_path,h.sung_at_utc,h.key_change,h.cdg_sync_seconds,h.times_sung,h.imported_from FROM origin.singer_history h WHERE h.singer_id=$id AND NOT EXISTS(SELECT 1 FROM main.singer_history t WHERE t.singer_id=$target AND t.artist=h.artist AND t.title=h.title AND t.file_path=h.file_path AND t.sung_at_utc=h.sung_at_utc AND t.key_change=h.key_change AND t.cdg_sync_seconds=h.cdg_sync_seconds AND t.times_sung=h.times_sung AND t.imported_from IS h.imported_from)";
            q.ExecuteNonQuery();
        }
        tx.Commit();
    });
}
