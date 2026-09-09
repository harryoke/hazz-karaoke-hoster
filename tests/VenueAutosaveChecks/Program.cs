using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Threading;
using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;
namespace HazzKaraokeHoster.App;
internal static class App { public static void WriteDiagnostic(string key, string message) => Console.WriteLine(key + ": " + message); }
public partial class MainWindow
{
    private readonly HazzDatabase _db;
    private string VenueProfilesPath => Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!, "venue-profiles.json");
    private readonly TextBlock SearchStatus = new();
    private readonly Dictionary<string, GroupRecord> _fairTurns = new();
    private sealed class GroupRecord { public string Group { get; set; } = ""; }
    private List<string> CaptureVenueRoster() => new() { "Alice" };
    private sealed class VenueProfile
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public Guid? SingerSnapshot { get; set; }
        public Guid? LastAutomaticSnapshot { get; set; }
        public List<string> SingerRoster { get; set; } = new();
        public Dictionary<string,string> SingerGroups { get; set; } = new();
    }
    private MainWindow(HazzDatabase db) { _db = db; }
    [STAThread] public static void Main()
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async () =>
        {
            try { await Test(); }
            catch (Exception ex) { Console.WriteLine(ex); Environment.ExitCode = 1; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }));
        Dispatcher.Run();
    }
    private static async Task Test()
    {
        var dir = Path.Combine(Path.GetTempPath(), "HazzAutosave-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var db = new HazzDatabase(Path.Combine(dir,"main.db")); await db.InitializeAsync();
        var w = new MainWindow(db); var id = Guid.NewGuid();
        File.WriteAllText(w.VenueProfilesPath, JsonSerializer.Serialize(new[] { new VenueProfile { Id = id, Name = "Pub" } }));
        w.SetActiveSingerVenue(id);
        using (var c = new SqliteConnection(db.ConnectionString)) { c.Open(); using var q = c.CreateCommand(); q.CommandText = "INSERT INTO singers(id,display_name) VALUES(1,'Alice'); INSERT INTO singer_history(singer_id,sung_at_utc) VALUES(1,'2026-09-09')"; q.ExecuteNonQuery(); }
        await Task.WhenAll(w.SaveActiveVenueAsync(), w.SaveActiveVenueAsync());
        var saved = JsonSerializer.Deserialize<List<VenueProfile>>(File.ReadAllText(w.VenueProfilesPath))![0];
        void Check(bool value,string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }
        Check(saved.SingerRoster.SequenceEqual(new[] { "Alice" }), "queue is captured");
        Check(Directory.GetFiles(Path.Combine(dir,"venue-singers"),"*.db").Length == 1, "overlapping saves serialize and retire old automatic revision");
        var file = Path.Combine(dir,"venue-singers",saved.SingerSnapshot!.Value.ToString("N") + ".db");
        using (var c = new SqliteConnection("Data Source=" + file)) { c.Open(); using var q=c.CreateCommand(); q.CommandText="SELECT COUNT(*) FROM singer_history"; Check((long)q.ExecuteScalar()! == 1,"history saved"); }
        var restarted = new MainWindow(db); restarted.InitializeVenueAutosave();
        Check(restarted._activeSingerVenue == id,"active venue survives restart");
        restarted.SetActiveSingerVenue(null); await restarted.SaveActiveVenueAsync();
        Check(File.Exists(file),"blank list disconnect leaves saved venue untouched");
        using (var c = new SqliteConnection(db.ConnectionString)) { c.Open(); using var q = c.CreateCommand(); q.CommandText = "SELECT COUNT(*) FROM singer_history"; Check((long)q.ExecuteScalar()! == 1,"no-venue operation preserves committed main history"); }
        w.SetActiveSingerVenue(Guid.NewGuid());
        try { await w.SaveActiveVenueAsync(); throw new Exception("missing profile accepted"); } catch (InvalidDataException) { }
        Check(File.Exists(file),"missing profile fails without overwriting saved singers");
    }
}
