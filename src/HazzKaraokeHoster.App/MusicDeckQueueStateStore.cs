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
        "Hazz Karaoke Hoster", "music-deck-queues.json");

    private static bool _canSave;

    public static MusicDeckQueueState Load()
    {
        try { var state = LoadFrom(StatePath); _canSave = true; return state; }
        catch { _canSave = false; return new MusicDeckQueueState(); }
    }

    internal static MusicDeckQueueState LoadFrom(string path)
    {
        if (!File.Exists(path) && !File.Exists(path + ".previous")) return new();
        foreach (var candidate in new[] { path, path + ".previous" })
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                var state = JsonSerializer.Deserialize<MusicDeckQueueState>(File.ReadAllText(candidate));
                if (IsValid(state)) return state!;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (JsonException) { }
        }
        throw new IOException("Saved music queues could not be read; existing files preserved.");
    }

    private static bool IsValid(MusicDeckQueueState? state)
        => state is { Deck1: not null, Deck2: not null }
            && state.Deck1.All(IsValidItem) && state.Deck2.All(IsValidItem);

    private static bool IsValidItem(MusicDeckQueueStateItem? item)
        => item is not null && (item.DurationSeconds is not double seconds
            || (double.IsFinite(seconds) && seconds < TimeSpan.MaxValue.TotalSeconds));

    public static void Save(MusicDeckQueueState state)
    {
        if (!_canSave) return;
        try { SaveTo(StatePath, state); }
        catch { /* Preserve existing data when storage is unavailable. */ }
    }

    internal static void SaveTo(string path, MusicDeckQueueState state)
    {
        if (!IsValid(state)) throw new ArgumentException("Music queues contain invalid entries or durations.", nameof(state));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            if (!File.Exists(path)) File.Move(temp, path);
            else
            {
                bool valid;
                try { valid = IsValid(JsonSerializer.Deserialize<MusicDeckQueueState>(File.ReadAllText(path))); }
                catch (JsonException) { valid = false; }
                File.Replace(temp, path, path + (valid ? ".previous" : ".unreadable"), true);
            }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
