using System.Text.Json;

namespace HazzKaraokeHoster.App;

public sealed class MusicFavourites(string path)
{
    public static MusicFavourites Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hazz Karaoke Hoster", "music-favourites.json"));
    private readonly object _gate = new();
    private HashSet<string>? _items;
    private HashSet<string> Items => _items ??= File.Exists(path)
        ? new(JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? throw new InvalidDataException("Favourites file is empty."), StringComparer.OrdinalIgnoreCase)
        : new(StringComparer.OrdinalIgnoreCase);
    public bool Contains(string file) { lock (_gate) return Items.Contains(Path.GetFullPath(file)); }
    public void Set(IEnumerable<string> files, bool favourite)
    {
        lock (_gate)
        {
            var copy = new HashSet<string>(Items, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files) { if (favourite) copy.Add(Path.GetFullPath(file)); else copy.Remove(Path.GetFullPath(file)); }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temp, JsonSerializer.Serialize(copy)); File.Move(temp, path, true); _items = copy; }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
