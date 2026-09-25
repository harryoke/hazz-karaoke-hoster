using System.Text.Json;
using Android.Content;
using Android.Net;
using Android.Provider;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.Android;

internal sealed record MediaRootMapping(string WindowsPrefix, string TreeUri);

internal sealed class AndroidMediaRootService
{
    private readonly Context _context;
    private readonly string _settingsPath;
    private readonly List<MediaRootMapping> _mappings = new();

    public AndroidMediaRootService(Context context)
    {
        _context = context;
        _settingsPath = Path.Combine(context.FilesDir!.AbsolutePath, "android-media-roots.json");
        Load();
    }

    public IReadOnlyList<MediaRootMapping> Mappings => _mappings;

    public async Task AddOrReplaceAsync(string windowsPrefix, Uri treeUri)
    {
        windowsPrefix = NormalizePrefix(windowsPrefix);
        if (windowsPrefix.Length == 0) throw new ArgumentException("Windows media root is required.", nameof(windowsPrefix));

        _mappings.RemoveAll(x => string.Equals(NormalizePrefix(x.WindowsPrefix), windowsPrefix, StringComparison.OrdinalIgnoreCase));
        _mappings.Add(new MediaRootMapping(windowsPrefix, treeUri.ToString()));
        _mappings.Sort((a, b) => b.WindowsPrefix.Length.CompareTo(a.WindowsPrefix.Length));
        await SaveAsync();
    }

    public async Task ClearAsync()
    {
        _mappings.Clear();
        await SaveAsync();
    }

    public async Task<Uri?> ResolveAsync(string windowsPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(windowsPath)) return null;

        var normalizedPath = NormalizeWindowsPath(windowsPath);
        var mapping = _mappings
            .Where(x => PathStartsWith(normalizedPath, NormalizePrefix(x.WindowsPrefix)))
            .OrderByDescending(x => NormalizePrefix(x.WindowsPrefix).Length)
            .FirstOrDefault();
        if (mapping is null) return null;

        var prefix = NormalizePrefix(mapping.WindowsPrefix);
        var relative = normalizedPath.Length == prefix.Length
            ? string.Empty
            : normalizedPath[(prefix.Length + 1)..];
        var segments = relative.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var treeUri = Uri.Parse(mapping.TreeUri);
        var documentId = DocumentsContract.GetTreeDocumentId(treeUri);
        var current = DocumentsContract.BuildDocumentUriUsingTree(treeUri, documentId);

        foreach (var segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = FindChild(treeUri, current, segment);
            if (next is null) return null;
            current = next;
        }

        await Task.CompletedTask;
        return current;
    }

    public string DescribeMappings()
        => _mappings.Count == 0
            ? "No media roots mapped"
            : string.Join("\n", _mappings.Select(x => $"{x.WindowsPrefix}  →  Android folder"));

    public static string SuggestWindowsRoot(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return string.Empty;
        var value = NormalizeWindowsPath(filePath);
        if (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && value[2] == '\\')
        {
            var rest = value[3..];
            var first = rest.IndexOf('\\');
            return first > 0 ? value[..(3 + first)] : value[..3].TrimEnd('\\');
        }

        if (value.StartsWith("\\\\", StringComparison.Ordinal))
        {
            var parts = value.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return $"\\\\{parts[0]}\\{parts[1]}";
        }
        return string.Empty;
    }

    private Uri? FindChild(Uri treeUri, Uri parentDocumentUri, string displayName)
    {
        var parentId = DocumentsContract.GetDocumentId(parentDocumentUri);
        var childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(treeUri, parentId);
        var projection = new[]
        {
            DocumentsContract.Document.ColumnDocumentId,
            DocumentsContract.Document.ColumnDisplayName
        };

        using var cursor = _context.ContentResolver!.Query(childrenUri, projection, null, null, null);
        if (cursor is null) return null;

        var idIndex = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDocumentId);
        var nameIndex = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDisplayName);
        while (cursor.MoveToNext())
        {
            var name = nameIndex >= 0 ? cursor.GetString(nameIndex) : null;
            if (!string.Equals(name, displayName, StringComparison.OrdinalIgnoreCase)) continue;
            var childId = cursor.GetString(idIndex);
            return DocumentsContract.BuildDocumentUriUsingTree(treeUri, childId);
        }
        return null;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return;
            var loaded = JsonSerializer.Deserialize<List<MediaRootMapping>>(File.ReadAllText(_settingsPath));
            if (loaded is null) return;
            _mappings.AddRange(loaded.Where(x => !string.IsNullOrWhiteSpace(x.WindowsPrefix) && !string.IsNullOrWhiteSpace(x.TreeUri)));
            _mappings.Sort((a, b) => b.WindowsPrefix.Length.CompareTo(a.WindowsPrefix.Length));
        }
        catch
        {
            _mappings.Clear();
        }
    }

    private Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_mappings, new JsonSerializerOptions { WriteIndented = true });
        return File.WriteAllTextAsync(_settingsPath, json);
    }

    private static bool PathStartsWith(string path, string prefix)
        => path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase);

    private static string NormalizePrefix(string value)
        => NormalizeWindowsPath(value).TrimEnd('\\');

    private static string NormalizeWindowsPath(string value)
        => (value ?? string.Empty).Trim().Replace('/', '\\');
}

internal sealed class AndroidShowStateService
{
    private sealed record SongState(long? SongId, string Artist, string Title, string FilePath, int Key, double Sync, double? Duration);
    private sealed record SingerState(long? SingerId, string Name, bool Held, List<SongState> Songs);
    private sealed record ShowState(List<SingerState> Singers);

    private readonly string _path;

    public AndroidShowStateService(Context context)
    {
        _path = Path.Combine(context.FilesDir!.AbsolutePath, "android-show-state.json");
    }

    public async Task SaveAsync(IEnumerable<SingerQueueEntry> rotation)
    {
        var state = new ShowState(rotation.Select(s => new SingerState(
            s.SingerId,
            s.SingerName,
            s.IsHeld,
            s.Songs.Select(song => new SongState(
                song.SongId, song.Artist, song.SongTitle, song.FilePath,
                song.KeyChange, song.CdgSyncSeconds, song.DurationSeconds)).ToList())).ToList());

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        var temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, json);
        File.Move(temp, _path, true);
    }

    public async Task<IReadOnlyList<SingerQueueEntry>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_path)) return Array.Empty<SingerQueueEntry>();
            var json = await File.ReadAllTextAsync(_path);
            var state = JsonSerializer.Deserialize<ShowState>(json);
            if (state?.Singers is null) return Array.Empty<SingerQueueEntry>();

            var result = new List<SingerQueueEntry>();
            foreach (var saved in state.Singers)
            {
                var singer = new SingerQueueEntry
                {
                    SingerId = saved.SingerId,
                    SingerName = saved.Name,
                    IsHeld = saved.Held
                };
                foreach (var savedSong in saved.Songs)
                {
                    singer.Songs.Add(new SingerSongEntry
                    {
                        SongId = savedSong.SongId,
                        Artist = savedSong.Artist,
                        SongTitle = savedSong.Title,
                        FilePath = savedSong.FilePath,
                        KeyChange = savedSong.Key,
                        CdgSyncSeconds = savedSong.Sync,
                        DurationSeconds = savedSong.Duration
                    });
                }
                result.Add(singer);
            }
            return result;
        }
        catch
        {
            return Array.Empty<SingerQueueEntry>();
        }
    }
}
