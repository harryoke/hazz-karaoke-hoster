using System.Text.Json;

namespace HazzKaraokeHoster.App;

internal static class SingerVersionPreferenceStore
{
    private static readonly object Gate = new();
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hazz Karaoke Hoster");
    private static readonly string PathName = Path.Combine(Folder, "singer-version-preferences.json");

    public static string? GetPreferredPath(string? singer, string? artist, string? title)
    {
        var key = MakeKey(singer, artist, title);
        if (key is null) return null;
        lock (Gate)
        {
            var values = LoadUnsafe();
            return values.TryGetValue(key, out var path) && !string.IsNullOrWhiteSpace(path) ? path : null;
        }
    }

    public static void SetPreferredPath(string? singer, string? artist, string? title, string filePath)
    {
        var key = MakeKey(singer, artist, title);
        if (key is null || string.IsNullOrWhiteSpace(filePath)) return;
        lock (Gate)
        {
            var values = LoadUnsafe();
            values[key] = System.IO.Path.GetFullPath(filePath);
            SaveUnsafe(values);
        }
    }

    public static void ClearPreferredPath(string? singer, string? artist, string? title)
    {
        var key = MakeKey(singer, artist, title);
        if (key is null) return;
        lock (Gate)
        {
            var values = LoadUnsafe();
            if (values.Remove(key)) SaveUnsafe(values);
        }
    }

    private static string? MakeKey(string? singer, string? artist, string? title)
    {
        singer = Normalize(singer);
        artist = Normalize(artist);
        title = Normalize(title);
        if (singer.Length == 0 || title.Length == 0) return null;
        return singer + "\u001f" + artist + "\u001f" + title;
    }

    private static string Normalize(string? value)
        => string.Join(' ', (value ?? string.Empty).Trim().ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static Dictionary<string, string> LoadUnsafe()
    {
        try
        {
            if (!File.Exists(PathName)) return new(StringComparer.OrdinalIgnoreCase);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(PathName))
                ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveUnsafe(Dictionary<string, string> values)
    {
        Directory.CreateDirectory(Folder);
        var temp = PathName + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, PathName, true);
    }
}
