using System.Text.Json;

namespace HazzKaraokeHoster.App;

internal sealed class LiveShowStateStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public LiveShowStateStore()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hazz Karaoke Hoster LibVLC Test");
        Directory.CreateDirectory(appData);
        _path = Path.Combine(appData, "live-show-state.json");
    }

    public LiveShowSnapshot? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<LiveShowSnapshot>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            App.WriteDiagnostic("SHOW RECOVERY LOAD", ex.ToString());
            return null;
        }
    }

    public void Save(LiveShowSnapshot snapshot)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, _jsonOptions));
            File.Move(temp, _path, true);
        }
        catch (Exception ex)
        {
            App.WriteDiagnostic("SHOW RECOVERY SAVE", ex.ToString());
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch (Exception ex)
        {
            App.WriteDiagnostic("SHOW RECOVERY CLEAR", ex.ToString());
        }
    }
}

internal sealed class LiveShowSnapshot
{
    public bool FairRotation { get; set; }
    public RotationRoundState RotationRound { get; set; } = new();
    public string NewcomerPlacement { get; set; } = "End of current round";
    public int NewcomerSpacing { get; set; } = 2;
    public string FairPrimary { get; set; } = "Fewest turns";
    public string FairSecondary { get; set; } = "Longest waiting";
    public bool FairAvoidConsecutive { get; set; }
    public long FairSequence { get; set; }
    public Dictionary<string, FairTurnRecord> FairTurns { get; set; } = new();
    public int Version { get; set; } = 2;
    public List<RecoveredMusicDeck> MusicDecks { get; set; } = new();
    public DateTimeOffset ShowStartedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset SavedUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool CleanShutdown { get; set; }
    public Guid? SelectedSingerQueueId { get; set; }
    public List<LiveShowSingerSnapshot> Singers { get; set; } = new();
}

internal sealed class LiveShowSingerSnapshot
{
    public Guid QueueId { get; set; }
    public long? SingerId { get; set; }
    public string SingerName { get; set; } = string.Empty;
    public bool IsHeld { get; set; }
    public List<LiveShowSongSnapshot> Songs { get; set; } = new();
}

internal sealed class LiveShowSongSnapshot
{
    public Guid QueueSongId { get; set; }
    public long? SongId { get; set; }
    public string SongTitle { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int KeyChange { get; set; }
    public double CdgSyncSeconds { get; set; }
}

internal sealed class RecoveredMusicDeck
{
    public int Deck { get; set; }
    public string Path { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Title { get; set; } = "";
    public double PositionSeconds { get; set; }
}
