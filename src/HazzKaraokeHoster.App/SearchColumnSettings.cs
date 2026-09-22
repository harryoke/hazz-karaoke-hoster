using System.Globalization;
using System.Text.Json;

namespace HazzKaraokeHoster.App;

internal sealed class SearchColumnSettingsFile
{
    public Dictionary<string, List<SearchColumnState>> Modes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class SearchColumnState
{
    public string Key { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public int DisplayIndex { get; set; }
    public double Width { get; set; }
    public string WidthUnit { get; set; } = "Pixel";
}

internal static class SearchColumnSettingsStore
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hazz Karaoke Hoster");
    internal static readonly string SettingsPath = Path.Combine(SettingsFolder, "search-columns.json");
    internal static readonly string PreviousPath = SettingsPath + ".previous";
    private static readonly object Gate = new();
    private static bool _canSave = true;

    public static SearchColumnSettingsFile Load()
    {
        lock (Gate)
        {
            if (!File.Exists(SettingsPath) && !File.Exists(PreviousPath))
            {
                _canSave = true;
                return new SearchColumnSettingsFile();
            }

            if (TryLoad(SettingsPath, out var primary))
            {
                _canSave = true;
                return primary;
            }

            if (TryLoad(PreviousPath, out var previous))
            {
                _canSave = true;
                return previous;
            }

            // Do not overwrite two unreadable settings copies with defaults.
            _canSave = false;
            return new SearchColumnSettingsFile();
        }
    }

    public static bool Save(SearchColumnSettingsFile settings)
    {
        lock (Gate)
        {
            if (!_canSave) return false;
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                var temp = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temp, json);
                try
                {
                    if (File.Exists(SettingsPath))
                    {
                        if (IsReadableJson(SettingsPath))
                            File.Replace(temp, SettingsPath, PreviousPath, true);
                        else
                        {
                            var unreadable = SettingsPath + ".unreadable";
                            File.Move(SettingsPath, unreadable, true);
                            File.Move(temp, SettingsPath);
                        }
                    }
                    else File.Move(temp, SettingsPath);
                }
                finally
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private static bool TryLoad(string path, out SearchColumnSettingsFile settings)
    {
        settings = new SearchColumnSettingsFile();
        if (!File.Exists(path)) return false;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;

            // Native v1.74+ format.
            try
            {
                var native = JsonSerializer.Deserialize<SearchColumnSettingsFile>(document.RootElement.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (native?.Modes is { Count: > 0 })
                {
                    settings = Normalize(native);
                    return true;
                }
            }
            catch (JsonException) { }

            // Compatibility reader for the unpublished Astra Search Columns test.
            // Accept either direct Karaoke/Music/MusicVideo nodes or a Modes wrapper,
            // arrays of column objects, or dictionaries keyed by column/header name.
            var root = document.RootElement;
            if (TryGetProperty(root, "Modes", out var modesNode) && modesNode.ValueKind == JsonValueKind.Object)
                ReadModeObject(modesNode, settings);
            ReadModeObject(root, settings);

            return settings.Modes.Count > 0;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (JsonException) { return false; }
    }

    private static SearchColumnSettingsFile Normalize(SearchColumnSettingsFile source)
    {
        var result = new SearchColumnSettingsFile();
        foreach (var pair in source.Modes)
        {
            var mode = NormalizeMode(pair.Key);
            if (mode is null) continue;
            var list = new List<SearchColumnState>();
            foreach (var state in pair.Value ?? new List<SearchColumnState>())
            {
                var key = NormalizeColumnKey(state.Key);
                if (key is null || list.Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase))) continue;
                list.Add(new SearchColumnState
                {
                    Key = key,
                    Visible = state.Visible,
                    DisplayIndex = state.DisplayIndex,
                    Width = double.IsFinite(state.Width) && state.Width > 0 ? state.Width : 80,
                    WidthUnit = NormalizeWidthUnit(state.WidthUnit)
                });
            }
            if (list.Count > 0) result.Modes[mode] = list;
        }
        return result;
    }

    private static void ReadModeObject(JsonElement node, SearchColumnSettingsFile target)
    {
        if (node.ValueKind != JsonValueKind.Object) return;
        foreach (var property in node.EnumerateObject())
        {
            var mode = NormalizeMode(property.Name);
            if (mode is null || target.Modes.ContainsKey(mode)) continue;
            var columnsNode = property.Value;
            if (columnsNode.ValueKind == JsonValueKind.Object && TryGetProperty(columnsNode, "Columns", out var nested))
                columnsNode = nested;
            var columns = ReadColumns(columnsNode);
            if (columns.Count > 0) target.Modes[mode] = columns;
        }
    }

    private static List<SearchColumnState> ReadColumns(JsonElement node)
    {
        var list = new List<SearchColumnState>();
        if (node.ValueKind == JsonValueKind.Array)
        {
            var fallbackOrder = 0;
            foreach (var item in node.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var rawKey = ReadString(item, "Key", "ColumnKey", "ColumnId", "Name", "Id", "Column", "Header");
                var key = NormalizeColumnKey(rawKey);
                if (key is null) continue;
                list.Add(ReadColumnState(item, key, fallbackOrder++));
            }
        }
        else if (node.ValueKind == JsonValueKind.Object)
        {
            var fallbackOrder = 0;
            foreach (var property in node.EnumerateObject())
            {
                var key = NormalizeColumnKey(property.Name);
                if (key is null || property.Value.ValueKind != JsonValueKind.Object) continue;
                list.Add(ReadColumnState(property.Value, key, fallbackOrder++));
            }
        }
        return list
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private static SearchColumnState ReadColumnState(JsonElement node, string key, int fallbackOrder)
    {
        var visible = ReadBool(node, true, "Visible", "IsVisible", "Shown", "IsShown");
        var order = ReadInt(node, fallbackOrder, "DisplayIndex", "Order", "Index");
        var widthUnit = ReadString(node, "WidthUnit", "Unit", "UnitType") ?? "Pixel";
        var width = ReadDouble(node, 80, "WidthValue", "ActualWidth", "Width");

        if (TryGetProperty(node, "Width", out var widthNode))
        {
            if (widthNode.ValueKind == JsonValueKind.String)
                ParseWidthString(widthNode.GetString(), ref width, ref widthUnit);
            else if (widthNode.ValueKind == JsonValueKind.Object)
            {
                width = ReadDouble(widthNode, width, "Value", "WidthValue", "DisplayValue", "ActualWidth");
                widthUnit = ReadString(widthNode, "UnitType", "WidthUnit", "Unit") ?? widthUnit;
            }
        }
        return new SearchColumnState
        {
            Key = key,
            Visible = visible,
            DisplayIndex = Math.Max(0, order),
            Width = double.IsFinite(width) && width > 0 ? width : 80,
            WidthUnit = NormalizeWidthUnit(widthUnit)
        };
    }

    private static void ParseWidthString(string? value, ref double width, ref string unit)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var text = value.Trim();
        if (text == "*") { width = 1; unit = "Star"; return; }
        if (text.EndsWith('*'))
        {
            if (double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var star) && star > 0)
                width = star;
            unit = "Star";
            return;
        }
        if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase)) { width = 1; unit = "Auto"; return; }
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixel) && pixel > 0)
        {
            width = pixel;
            unit = "Pixel";
        }
    }

    private static string? NormalizeMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var compact = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return compact switch
        {
            "karaoke" or "karaokesearch" => "Karaoke",
            "music" or "musicsearch" => "Music",
            "musicvideo" or "musicvideos" or "videomusic" or "musicvideosearch" => "MusicVideo",
            _ => null
        };
    }

    internal static string? NormalizeColumnKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var compact = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return compact switch
        {
            "favourite" or "favorite" or "fav" or "star" => "Favourite",
            "artist" => "Artist",
            "title" or "song" or "songtitle" => "Title",
            "maker" or "manufacturer" => "Maker",
            "disc" or "discid" or "disk" or "diskid" => "Disc",
            "length" or "duration" or "time" => "Length",
            "format" or "fileformat" or "type" => "Format",
            _ => null
        };
    }

    private static string NormalizeWidthUnit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Pixel";
        return value.Trim().ToLowerInvariant() switch
        {
            "star" or "*" => "Star",
            "auto" => "Auto",
            "sizetocells" or "cells" => "SizeToCells",
            "sizetoheader" or "header" => "SizeToHeader",
            _ => "Pixel"
        };
    }

    private static bool IsReadableJson(string path)
    {
        try { using var _ = JsonDocument.Parse(File.ReadAllText(path)); return true; }
        catch { return false; }
    }

    private static bool TryGetProperty(JsonElement node, string name, out JsonElement value)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in node.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static string? ReadString(JsonElement node, params string[] names)
    {
        foreach (var name in names)
            if (TryGetProperty(node, name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        return null;
    }

    private static bool ReadBool(JsonElement node, bool fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(node, name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;
            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    private static int ReadInt(JsonElement node, int fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(node, name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)) return parsed;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed)) return parsed;
        }
        return fallback;
    }

    private static double ReadDouble(JsonElement node, double fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(node, name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var parsed)) return parsed;
            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) return parsed;
        }
        return fallback;
    }
}
