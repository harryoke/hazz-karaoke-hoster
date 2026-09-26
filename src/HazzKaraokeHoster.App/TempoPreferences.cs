using System.Text.Json;

namespace HazzKaraokeHoster.App;

public sealed class TempoPreferences
{
    public static TempoPreferences Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hazz Karaoke Hoster", "tempo-preferences.json"));
    private readonly string _path;
    private Dictionary<string, double>? _values;
    public TempoPreferences(string path) => _path = path;
    private Dictionary<string, double> Values => _values ??= File.Exists(_path)
        ? JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(_path)) ?? throw new InvalidDataException("Tempo settings are empty.")
        : new();
    private static string Key(string path, string? singer) => JsonSerializer.Serialize(new[] {
        Path.GetFullPath(path).ToUpperInvariant(), singer?.ToUpperInvariant() ?? "" });
    public double Load(string path, string? singer = null)
    {
        if (singer is not null && Values.TryGetValue(Key(path, singer), out var preferred)) return Valid(preferred);
        return Values.TryGetValue(Key(path, null), out var tempo) ? Valid(tempo) : 1;
    }
    private static double Valid(double value) => double.IsFinite(value) ? Math.Clamp(value, 0.75, 1.25) : 1;
    public void CopyPath(string oldPath, string newPath) => CopyPaths(new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { [Path.GetFullPath(oldPath)] = newPath });
    public void CopyPaths(IReadOnlyDictionary<string,string> paths)
    {
        var copy = new Dictionary<string,double>(Values); var changed = false;
        foreach (var entry in Values)
        {
            var parts = JsonSerializer.Deserialize<string[]>(entry.Key);
            if (parts is { Length: 2 } && paths.TryGetValue(parts[0], out var destination))
            { copy[Key(destination, parts[1])] = entry.Value; changed = true; }
        }
        if (changed) Write(copy);
    }
    public void Save(string path, string? singer, double tempo)
    {
        var copy = new Dictionary<string, double>(Values) { [Key(path, singer)] = Valid(tempo) };
        Write(copy);
    }
    private void Write(Dictionary<string,double> copy)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(copy));
            File.Move(temporary, _path, overwrite: true);
            _values = copy;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
