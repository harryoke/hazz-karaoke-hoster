using System.Xml.Linq;
using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

internal sealed record ExternalVirtualFolderImportResult(int FoldersCreated, long TrackLinksAdded, int ListsRead, IReadOnlyList<string> Warnings);

/// <summary>Imports only organisation metadata. Every external source is opened read-only.</summary>
internal static class ExternalVirtualFolderImport
{
    private static readonly HashSet<string> ListExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".m3u", ".m3u8", ".pls", ".xspf", ".wpl", ".xml", ".vdjfolder" };

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".aif", ".aiff", ".ape", ".opus",
      ".mp4", ".mkv", ".avi", ".mov", ".mpeg", ".mpg", ".wmv", ".m4v", ".vob", ".ts", ".m2ts", ".webm", ".divx", ".cdg", ".zip" };

    public static async Task<ExternalVirtualFolderImportResult> ImportAsync(
        SqliteConnection connection, SqliteTransaction transaction, string requestedSource, string resolvedSource,
        string detectedSource, string defaultMediaKind, CancellationToken token)
    {
        var warnings = new List<string>();
        var lists = DiscoverLists(requestedSource, resolvedSource, detectedSource).ToArray();
        var importedLists = new List<(IReadOnlyList<string> Parts, IReadOnlyList<string> Paths)>();

        foreach (var file in lists)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (Path.GetFileName(file).Equals("rekordbox.xml", StringComparison.OrdinalIgnoreCase))
                {
                    importedLists.AddRange(ParseRekordbox(file));
                    continue;
                }
                var paths = ParseList(file);
                if (paths.Count == 0) continue;
                importedLists.Add((BuildFolderParts(requestedSource, resolvedSource, detectedSource, file), paths));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (warnings.Count < 40) warnings.Add($"Could not read folder/list {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        if (importedLists.Count == 0) return new(0, 0, 0, warnings);

        await using var findFolder = connection.CreateCommand();
        findFolder.Transaction = transaction;
        findFolder.CommandText = "SELECT id FROM virtual_folders WHERE COALESCE(parent_id,0)=COALESCE($parent,0) AND name=$name COLLATE NOCASE LIMIT 1;";
        findFolder.Parameters.Add("$parent", SqliteType.Integer);
        findFolder.Parameters.Add("$name", SqliteType.Text);

        await using var createFolder = connection.CreateCommand();
        createFolder.Transaction = transaction;
        createFolder.CommandText = "INSERT INTO virtual_folders(parent_id,name) VALUES($parent,$name) RETURNING id;";
        createFolder.Parameters.Add("$parent", SqliteType.Integer);
        createFolder.Parameters.Add("$name", SqliteType.Text);

        await using var upsertSong = connection.CreateCommand();
        upsertSong.Transaction = transaction;
        upsertSong.CommandText = """
INSERT INTO songs(artist,title,manufacturer,disc_id,file_path,format,file_size,date_added,last_seen_utc,media_kind)
VALUES($artist,$title,'','',$path,$format,0,NULL,CURRENT_TIMESTAMP,$kind)
ON CONFLICT(file_path) DO UPDATE SET last_seen_utc=CURRENT_TIMESTAMP
RETURNING id;
""";
        upsertSong.Parameters.Add("$artist", SqliteType.Text);
        upsertSong.Parameters.Add("$title", SqliteType.Text);
        upsertSong.Parameters.Add("$path", SqliteType.Text);
        upsertSong.Parameters.Add("$format", SqliteType.Text);
        upsertSong.Parameters.Add("$kind", SqliteType.Text);

        await using var link = connection.CreateCommand();
        link.Transaction = transaction;
        link.CommandText = "INSERT OR IGNORE INTO virtual_folder_songs(folder_id,song_id) VALUES($folder,$song);";
        link.Parameters.Add("$folder", SqliteType.Integer);
        link.Parameters.Add("$song", SqliteType.Integer);

        var cache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var foldersCreated = 0;
        long linksAdded = 0;
        foreach (var list in importedLists)
        {
            token.ThrowIfCancellationRequested();
            long? parent = null;
            var key = "";
            foreach (var rawPart in new[] { CleanName(detectedSource) }.Concat(list.Parts.Select(CleanName)))
            {
                var part = rawPart.Length == 0 ? "Imported" : rawPart;
                key = key.Length == 0 ? part : key + "/" + part;
                if (cache.TryGetValue(key, out var cached)) { parent = cached; continue; }
                findFolder.Parameters["$parent"].Value = parent ?? (object)DBNull.Value;
                findFolder.Parameters["$name"].Value = part;
                var found = await findFolder.ExecuteScalarAsync(token).ConfigureAwait(false);
                long id;
                if (found is not null && found is not DBNull) id = Convert.ToInt64(found);
                else
                {
                    createFolder.Parameters["$parent"].Value = parent ?? (object)DBNull.Value;
                    createFolder.Parameters["$name"].Value = part;
                    id = Convert.ToInt64(await createFolder.ExecuteScalarAsync(token).ConfigureAwait(false));
                    foldersCreated++;
                }
                cache[key] = id;
                parent = id;
            }
            if (parent is null) continue;

            foreach (var rawPath in list.Paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var path = NormalizePath(rawPath, resolvedSource);
                if (!MediaExtensions.Contains(Path.GetExtension(path))) continue;
                var parsed = LibraryImportService.ParseName(path);
                upsertSong.Parameters["$artist"].Value = parsed.Artist;
                upsertSong.Parameters["$title"].Value = parsed.Title;
                upsertSong.Parameters["$path"].Value = path;
                upsertSong.Parameters["$format"].Value = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
                upsertSong.Parameters["$kind"].Value = ResolveKind(path, defaultMediaKind);
                var songId = Convert.ToInt64(await upsertSong.ExecuteScalarAsync(token).ConfigureAwait(false));
                link.Parameters["$folder"].Value = parent.Value;
                link.Parameters["$song"].Value = songId;
                linksAdded += await link.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }
        return new(foldersCreated, linksAdded, importedLists.Count, warnings);
    }

    private static IEnumerable<string> DiscoverLists(string requested, string resolved, string source)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddFile(string p) { if (File.Exists(p) && ListExtensions.Contains(Path.GetExtension(p))) found.Add(Path.GetFullPath(p)); }
        void AddTree(string p)
        {
            if (!Directory.Exists(p)) return;
            foreach (var f in Directory.EnumerateFiles(p, "*", new EnumerationOptions { RecurseSubdirectories=true, IgnoreInaccessible=true, MaxRecursionDepth=12, AttributesToSkip=FileAttributes.ReparsePoint }).Take(20000)) AddFile(f);
        }

        if (Directory.Exists(requested)) AddTree(requested); else AddFile(requested);
        AddFile(resolved);
        if (source.Equals("VirtualDJ", StringComparison.OrdinalIgnoreCase))
        {
            var home = Directory.Exists(requested) ? requested : Path.GetDirectoryName(resolved);
            if (home is not null)
            {
                AddTree(Path.Combine(home, "MyLists")); AddTree(Path.Combine(home, "My Lists"));
                AddTree(Path.Combine(home, "Playlists")); AddTree(Path.Combine(home, "Folders"));
            }
        }
        return found.OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ParseList(string file)
    {
        var ext = Path.GetExtension(file);
        if (ext.Equals(".m3u", StringComparison.OrdinalIgnoreCase) || ext.Equals(".m3u8", StringComparison.OrdinalIgnoreCase))
            return File.ReadLines(file).Select(x => x.Trim().Trim('"')).Where(x => x.Length > 0 && !x.StartsWith('#')).ToArray();
        if (ext.Equals(".pls", StringComparison.OrdinalIgnoreCase))
            return File.ReadLines(file).Select(x => x.Trim()).Where(x => x.StartsWith("File", StringComparison.OrdinalIgnoreCase) && x.Contains('='))
                .Select(x => x[(x.IndexOf('=')+1)..].Trim().Trim('"')).ToArray();

        var doc = XDocument.Load(file, LoadOptions.None);
        var root = doc.Root?.Name.LocalName ?? "";
        if (Path.GetFileName(file).Equals("database.xml", StringComparison.OrdinalIgnoreCase) || root.Equals("VirtualDJ_Database", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();
        return doc.Descendants().Select(e =>
            Attribute(e,"path","Path","src","Src","location","Location","href")
            ?? (new[] { "location", "path", "src" }.Contains(e.Name.LocalName, StringComparer.OrdinalIgnoreCase) ? e.Value : null))
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Where(x => MediaExtensions.Contains(Path.GetExtension(NormalizeUri(x)))).ToArray();
    }

    private static IEnumerable<(IReadOnlyList<string> Parts, IReadOnlyList<string> Paths)> ParseRekordbox(string file)
    {
        var doc = XDocument.Load(file, LoadOptions.None);
        var tracks = doc.Descendants().Where(x => x.Name.LocalName.Equals("TRACK", StringComparison.OrdinalIgnoreCase))
            .Select(x => (Id: Attribute(x,"TrackID"), Location: Attribute(x,"Location")))
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Location))
            .ToDictionary(x => x.Id!, x => NormalizeUri(x.Location!), StringComparer.OrdinalIgnoreCase);

        IEnumerable<(IReadOnlyList<string>, IReadOnlyList<string>)> Walk(XElement node, List<string> parents)
        {
            var name = Attribute(node,"Name") ?? "Playlist";
            var type = Attribute(node,"Type") ?? "";
            var current = new List<string>(parents) { name };
            var keys = node.Elements().Where(x => x.Name.LocalName.Equals("TRACK", StringComparison.OrdinalIgnoreCase))
                .Select(x => Attribute(x,"Key")).Where(x => x is not null && tracks.ContainsKey(x)).Select(x => tracks[x!]).ToArray();
            if (keys.Length > 0) yield return (current, keys);
            foreach (var child in node.Elements().Where(x => x.Name.LocalName.Equals("NODE", StringComparison.OrdinalIgnoreCase)))
                foreach (var item in Walk(child, type == "0" || keys.Length == 0 ? current : parents)) yield return item;
        }

        var playlists = doc.Descendants().FirstOrDefault(x => x.Name.LocalName.Equals("PLAYLISTS", StringComparison.OrdinalIgnoreCase));
        if (playlists is null) yield break;
        foreach (var node in playlists.Elements().Where(x => x.Name.LocalName.Equals("NODE", StringComparison.OrdinalIgnoreCase)))
            foreach (var item in Walk(node, new List<string>())) yield return item;
    }

    private static IReadOnlyList<string> BuildFolderParts(string requested, string resolved, string source, string file)
    {
        var root = Directory.Exists(requested) ? Path.GetFullPath(requested) : Path.GetDirectoryName(Path.GetFullPath(resolved)) ?? "";
        var dir = Path.GetDirectoryName(Path.GetFullPath(file)) ?? root;
        var parts = new List<string>();
        if (root.Length > 0 && dir.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(root, dir);
            if (relative != ".") parts.AddRange(relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        foreach (var generic in new[] { "VirtualDJ", "MyLists", "My Lists", "Playlists", "Folders" })
            if (parts.Count > 0 && parts[0].Equals(generic, StringComparison.OrdinalIgnoreCase)) parts.RemoveAt(0);
        parts.Add(Path.GetFileNameWithoutExtension(file));
        return parts.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }

    private static string? Attribute(XElement element, params string[] names)
        => element.Attributes().FirstOrDefault(a => names.Contains(a.Name.LocalName, StringComparer.OrdinalIgnoreCase))?.Value;

    private static string NormalizeUri(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile) return uri.LocalPath;
        return Uri.UnescapeDataString(value);
    }

    private static string NormalizePath(string value, string source)
    {
        value = NormalizeUri(value.Trim().Trim('"')).Replace('/', Path.DirectorySeparatorChar);
        if (!Path.IsPathRooted(value)) value = Path.Combine(Path.GetDirectoryName(source) ?? Environment.CurrentDirectory, value);
        try { return Path.GetFullPath(value); } catch { return value; }
    }

    private static string CleanName(string value)
    {
        value = new string(value.Trim().Where(c => !char.IsControl(c) && c != '/' && c != '\\').ToArray());
        return value.Length <= 80 ? value : value[..80];
    }

    private static string ResolveKind(string path, string fallback)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".cdg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            || path.Contains("karaoke", StringComparison.OrdinalIgnoreCase)) return "Karaoke";
        return fallback.Equals("Karaoke", StringComparison.OrdinalIgnoreCase) ? "Karaoke" : "Music";
    }
}
