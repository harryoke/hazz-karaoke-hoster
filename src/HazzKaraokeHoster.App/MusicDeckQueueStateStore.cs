using System.Text.Json;

namespace HazzKaraokeHoster.App;

internal sealed class MusicDeckQueueState
{
    public List<MusicDeckQueueStateItem> Deck1 { get; set; } = new();
    public List<MusicDeckQueueStateItem> Deck2 { get; set; } = new();
}

internal sealed class MusicDeckQueueStateItem
{
    public long? SongId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double? DurationSeconds { get; set; }
}

internal static class MusicDeckQueueStateStore
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hazz Karaoke Hoster LibVLC Test", "music-deck-queues.json");

    public static MusicDeckQueueState Load()
    {
        try
        {
            if (!File.Exists(StatePath)) return new MusicDeckQueueState();
            return JsonSerializer.Deserialize<MusicDeckQueueState>(File.ReadAllText(StatePath)) ?? new MusicDeckQueueState();
        }
        catch
        {
            // A damaged queue-state file must never prevent Hazz from starting a show.
            return new MusicDeckQueueState();
        }
    }

    public static void Save(MusicDeckQueueState state)
    {
        try
        {
            var folder = Path.GetDirectoryName(StatePath)!;
            Directory.CreateDirectory(folder);
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            var temp = StatePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, StatePath, overwrite: true);
        }
        catch
        {
            // Queue persistence is a safety feature and must never interrupt live playback/shutdown.
        }
    }
}
