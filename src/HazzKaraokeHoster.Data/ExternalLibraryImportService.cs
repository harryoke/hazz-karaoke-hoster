using System.Data;
using System.Data.OleDb;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

/// <summary>
/// Read-only migration adapter for VirtualDJ and common third-party library/list formats.
/// Source files/databases are never modified. Imported records are copied into Hazz's own SQLite database.
/// </summary>
public sealed class ExternalLibraryImportService(HazzDatabase database) : IExternalLibraryImportService
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".aif", ".aiff", ".ape", ".opus" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".mp4", ".mkv", ".avi", ".mov", ".mpeg", ".mpg", ".wmv", ".m4v", ".vob", ".ts", ".m2ts", ".webm", ".divx" };

    private static readonly HashSet<string> KaraokeExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".cdg", ".zip" };

    private static readonly Regex KaraokePathHint = new(@"(^|[\\/ _.-])(karaoke|cdg|mp3g|mp3\+g|kar)([\\/ _.-]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed record ImportedRow(string FilePath, string Artist, string Title, string Manufacturer, string DiscId, string? MediaHint = null);
    private sealed record TableMapping(string Table, string PathColumn, string? ArtistColumn, string? TitleColumn, string? ManufacturerColumn, string? DiscColumn);
    private sealed record SmartSource(string Path, string Application, int Confidence, IReadOnlyList<string> Evidence);
    private sealed record LegacyMediaMonkeySong(long Id, string Path);

    private static readonly HashSet<string> SmartSourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".xml", ".json", ".m3u", ".m3u8", ".pls", ".lst", ".kpl", ".wpl", ".xspf", ".asx", ".csv", ".tsv", ".txt", ".db", ".db3", ".sqlite", ".sqlite3", ".s3db", ".sqlitedb", ".musicdb", ".mdb", ".accdb", ".kdb" };

    public async Task<ExternalImportPreview> PreviewAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("Select an import source.", nameof(sourcePath));
        var smart = await ResolveSmartSourceAsync(sourcePath, cancellationToken);
        sourcePath = smart.Path;

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        ExternalImportPreview preview;
        if (ext is ".xml" or ".kpl" or ".wpl" or ".xspf" or ".asx") preview = await PreviewXmlAsync(sourcePath, cancellationToken);
        else if (ext == ".json") preview = await PreviewJsonAsync(sourcePath, cancellationToken);
        else if (ext is ".m3u" or ".m3u8" or ".pls" or ".lst") preview = await PreviewPlaylistAsync(sourcePath, cancellationToken);
        else if (ext is ".csv" or ".tsv" or ".txt") preview = await PreviewDelimitedAsync(sourcePath, cancellationToken);
        else if (ext is ".db" or ".db3" or ".sqlite" or ".sqlite3" or ".s3db" or ".sqlitedb" or ".musicdb") preview = await PreviewSqliteAsync(sourcePath, cancellationToken);
        else if (ext is ".mdb" or ".accdb" or ".kdb") preview = await PreviewOleDbAsync(sourcePath, cancellationToken);
        else throw new InvalidDataException($"Hazz does not recognise '{ext}' as an external library/list format.");

        var evidence = new[] { $"Smart detection confidence: {smart.Confidence}%" }.Concat(smart.Evidence).Concat(preview.DetectedObjects).Distinct().ToArray();
        return preview with { DetectedSource = smart.Application, SourcePath = sourcePath, DetectedObjects = evidence };
    }

    public async Task<ExternalImportResult> ImportAsync(
        string sourcePath,
        ExternalMediaKindMode mediaKindMode,
        bool verifyPaths,
        IProgress<ExternalImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var smart = await ResolveSmartSourceAsync(sourcePath, cancellationToken);
        sourcePath = smart.Path;
        await database.InitializeAsync(cancellationToken);

        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        var detectedSource = smart.Application;
        var warnings = new List<string>();
        var watchRoots = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        long rowsRead = 0, imported = 0, karaoke = 0, music = 0, missing = 0, errors = 0;
        long playlistsImported = 0, playlistItemsImported = 0;

        await using var target = new SqliteConnection(database.ConnectionString);
        await target.OpenAsync(cancellationToken);
        using var transaction = target.BeginTransaction();
        await using var upsertSongCommand = CreateTargetSongUpsertCommand(target, transaction);
        await using var upsertSourceCommand = CreateSourceUpsertCommand(target, transaction);
        await using var repairMediaMonkeyCommand = CreateMediaMonkeyRepairCommand(target, transaction);
        upsertSongCommand.Prepare();
        upsertSourceCommand.Prepare();
        repairMediaMonkeyCommand.Prepare();
        var legacyMediaMonkeySongs = detectedSource.Equals("MediaMonkey", StringComparison.OrdinalIgnoreCase)
            ? await LoadLegacyMediaMonkeySongsAsync(target, transaction, sourcePath, cancellationToken)
            : new Dictionary<string, List<LegacyMediaMonkeySong>>(StringComparer.OrdinalIgnoreCase);
        var mediaMonkeySourceDirectory = detectedSource.Equals("MediaMonkey", StringComparison.OrdinalIgnoreCase)
            ? Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? string.Empty)
            : string.Empty;

        async Task ProcessRowAsync(ImportedRow row)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowsRead++;
            try
            {
                var normalizedPath = NormalizePath(row.FilePath, sourcePath);
                if (string.IsNullOrWhiteSpace(normalizedPath) || !IsKnownMediaPath(normalizedPath)) return;
                if (verifyPaths && !File.Exists(normalizedPath))
                {
                    missing++;
                    if (missing <= 50) warnings.Add($"Missing: {normalizedPath}");
                    return;
                }

                var parsed = LibraryImportService.ParseName(normalizedPath);
                var artist = FirstNonBlank(row.Artist, parsed.Artist);
                var title = FirstNonBlank(row.Title, parsed.Title, Path.GetFileNameWithoutExtension(normalizedPath));
                var manufacturer = FirstNonBlank(row.Manufacturer, parsed.Manufacturer);
                var disc = FirstNonBlank(row.DiscId, parsed.DiscId);
                var kind = ResolveMediaKind(normalizedPath, row.MediaHint, mediaKindMode);
                if (detectedSource.Equals("MediaMonkey", StringComparison.OrdinalIgnoreCase))
                    await RepairLegacyMediaMonkeyPathAsync(repairMediaMonkeyCommand, legacyMediaMonkeySongs, mediaMonkeySourceDirectory, artist, title, normalizedPath, cancellationToken);
                var fileInfo = verifyPaths && File.Exists(normalizedPath) ? new FileInfo(normalizedPath) : null;
                var id = await UpsertTargetSongAsync(upsertSongCommand, artist, title, manufacturer, disc, normalizedPath,
                    Path.GetExtension(normalizedPath).TrimStart('.').ToUpperInvariant(), fileInfo?.Length ?? 0,
                    fileInfo is null ? null : new DateTimeOffset(fileInfo.CreationTimeUtc), kind, cancellationToken);
                await UpsertSourceAsync(upsertSourceCommand, id, detectedSource, sourcePath, cancellationToken);
                imported++;
                if (kind == "Karaoke") karaoke++; else music++;
                var root = InferWatchRoot(normalizedPath);
                if (!string.IsNullOrWhiteSpace(root))
                {
                    if (!watchRoots.TryGetValue(root, out var kinds)) watchRoots[root] = kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    kinds.Add(kind);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors++;
                if (errors <= 50) warnings.Add($"{row.FilePath}: {ex.Message}");
            }

            if (rowsRead % 1000 == 0)
                progress?.Report(new ExternalImportProgress(rowsRead, imported, playlistsImported, missing, errors, row.FilePath));
        }

        if (ext is ".xml" or ".kpl" or ".wpl" or ".xspf" or ".asx")
        {
            await foreach (var row in EnumerateXmlRowsAsync(sourcePath, cancellationToken)) await ProcessRowAsync(row);
        }
        else if (ext == ".json")
        {
            await foreach (var row in EnumerateJsonRowsAsync(sourcePath, cancellationToken)) await ProcessRowAsync(row);
        }
        else if (ext is ".m3u" or ".m3u8" or ".pls" or ".lst")
        {
            var playlistName = Path.GetFileNameWithoutExtension(sourcePath);
            var playlistId = await UpsertPlaylistAsync(target, transaction, playlistName, detectedSource, sourcePath, cancellationToken);
            await ClearPlaylistItemsAsync(target, transaction, playlistId, cancellationToken);
            playlistsImported = 1;
            var position = 0;
            await foreach (var row in EnumeratePlaylistRowsAsync(sourcePath, cancellationToken))
            {
                await ProcessRowAsync(row);
                position++;
                var p = NormalizePath(row.FilePath, sourcePath);
                if (!string.IsNullOrWhiteSpace(p))
                {
                    await InsertPlaylistItemAsync(target, transaction, playlistId, position, p, row.Artist, row.Title, cancellationToken);
                    playlistItemsImported++;
                }
            }
        }
        else if (ext is ".csv" or ".tsv" or ".txt")
        {
            await foreach (var row in EnumerateDelimitedRowsAsync(sourcePath, cancellationToken)) await ProcessRowAsync(row);
        }
        else if (ext is ".db" or ".db3" or ".sqlite" or ".sqlite3" or ".s3db" or ".sqlitedb" or ".musicdb")
        {
            await foreach (var row in EnumerateSqliteRowsAsync(sourcePath, cancellationToken)) await ProcessRowAsync(row);
        }
        else if (ext is ".mdb" or ".accdb" or ".kdb")
        {
            // OleDb is synchronous; it runs off the UI thread in MainWindow.
            foreach (var row in EnumerateOleDbRows(sourcePath, cancellationToken)) await ProcessRowAsync(row);
        }

        transaction.Commit();
        progress?.Report(new ExternalImportProgress(rowsRead, imported, playlistsImported, missing, errors, string.Empty));
        var collapsed = CollapseWatchRoots(watchRoots);
        return new ExternalImportResult(detectedSource, rowsRead, imported, karaoke, music, playlistsImported, playlistItemsImported,
            missing, errors, collapsed, warnings);
    }

    private async Task<SmartSource> ResolveSmartSourceAsync(string sourcePath, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("Select an import file or application data folder.", nameof(sourcePath));
        sourcePath = Path.GetFullPath(sourcePath);
        if (File.Exists(sourcePath)) return await DetectApplicationAsync(sourcePath, token);
        if (!Directory.Exists(sourcePath)) throw new FileNotFoundException("Import source not found.", sourcePath);

        var files = await Task.Run(() => Directory.EnumerateFiles(sourcePath, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MaxRecursionDepth = 6,
            AttributesToSkip = FileAttributes.ReparsePoint
        }).Where(p => SmartSourceExtensions.Contains(Path.GetExtension(p))).Take(10000).ToArray(), token);
        if (files.Length == 0) throw new InvalidDataException("No supported database, playlist or export file was found in that folder.");

        var named = files.Select(p => (Path: p, Score: CandidateNameScore(p))).OrderByDescending(x => x.Score)
            .ThenBy(x => x.Path.Length).Take(24).ToArray();
        var detected = new List<SmartSource>();
        foreach (var candidate in named)
        {
            token.ThrowIfCancellationRequested();
            detected.Add(await DetectApplicationAsync(candidate.Path, token));
        }
        var best = detected.OrderByDescending(x => x.Confidence * 1000 + CandidateNameScore(x.Path)).First();
        return best with { Evidence = new[] { $"Selected {Path.GetFileName(best.Path)} from {files.Length:N0} supported candidate file(s) in the folder." }.Concat(best.Evidence).ToArray() };
    }

    private static int CandidateNameScore(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        var score = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".db" or ".db3" or ".sqlite" or ".sqlite3" or ".s3db" or ".sqlitedb" or ".musicdb" => 35,
            ".mdb" or ".accdb" or ".kdb" => 34,
            ".xml" or ".json" or ".wpl" or ".xspf" or ".asx" => 28,
            ".m3u" or ".m3u8" or ".pls" or ".kpl" => 20,
            _ => 10
        };
        if (name is "database.xml" or "mm5.db" or "mm.db" or "itunes music library.xml" or "rekordbox.xml") score += 80;
        if (new[] { "mediamonkey", "compuhost", "lyrx", "karafun", "siglos", "powerkaraoke", "virtualdj", "openkj", "karma", "bpmstudio", "pcdj", "songbookdb", "kjams", "justkaraoke", "sax", "tricerasoft", "serato", "mixxx", "djay" }.Any(name.Contains)) score += 50;
        return score;
    }

    private static async Task<SmartSource> DetectApplicationAsync(string path, CancellationToken token)
    {
        var full = path.ToLowerInvariant();
        var file = Path.GetFileName(path).ToLowerInvariant();
        var evidence = new List<string>();
        string? app = null;
        var confidence = 55;
        void Match(string display, params string[] signatures)
        {
            if (app is not null || !signatures.Any(s => full.Contains(s, StringComparison.OrdinalIgnoreCase))) return;
            app = display; confidence = 82; evidence.Add($"Application signature found in path/name: {display}");
        }

        if (file is "mm5.db" or "mm.db") { app = "MediaMonkey"; confidence = 98; evidence.Add("Recognised MediaMonkey database filename."); }
        else if (file == "database.xml") { app = "VirtualDJ"; confidence = 96; evidence.Add("Recognised VirtualDJ database.xml filename."); }
        else if (file == "itunes music library.xml") { app = "Apple Music / iTunes"; confidence = 98; evidence.Add("Recognised iTunes library export filename."); }
        else if (file == "rekordbox.xml") { app = "rekordbox"; confidence = 98; evidence.Add("Recognised rekordbox XML export filename."); }
        Match("MediaMonkey", "mediamonkey"); Match("CompuHost", "compuhost"); Match("Lyrx", "lyrx"); Match("KaraFun", "karafun");
        Match("Siglos / PowerKaraoke", "siglos", "powerkaraoke", "power karaoke"); Match("VirtualDJ", "virtualdj");
        Match("OpenKJ", "openkj"); Match("Karma", "karma"); Match("BPM Studio", "bpmstudio", "bpm studio");
        Match("PCDJ DEX", "pcdj", "dex karaoke"); Match("MTU Hoster", "mtu hoster"); Match("Winamp", "winamp");
        Match("SongBookDB", "songbookdb"); Match("kJams", "kjams"); Match("JustKaraoke", "justkaraoke", "just karaoke");
        Match("Sax & Dottys", "sax & dottys", "saxanddottys"); Match("TriceraSoft", "tricerasoft", "swift elite");
        Match("Serato", "serato"); Match("Mixxx", "mixxx"); Match("djay Pro", "djay pro", "algoriddim");

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (app is null && ext is ".xml" or ".json" or ".csv" or ".tsv" or ".txt")
        {
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var buffer = new byte[Math.Min(64 * 1024, (int)Math.Min(int.MaxValue, stream.Length))];
                var count = await stream.ReadAsync(buffer, token);
                var sample = Encoding.UTF8.GetString(buffer, 0, count).ToLowerInvariant();
                foreach (var signature in new[]
                {
                    ("MediaMonkey", "mediamonkey"), ("CompuHost", "compuhost"), ("Lyrx", "lyrx"), ("KaraFun", "karafun"),
                    ("Siglos / PowerKaraoke", "siglos"), ("Siglos / PowerKaraoke", "powerkaraoke"), ("VirtualDJ", "virtualdj"),
                    ("OpenKJ", "openkj"), ("Karma", "karma"), ("BPM Studio", "bpm studio"), ("PCDJ DEX", "pcdj"),
                    ("SongBookDB", "songbookdb"), ("kJams", "kjams"), ("JustKaraoke", "justkaraoke"),
                    ("Sax & Dottys", "sax & dottys"), ("TriceraSoft", "tricerasoft"), ("Serato", "serato"),
                    ("Mixxx", "mixxx"), ("djay Pro", "algoriddim"), ("rekordbox", "rekordbox"), ("Apple Music / iTunes", "itunes")
                })
                {
                    if (!sample.Contains(signature.Item2, StringComparison.Ordinal)) continue;
                    app = signature.Item1; confidence = 90; evidence.Add($"Application signature found inside the export: {app}"); break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { evidence.Add("Could not inspect the file header: " + ex.Message); }
        }

        app ??= ext switch
        {
            ".m3u" or ".m3u8" or ".pls" or ".lst" => "Playlist export (application not identified)",
            ".csv" or ".tsv" or ".txt" => "Delimited library export (application not identified)",
            ".db" or ".db3" or ".sqlite" or ".sqlite3" or ".s3db" or ".sqlitedb" or ".musicdb" => "SQLite media application (application not identified)",
            ".mdb" or ".accdb" or ".kdb" => "Access/KDB media application (application not identified)",
            ".xml" or ".kpl" or ".wpl" or ".xspf" or ".asx" => "XML karaoke/media application (application not identified)",
            ".json" => "JSON media application (application not identified)",
            _ => "External media application"
        };
        evidence.Add($"Format adapter: {ext.TrimStart('.').ToUpperInvariant()}");
        return new SmartSource(path, app, confidence, evidence);
    }

    private async Task<ExternalImportPreview> PreviewXmlAsync(string path, CancellationToken token)
    {
        var samples = new List<ExternalImportPreviewRow>();
        var detected = "XML library";
        await foreach (var row in EnumerateXmlRowsAsync(path, token))
        {
            if (samples.Count == 0 && row.MediaHint?.Contains("VirtualDJ", StringComparison.OrdinalIgnoreCase) == true) detected = "VirtualDJ database.xml";
            samples.Add(ToPreview(row));
            if (samples.Count >= 20) break;
        }
        if (samples.Count == 0) throw new InvalidDataException("No media-file records could be detected in this XML file.");
        if (Path.GetFileName(path).Equals("database.xml", StringComparison.OrdinalIgnoreCase) && samples.Count > 0) detected = "VirtualDJ database.xml";
        return new ExternalImportPreview(detected, path, 0, 0, samples, Array.Empty<string>(), new[] { "XML media records" });
    }

    private async Task<ExternalImportPreview> PreviewJsonAsync(string path, CancellationToken token)
    {
        var samples = new List<ExternalImportPreviewRow>();
        long count = 0;
        await foreach (var row in EnumerateJsonRowsAsync(path, token))
        {
            count++;
            if (samples.Count < 20) samples.Add(ToPreview(row));
        }
        if (samples.Count == 0) throw new InvalidDataException("No JSON objects with a recognisable media path were found.");
        return new ExternalImportPreview("JSON media export", path, count, 0, samples, Array.Empty<string>(), new[] { "JSON media records" });
    }

    private async Task<ExternalImportPreview> PreviewPlaylistAsync(string path, CancellationToken token)
    {
        var samples = new List<ExternalImportPreviewRow>();
        long count = 0;
        await foreach (var row in EnumeratePlaylistRowsAsync(path, token))
        {
            count++;
            if (samples.Count < 20) samples.Add(ToPreview(row));
        }
        return new ExternalImportPreview($"{Path.GetExtension(path).TrimStart('.').ToUpperInvariant()} playlist/list", path, count, 1,
            samples, Array.Empty<string>(), new[] { Path.GetFileName(path) });
    }

    private async Task<ExternalImportPreview> PreviewDelimitedAsync(string path, CancellationToken token)
    {
        var samples = new List<ExternalImportPreviewRow>();
        long count = 0;
        await foreach (var row in EnumerateDelimitedRowsAsync(path, token))
        {
            count++;
            if (samples.Count < 20) samples.Add(ToPreview(row));
            if (count >= 5000 && samples.Count >= 20) break; // keep preview fast on giant exports
        }
        if (samples.Count == 0) throw new InvalidDataException("No media file/path column could be recognised in this text/CSV export.");
        return new ExternalImportPreview("Delimited library export", path, count >= 5000 ? 0 : count, 0, samples,
            count >= 5000 ? new[] { "Preview stopped after 5,000 rows to keep the UI responsive; import streams the complete file." } : Array.Empty<string>(),
            new[] { "Delimited records" });
    }

    private async Task<ExternalImportPreview> PreviewSqliteAsync(string path, CancellationToken token)
    {
        await using var c = CreateExternalSqliteConnection(path);
        await c.OpenAsync(token);
        await PrepareExternalSqliteAsync(c, token);
        var mappings = await FindSqliteMappingsAsync(c, token);
        var mapping = mappings.FirstOrDefault() ?? throw new InvalidDataException("No SQLite table with a recognisable media file/path column was found.");
        var count = await TryCountSqliteAsync(c, mapping.Table, token);
        var samples = new List<ExternalImportPreviewRow>();
        await foreach (var row in EnumerateSqliteRowsAsync(path, token))
        {
            samples.Add(ToPreview(row));
            if (samples.Count >= 20) break;
        }
        return new ExternalImportPreview("SQLite media database", path, count, 0, samples, Array.Empty<string>(),
            mappings.Select(m => $"{m.Table}: path={m.PathColumn}, artist={m.ArtistColumn ?? "-"}, title={m.TitleColumn ?? "-"}").ToArray());
    }

    private Task<ExternalImportPreview> PreviewOleDbAsync(string path, CancellationToken token)
        => Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            using var c = OpenOleDbReadOnly(path);
            var mappings = FindOleDbMappings(c);
            var mapping = mappings.FirstOrDefault() ?? throw new InvalidDataException("No Access/KDB table with a recognisable media file/path column was found.");
            var count = TryCountOleDb(c, mapping.Table);
            var samples = EnumerateOleDbRows(path, token).Take(20).Select(ToPreview).ToArray();
            return new ExternalImportPreview("Access/KDB media database", path, count, 0, samples, Array.Empty<string>(),
                mappings.Select(m => $"{m.Table}: path={m.PathColumn}, artist={m.ArtistColumn ?? "-"}, title={m.TitleColumn ?? "-"}").ToArray());
        }, token);

    private static ExternalImportPreviewRow ToPreview(ImportedRow row)
    {
        var path = row.FilePath;
        var parsed = LibraryImportService.ParseName(path);
        var kind = ResolveMediaKind(path, row.MediaHint, ExternalMediaKindMode.Auto);
        return new ExternalImportPreviewRow(FirstNonBlank(row.Artist, parsed.Artist), FirstNonBlank(row.Title, parsed.Title), path, kind,
            FirstNonBlank(row.Manufacturer, parsed.Manufacturer), FirstNonBlank(row.DiscId, parsed.DiscId));
    }

    private static async IAsyncEnumerable<ImportedRow> EnumerateXmlRowsAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        if (await LooksLikeApplePlistAsync(path, token))
        {
            await foreach (var row in EnumerateApplePlistRowsAsync(path, token)) yield return row;
            yield break;
        }
        var settings = new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Ignore, IgnoreComments = true, IgnoreWhitespace = true };
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = XmlReader.Create(stream, settings);
        string? currentSongPath = null, artist = null, title = null, manufacturer = null, disc = null, mediaHint = null;
        while (await reader.ReadAsync())
        {
            token.ThrowIfCancellationRequested();

            if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName.Equals("Song", StringComparison.OrdinalIgnoreCase) && currentSongPath is not null)
            {
                yield return new ImportedRow(currentSongPath, artist ?? "", title ?? "", manufacturer ?? "", disc ?? "", mediaHint);
                currentSongPath = null; artist = title = manufacturer = disc = mediaHint = null;
                continue;
            }

            if (reader.NodeType != XmlNodeType.Element) continue;
            var name = reader.LocalName;
            if (name.Equals("Song", StringComparison.OrdinalIgnoreCase))
            {
                currentSongPath = Attr(reader, "FilePath", "Path", "File", "Filename", "Location");
                artist = Attr(reader, "Author", "Artist", "Singer");
                title = Attr(reader, "Title", "Name");
                manufacturer = Attr(reader, "Manufacturer", "Brand", "Label");
                disc = Attr(reader, "DiscId", "DiscID", "Disc", "TrackId", "Code");
                mediaHint = "VirtualDJ";
                if (!string.IsNullOrWhiteSpace(currentSongPath) && reader.IsEmptyElement)
                {
                    yield return new ImportedRow(currentSongPath, artist ?? "", title ?? "", manufacturer ?? "", disc ?? "", mediaHint);
                    currentSongPath = null; artist = title = manufacturer = disc = mediaHint = null;
                }
                continue;
            }
            if (currentSongPath is not null && name.Equals("Tags", StringComparison.OrdinalIgnoreCase))
            {
                artist = FirstNonBlank(Attr(reader, "Author", "Artist"), artist);
                title = FirstNonBlank(Attr(reader, "Title", "Name"), title);
                manufacturer = FirstNonBlank(Attr(reader, "Label", "Publisher", "Manufacturer"), manufacturer);
                disc = FirstNonBlank(Attr(reader, "Grouping", "Remix"), disc);
                var flag = Attr(reader, "Karaoke", "Type", "Genre");
                if (!string.IsNullOrWhiteSpace(flag)) mediaHint = "VirtualDJ " + flag;
                continue;
            }
            if (currentSongPath is null)
            {
                // Generic XML exports often store each media row in a single element with attributes.
                var p = Attr(reader, "FilePath", "filepath", "Path", "path", "File", "file", "Filename", "filename", "Location", "location", "Src", "src", "Href", "href", "Uri", "uri");
                if (!string.IsNullOrWhiteSpace(p) && IsKnownMediaPath(p))
                    yield return new ImportedRow(p, Attr(reader, "Artist", "artist", "Author", "author") ?? "",
                        Attr(reader, "Title", "title", "Name", "name") ?? "", Attr(reader, "Manufacturer", "manufacturer", "Brand", "brand") ?? "",
                        Attr(reader, "DiscId", "DiscID", "disc_id", "Code", "code") ?? "", Attr(reader, "Type", "type", "MediaKind", "media_kind"));
                else if (!reader.IsEmptyElement && name is "location" or "path" or "file" or "filename" or "src")
                {
                    var textPath = await reader.ReadElementContentAsStringAsync();
                    if (IsKnownMediaPath(textPath)) yield return new ImportedRow(textPath, "", "", "", "");
                }
            }
        }
    }

    private static async Task<bool> LooksLikeApplePlistAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[Math.Min(4096, (int)Math.Min(stream.Length, 4096))];
        var count = await stream.ReadAsync(buffer, token);
        var sample = Encoding.UTF8.GetString(buffer, 0, count);
        return sample.Contains("<plist", StringComparison.OrdinalIgnoreCase);
    }

    private static async IAsyncEnumerable<ImportedRow> EnumerateApplePlistRowsAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var settings = new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Ignore, IgnoreComments = true, IgnoreWhitespace = true };
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = XmlReader.Create(stream, settings);
        string artist = string.Empty, title = string.Empty, location = string.Empty;
        while (await reader.ReadAsync())
        {
            token.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.IsEmptyElement || !reader.LocalName.Equals("key", StringComparison.OrdinalIgnoreCase)) continue;
            var key = (await reader.ReadElementContentAsStringAsync()).Trim();
            while (reader.NodeType != XmlNodeType.Element && await reader.ReadAsync()) token.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.IsEmptyElement || !reader.LocalName.Equals("string", StringComparison.OrdinalIgnoreCase)) continue;
            var value = (await reader.ReadElementContentAsStringAsync()).Trim();
            if (key.Equals("Name", StringComparison.OrdinalIgnoreCase)) title = value;
            else if (key.Equals("Artist", StringComparison.OrdinalIgnoreCase)) artist = value;
            else if (key.Equals("Location", StringComparison.OrdinalIgnoreCase)) location = value;
            if (!IsKnownMediaPath(location)) continue;
            yield return new ImportedRow(location, artist, title, "", "", "iTunes");
            artist = title = location = string.Empty;
        }
    }

    private static async IAsyncEnumerable<ImportedRow> EnumeratePlaylistRowsAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".pls")
        {
            using var sr = new StreamReader(path, Encoding.UTF8, true);
            while (await sr.ReadLineAsync(token) is { } line)
            {
                token.ThrowIfCancellationRequested();
                line = line.Trim();
                if (!line.StartsWith("File", StringComparison.OrdinalIgnoreCase)) continue;
                var idx = line.IndexOf('=');
                if (idx < 0) continue;
                var p = line[(idx + 1)..].Trim();
                if (p.Length > 0) yield return new ImportedRow(p, "", "", "", "");
            }
            yield break;
        }

        string? pendingTitle = null, pendingArtist = null;
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        while (await reader.ReadLineAsync(token) is { } line)
        {
            token.ThrowIfCancellationRequested();
            line = line.Trim().Trim('\0');
            if (line.Length == 0) continue;
            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                var comma = line.IndexOf(',');
                if (comma >= 0)
                {
                    var display = line[(comma + 1)..].Trim();
                    var sep = display.IndexOf(" - ", StringComparison.Ordinal);
                    if (sep > 0) { pendingArtist = display[..sep].Trim(); pendingTitle = display[(sep + 3)..].Trim(); }
                    else pendingTitle = display;
                }
                continue;
            }
            if (line.StartsWith('#') || line.StartsWith('[')) continue;
            if (!IsKnownMediaPath(line)) continue;
            yield return new ImportedRow(line, pendingArtist ?? "", pendingTitle ?? "", "", "");
            pendingArtist = pendingTitle = null;
        }
    }

    private static async IAsyncEnumerable<ImportedRow> EnumerateJsonRowsAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }, token);
        var pending = new Stack<JsonElement>();
        pending.Push(document.RootElement);
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var element = pending.Pop();
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray().Reverse()) pending.Push(child);
                continue;
            }
            if (element.ValueKind != JsonValueKind.Object) continue;
            var properties = element.EnumerateObject().ToArray();
            string Value(params string[] names)
            {
                foreach (var property in properties)
                    if (names.Any(n => string.Equals(NormalizeColumn(property.Name), NormalizeColumn(n), StringComparison.Ordinal))
                        && property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                        return property.Value.ToString().Trim();
                return string.Empty;
            }
            var mediaPath = Value("filepath", "file_path", "fullpath", "path", "filename", "location", "url", "uri", "mediafile", "songfile", "trackpath");
            if (IsKnownMediaPath(mediaPath))
                yield return new ImportedRow(mediaPath, Value("artist", "author", "performer"), Value("title", "songtitle", "tracktitle", "name"),
                    Value("manufacturer", "brand", "label", "publisher"), Value("discid", "disc_id", "code", "trackid"), Value("mediakind", "type", "category"));
            foreach (var property in properties.Reverse())
                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) pending.Push(property.Value);
        }
    }

    private static async IAsyncEnumerable<ImportedRow> EnumerateDelimitedRowsAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        var first = await reader.ReadLineAsync(token) ?? string.Empty;
        var delimiter = Path.GetExtension(path).Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : GuessDelimiter(first);
        var headers = ParseDelimitedLine(first, delimiter);
        var pathIndex = FindColumn(headers, "filepath", "file_path", "fullpath", "path", "filename", "file", "location", "url", "uri", "mediafile", "media_path", "songfile", "song_path", "trackpath");
        var artistIndex = FindColumn(headers, "artist", "author", "performer");
        var titleIndex = FindColumn(headers, "title", "song", "tracktitle", "name");
        var manufacturerIndex = FindColumn(headers, "manufacturer", "brand", "label", "publisher");
        var discIndex = FindColumn(headers, "discid", "disc_id", "disc", "code", "trackid", "track_id");
        var kindIndex = FindColumn(headers, "mediakind", "media_kind", "type", "category");

        if (pathIndex < 0)
        {
            // Headerless text export: treat first line and every following line as a file path if possible.
            if (IsKnownMediaPath(first.Trim())) yield return new ImportedRow(first.Trim(), "", "", "", "");
            while (await reader.ReadLineAsync(token) is { } raw)
            {
                token.ThrowIfCancellationRequested();
                raw = raw.Trim();
                if (IsKnownMediaPath(raw)) yield return new ImportedRow(raw, "", "", "", "");
            }
            yield break;
        }

        while (await reader.ReadLineAsync(token) is { } line)
        {
            token.ThrowIfCancellationRequested();
            var fields = ParseDelimitedLine(line, delimiter);
            string Get(int i) => i >= 0 && i < fields.Count ? fields[i].Trim() : string.Empty;
            var p = Get(pathIndex);
            if (!IsKnownMediaPath(p)) continue;
            yield return new ImportedRow(p, Get(artistIndex), Get(titleIndex), Get(manufacturerIndex), Get(discIndex), Get(kindIndex));
        }
    }

    private static async IAsyncEnumerable<ImportedRow> EnumerateSqliteRowsAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        await using var c = CreateExternalSqliteConnection(path);
        await c.OpenAsync(token);
        await PrepareExternalSqliteAsync(c, token);
        if (Path.GetFileName(path) is var fileName &&
            (fileName.Equals("MM5.DB", StringComparison.OrdinalIgnoreCase) || fileName.Equals("MM.DB", StringComparison.OrdinalIgnoreCase)))
        {
            await foreach (var row in EnumerateMediaMonkeyRowsAsync(c, token)) yield return row;
            yield break;
        }
        var mapping = (await FindSqliteMappingsAsync(c, token)).FirstOrDefault()
            ?? throw new InvalidDataException("No SQLite table with a recognisable media path column was found.");
        await using var command = c.CreateCommand();
        command.CommandText = $"SELECT * FROM {QuoteSqlite(mapping.Table)}";
        await using var r = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token);
        var ord = Enumerable.Range(0, r.FieldCount).ToDictionary(i => r.GetName(i), i => i, StringComparer.OrdinalIgnoreCase);
        string Read(string? col)
        {
            if (string.IsNullOrWhiteSpace(col) || !ord.TryGetValue(col, out var i) || r.IsDBNull(i)) return string.Empty;
            return Convert.ToString(r.GetValue(i))?.Trim() ?? string.Empty;
        }
        while (await r.ReadAsync(token))
        {
            var p = Read(mapping.PathColumn);
            if (!IsKnownMediaPath(p)) continue;
            yield return new ImportedRow(p, Read(mapping.ArtistColumn), Read(mapping.TitleColumn), Read(mapping.ManufacturerColumn), Read(mapping.DiscColumn));
        }
    }

    private static async IAsyncEnumerable<ImportedRow> EnumerateMediaMonkeyRowsAsync(
        SqliteConnection connection,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
SELECT s.SongPath,
       coalesce(s.Artist, s.Author, ''),
       coalesce(s.SongTitle, ''),
       coalesce(s.Publisher, ''),
       coalesce(s.DiscNumber, ''),
       m.DriveLetter,
       coalesce(m.Location, '')
FROM Songs AS s
LEFT JOIN Medias AS m ON m.IDMedia = s.IDMedia
WHERE s.SongPath IS NOT NULL
""";
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token);
        while (await reader.ReadAsync(token))
        {
            var rawPath = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var driveLetter = reader.IsDBNull(5) ? string.Empty : Convert.ToString(reader.GetValue(5)) ?? string.Empty;
            var mediaLocation = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
            var resolvedPath = ResolveMediaMonkeyPath(rawPath, driveLetter, mediaLocation);
            if (!IsKnownMediaPath(resolvedPath)) continue;
            yield return new ImportedRow(
                resolvedPath,
                reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2)) ?? string.Empty,
                reader.IsDBNull(3) ? string.Empty : Convert.ToString(reader.GetValue(3)) ?? string.Empty,
                reader.IsDBNull(4) ? string.Empty : Convert.ToString(reader.GetValue(4)) ?? string.Empty,
                "MediaMonkey");
        }
    }

    private static string ResolveMediaMonkeyPath(string rawPath, string driveLetterValue, string mediaLocation)
    {
        var value = (rawPath ?? string.Empty).Trim().Trim('"').Replace('/', Path.DirectorySeparatorChar);
        if (value.Length == 0 || Path.IsPathRooted(value) || Uri.TryCreate(value, UriKind.Absolute, out _)) return value;

        string? drive = null;
        if (int.TryParse(driveLetterValue, out var driveIndex) && driveIndex is >= 0 and < 26)
            drive = ((char)('A' + driveIndex)).ToString();
        else
        {
            var cleaned = driveLetterValue.Trim().TrimEnd(':', '\\', '/');
            if (cleaned.Length == 1 && char.IsLetter(cleaned[0])) drive = cleaned.ToUpperInvariant();
        }

        var encoded = value.Length >= 2 && (value[0] is ':' or ';') && value[1] == Path.DirectorySeparatorChar;
        if (encoded && drive is not null) return drive + ":" + value[1..];
        if (value.StartsWith(Path.DirectorySeparatorChar) && drive is not null) return drive + ":" + value;

        if (encoded && !string.IsNullOrWhiteSpace(mediaLocation))
        {
            var root = mediaLocation.Trim().TrimEnd('\\', '/');
            if (root.Length > 0) return root + value[1..];
        }

        // An encoded path without a usable Medias row cannot be played reliably.
        return encoded ? string.Empty : value;
    }

    private static IEnumerable<ImportedRow> EnumerateOleDbRows(string path, CancellationToken token)
    {
        using var c = OpenOleDbReadOnly(path);
        var mapping = FindOleDbMappings(c).FirstOrDefault()
            ?? throw new InvalidDataException("No Access/KDB table with a recognisable media path column was found.");
        using var command = new OleDbCommand($"SELECT * FROM [{mapping.Table.Replace("]", "]]", StringComparison.Ordinal)}]", c);
        using var r = command.ExecuteReader(CommandBehavior.SequentialAccess);
        if (r is null) yield break;
        var ord = Enumerable.Range(0, r.FieldCount).ToDictionary(i => r.GetName(i), i => i, StringComparer.OrdinalIgnoreCase);
        string Read(string? col)
        {
            if (string.IsNullOrWhiteSpace(col) || !ord.TryGetValue(col, out var i) || r.IsDBNull(i)) return string.Empty;
            return Convert.ToString(r.GetValue(i))?.Trim() ?? string.Empty;
        }
        while (r.Read())
        {
            token.ThrowIfCancellationRequested();
            var p = Read(mapping.PathColumn);
            if (!IsKnownMediaPath(p)) continue;
            yield return new ImportedRow(p, Read(mapping.ArtistColumn), Read(mapping.TitleColumn), Read(mapping.ManufacturerColumn), Read(mapping.DiscColumn));
        }
    }

    private static async Task<IReadOnlyList<TableMapping>> FindSqliteMappingsAsync(SqliteConnection c, CancellationToken token)
    {
        var result = new List<TableMapping>();
        await using var tables = c.CreateCommand();
        // MediaMonkey databases contain an FTS virtual table that uses its private `mm`
        // tokenizer. Microsoft.Data.Sqlite cannot load that tokenizer, but the ordinary
        // Songs table remains fully readable. Never consider virtual search indexes as
        // import tables.
        tables.CommandText = """
SELECT name
FROM sqlite_master
WHERE type='table'
  AND name NOT LIKE 'sqlite_%'
  AND (sql IS NULL OR upper(ltrim(sql)) NOT LIKE 'CREATE VIRTUAL TABLE%')
ORDER BY name
""";
        await using var tr = await tables.ExecuteReaderAsync(token);
        var names = new List<string>();
        while (await tr.ReadAsync(token)) names.Add(tr.GetString(0));
        await tr.DisposeAsync();
        foreach (var table in names)
        {
            try
            {
                var cols = new List<string>();
                await using var cmd = c.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info({QuoteSqlite(table)})";
                await using var r = await cmd.ExecuteReaderAsync(token);
                while (await r.ReadAsync(token)) cols.Add(r.GetString(1));
                var m = BuildMapping(table, cols);
                if (m is not null) result.Add(m);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
            {
                // Skip optional vendor index/search objects requiring a private SQLite
                // extension. A failure here must not hide readable media tables.
            }
        }
        return result.OrderByDescending(MappingScore).ToArray();
    }

    private static SqliteConnection CreateExternalSqliteConnection(string path)
        => new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());

    private static async Task PrepareExternalSqliteAsync(SqliteConnection connection, CancellationToken token)
    {
        // MediaMonkey declares text columns with its private IUNICODE collation.
        // Register a read-only compatible comparison so SQLite can prepare SELECTs;
        // Smart Import never uses it to modify or reindex the source database.
        connection.CreateCollation("IUNICODE", (left, right) =>
            string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        await using var command = connection.CreateCommand();
        // writable_schema is a connection parsing mode here; the read-only connection
        // prevents changes. It lets SQLite ignore unusable vendor extension objects.
        command.CommandText = "PRAGMA writable_schema=ON; PRAGMA query_only=ON;";
        await command.ExecuteNonQueryAsync(token);
    }

    private static IReadOnlyList<TableMapping> FindOleDbMappings(OleDbConnection c)
    {
        var list = new List<TableMapping>();
        var schema = c.GetSchema("Tables");
        foreach (DataRow row in schema.Rows)
        {
            var type = Convert.ToString(row["TABLE_TYPE"]);
            if (!string.Equals(type, "TABLE", StringComparison.OrdinalIgnoreCase)) continue;
            var table = Convert.ToString(row["TABLE_NAME"]) ?? string.Empty;
            if (table.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var cmd = new OleDbCommand($"SELECT * FROM [{table.Replace("]", "]]", StringComparison.Ordinal)}] WHERE 1=0", c);
                using var r = cmd.ExecuteReader(CommandBehavior.SchemaOnly);
                if (r is null) continue;
                var cols = Enumerable.Range(0, r.FieldCount).Select(r.GetName).ToArray();
                var m = BuildMapping(table, cols);
                if (m is not null) list.Add(m);
            }
            catch { }
        }
        return list.OrderByDescending(MappingScore).ToArray();
    }

    private static TableMapping? BuildMapping(string table, IReadOnlyList<string> columns)
    {
        var path = BestColumn(columns, "filepath", "file_path", "fullpath", "path", "filename", "file", "location", "url", "uri", "mediafile", "media_path", "songfile", "songpath", "trackpath");
        if (path is null) return null;
        return new TableMapping(table, path,
            BestColumn(columns, "artist", "author", "performer", "singer"),
            BestColumn(columns, "title", "songtitle", "tracktitle", "song", "name"),
            BestColumn(columns, "manufacturer", "brand", "label", "publisher"),
            BestColumn(columns, "discid", "disc_id", "disc", "code", "trackid", "track_id"));
    }

    private static int MappingScore(TableMapping m)
        => 20 + (m.ArtistColumn is null ? 0 : 4) + (m.TitleColumn is null ? 0 : 4) + (m.ManufacturerColumn is null ? 0 : 1) + (m.DiscColumn is null ? 0 : 1)
           + (m.Table.Contains("song", StringComparison.OrdinalIgnoreCase) || m.Table.Contains("track", StringComparison.OrdinalIgnoreCase) || m.Table.Contains("media", StringComparison.OrdinalIgnoreCase) ? 5 : 0);

    private static string? BestColumn(IReadOnlyList<string> columns, params string[] desired)
    {
        static string N(string s) => new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        foreach (var d in desired)
        {
            var found = columns.FirstOrDefault(c => N(c) == N(d));
            if (found is not null) return found;
        }
        foreach (var d in desired)
        {
            var dn = N(d);
            var found = columns.FirstOrDefault(c => N(c).Contains(dn, StringComparison.Ordinal));
            if (found is not null) return found;
        }
        return null;
    }

    private static async Task<long> TryCountSqliteAsync(SqliteConnection c, string table, CancellationToken token)
    {
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {QuoteSqlite(table)}";
            return Convert.ToInt64(await cmd.ExecuteScalarAsync(token));
        }
        catch { return 0; }
    }

    private static long TryCountOleDb(OleDbConnection c, string table)
    {
        try { using var cmd = new OleDbCommand($"SELECT COUNT(*) FROM [{table.Replace("]", "]]", StringComparison.Ordinal)}]", c); return Convert.ToInt64(cmd.ExecuteScalar()); }
        catch { return 0; }
    }

    private static OleDbConnection OpenOleDbReadOnly(string path)
    {
        Exception? last = null;
        foreach (var provider in new[] { "Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0", "Microsoft.Jet.OLEDB.4.0" })
        {
            try
            {
                var c = new OleDbConnection($"Provider={provider};Data Source={path};Mode=Read;Persist Security Info=False;");
                c.Open();
                return c;
            }
            catch (Exception ex) { last = ex; }
        }
        throw new InvalidOperationException("Hazz could not open that Access/KDB database read-only. Install the Microsoft Access Database Engine matching Hazz x64 if required.", last);
    }

    private static SqliteCommand CreateTargetSongUpsertCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO songs(artist,title,manufacturer,disc_id,file_path,format,file_size,date_added,last_seen_utc,media_kind)
VALUES($artist,$title,$manufacturer,$disc,$path,$format,$size,$added,CURRENT_TIMESTAMP,$kind)
ON CONFLICT(file_path) DO UPDATE SET
 artist=CASE WHEN excluded.artist<>'' THEN excluded.artist ELSE songs.artist END,
 title=CASE WHEN excluded.title<>'' THEN excluded.title ELSE songs.title END,
 manufacturer=CASE WHEN excluded.manufacturer<>'' THEN excluded.manufacturer ELSE songs.manufacturer END,
 disc_id=CASE WHEN excluded.disc_id<>'' THEN excluded.disc_id ELSE songs.disc_id END,
 format=CASE WHEN excluded.format<>'' THEN excluded.format ELSE songs.format END,
 file_size=CASE WHEN excluded.file_size>0 THEN excluded.file_size ELSE songs.file_size END,
 date_added=COALESCE(songs.date_added,excluded.date_added),media_kind=excluded.media_kind,last_seen_utc=CURRENT_TIMESTAMP
RETURNING id;
""";
        command.Parameters.Add("$artist", SqliteType.Text);
        command.Parameters.Add("$title", SqliteType.Text);
        command.Parameters.Add("$manufacturer", SqliteType.Text);
        command.Parameters.Add("$disc", SqliteType.Text);
        command.Parameters.Add("$path", SqliteType.Text);
        command.Parameters.Add("$format", SqliteType.Text);
        command.Parameters.Add("$size", SqliteType.Integer);
        command.Parameters.Add("$added", SqliteType.Text);
        command.Parameters.Add("$kind", SqliteType.Text);
        return command;
    }

    private static async Task<long> UpsertTargetSongAsync(SqliteCommand command, string artist, string title, string manufacturer,
        string disc, string path, string format, long fileSize, DateTimeOffset? added, string kind, CancellationToken token)
    {
        command.Parameters["$artist"].Value = artist ?? string.Empty;
        command.Parameters["$title"].Value = title ?? string.Empty;
        command.Parameters["$manufacturer"].Value = manufacturer ?? string.Empty;
        command.Parameters["$disc"].Value = disc ?? string.Empty;
        command.Parameters["$path"].Value = path;
        command.Parameters["$format"].Value = format;
        command.Parameters["$size"].Value = fileSize;
        command.Parameters["$added"].Value = added?.ToString("O") ?? (object)DBNull.Value;
        command.Parameters["$kind"].Value = kind;
        return Convert.ToInt64(await command.ExecuteScalarAsync(token));
    }

    private static SqliteCommand CreateMediaMonkeyRepairCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
UPDATE songs
SET file_path=$corrected, last_seen_utc=CURRENT_TIMESTAMP
WHERE id=$id
  AND NOT EXISTS (SELECT 1 FROM songs WHERE file_path=$corrected)
""";
        command.Parameters.Add("$corrected", SqliteType.Text);
        command.Parameters.Add("$id", SqliteType.Integer);
        return command;
    }

    private static async Task<Dictionary<string, List<LegacyMediaMonkeySong>>> LoadLegacyMediaMonkeySongsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sourcePath,
        CancellationToken token)
    {
        var result = new Dictionary<string, List<LegacyMediaMonkeySong>>(StringComparer.OrdinalIgnoreCase);
        var sourceDirectory = Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? string.Empty);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
SELECT s.id, s.artist, s.title, s.file_path
FROM songs AS s
JOIN song_sources AS ss ON ss.song_id=s.id
WHERE ss.source_type='MediaMonkey' AND ss.source_path=$source
""";
        command.Parameters.AddWithValue("$source", sourcePath);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var path = reader.GetString(3);
            if (!LooksLikeLegacyMediaMonkeyPath(path, sourceDirectory)) continue;
            var key = MediaMonkeySongKey(reader.GetString(1), reader.GetString(2));
            if (!result.TryGetValue(key, out var songs)) result[key] = songs = new List<LegacyMediaMonkeySong>();
            songs.Add(new LegacyMediaMonkeySong(reader.GetInt64(0), path));
        }
        return result;
    }

    private static async Task RepairLegacyMediaMonkeyPathAsync(
        SqliteCommand command,
        Dictionary<string, List<LegacyMediaMonkeySong>> legacySongs,
        string sourceDirectory,
        string artist,
        string title,
        string correctedPath,
        CancellationToken token)
    {
        if (!Path.IsPathRooted(correctedPath) || sourceDirectory.Length == 0) return;
        var key = MediaMonkeySongKey(artist, title);
        if (!legacySongs.TryGetValue(key, out var candidates) || candidates.Count == 0) return;
        var matchIndex = candidates.FindIndex(x => LegacyMediaMonkeyPathMatches(x.Path, sourceDirectory, correctedPath));
        if (matchIndex < 0) matchIndex = 0;
        var candidate = candidates[matchIndex];
        candidates.RemoveAt(matchIndex);
        if (candidates.Count == 0) legacySongs.Remove(key);
        command.Parameters["$corrected"].Value = correctedPath;
        command.Parameters["$id"].Value = candidate.Id;
        await command.ExecuteNonQueryAsync(token);
    }

    private static bool LooksLikeLegacyMediaMonkeyPath(string value, string sourceDirectory)
    {
        if (!value.StartsWith(sourceDirectory, StringComparison.OrdinalIgnoreCase)) return false;
        var suffix = value[sourceDirectory.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return suffix.StartsWith(":\\", StringComparison.Ordinal) || suffix.StartsWith(";\\", StringComparison.Ordinal);
    }

    private static bool LegacyMediaMonkeyPathMatches(string legacyPath, string sourceDirectory, string correctedPath)
    {
        if (!LooksLikeLegacyMediaMonkeyPath(legacyPath, sourceDirectory) || correctedPath.Length < 3 || correctedPath[1] != ':') return false;
        var legacySuffix = legacyPath[sourceDirectory.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return legacySuffix.Length > 1 && legacySuffix[1..].Equals(correctedPath[2..], StringComparison.OrdinalIgnoreCase);
    }

    private static string MediaMonkeySongKey(string artist, string title)
        => string.Concat(artist.Trim(), "\u001f", title.Trim());

    private static SqliteCommand CreateSourceUpsertCommand(SqliteConnection connection, SqliteTransaction transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO song_sources(song_id,source_type,source_path,imported_utc)
VALUES($song,$type,$path,CURRENT_TIMESTAMP)
ON CONFLICT(song_id,source_type,source_path) DO UPDATE SET imported_utc=CURRENT_TIMESTAMP;
""";
        command.Parameters.Add("$song", SqliteType.Integer);
        command.Parameters.Add("$type", SqliteType.Text);
        command.Parameters.Add("$path", SqliteType.Text);
        return command;
    }

    private static async Task UpsertSourceAsync(SqliteCommand command, long songId, string sourceType, string sourcePath, CancellationToken token)
    {
        command.Parameters["$song"].Value = songId;
        command.Parameters["$type"].Value = sourceType;
        command.Parameters["$path"].Value = sourcePath;
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<long> UpsertPlaylistAsync(SqliteConnection c, SqliteTransaction tx, string name, string sourceType, string sourcePath, CancellationToken token)
    {
        await using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
INSERT INTO music_playlists(name,source_type,source_path,imported_utc)
VALUES($name,$type,$path,CURRENT_TIMESTAMP)
ON CONFLICT(name,source_type,source_path) DO UPDATE SET imported_utc=CURRENT_TIMESTAMP
RETURNING id;
""";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$type", sourceType);
        cmd.Parameters.AddWithValue("$path", sourcePath);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(token));
    }

    private static async Task ClearPlaylistItemsAsync(SqliteConnection c, SqliteTransaction tx, long playlistId, CancellationToken token)
    {
        await using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM music_playlist_items WHERE playlist_id=$p";
        cmd.Parameters.AddWithValue("$p", playlistId);
        await cmd.ExecuteNonQueryAsync(token);
    }

    private static async Task InsertPlaylistItemAsync(SqliteConnection c, SqliteTransaction tx, long playlistId, int position, string path, string artist, string title, CancellationToken token)
    {
        await using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO music_playlist_items(playlist_id,position,file_path,artist,title) VALUES($p,$pos,$path,$artist,$title)";
        cmd.Parameters.AddWithValue("$p", playlistId);
        cmd.Parameters.AddWithValue("$pos", position);
        cmd.Parameters.AddWithValue("$path", path);
        cmd.Parameters.AddWithValue("$artist", artist ?? "");
        cmd.Parameters.AddWithValue("$title", title ?? "");
        await cmd.ExecuteNonQueryAsync(token);
    }

    private static string ResolveMediaKind(string path, string? hint, ExternalMediaKindMode mode)
    {
        if (mode == ExternalMediaKindMode.Karaoke) return "Karaoke";
        if (mode == ExternalMediaKindMode.Music) return "Music";
        if (!string.IsNullOrWhiteSpace(hint) && hint.Contains("karaoke", StringComparison.OrdinalIgnoreCase)) return "Karaoke";
        var ext = Path.GetExtension(path);
        if (KaraokeExtensions.Contains(ext)) return "Karaoke";
        if ((AudioExtensions.Contains(ext) || VideoExtensions.Contains(ext)) && KaraokePathHint.IsMatch(path)) return "Karaoke";
        return "Music";
    }

    private static bool IsKnownMediaPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var ext = Path.GetExtension(value.Trim().Trim('"'));
        return AudioExtensions.Contains(ext) || VideoExtensions.Contains(ext) || KaraokeExtensions.Contains(ext);
    }

    private static string NormalizePath(string value, string sourcePath)
    {
        value = Environment.ExpandEnvironmentVariables((value ?? string.Empty).Trim().Trim('"'));
        if (value.Length == 0) return string.Empty;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile) value = uri.LocalPath;
        value = value.Replace('/', Path.DirectorySeparatorChar);
        if (!Path.IsPathRooted(value))
        {
            try { value = Path.Combine(Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory, value); } catch { }
        }
        try { return Path.GetFullPath(value); } catch { return value; }
    }

    private static string? InferWatchRoot(string mediaPath)
    {
        try
        {
            if (!Path.IsPathRooted(mediaPath)) return null;
            var root = Path.GetPathRoot(mediaPath);
            var directory = Path.GetDirectoryName(mediaPath);
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(directory)) return root;
            var relative = Path.GetRelativePath(root, directory);
            if (relative == ".") return Path.TrimEndingDirectorySeparator(root);
            var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault(x => x.Length > 0);
            return string.IsNullOrWhiteSpace(first) ? Path.TrimEndingDirectorySeparator(root) : Path.Combine(root, first);
        }
        catch { return null; }
    }

    private static IReadOnlyList<ExternalWatchRoot> CollapseWatchRoots(IReadOnlyDictionary<string, HashSet<string>> roots)
    {
        var ordered = roots.Keys.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Path.TrimEndingDirectorySeparator)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Length).ToList();
        var kept = new List<string>();
        foreach (var candidate in ordered)
        {
            if (kept.Any(parent => string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase)
                || candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) continue;
            kept.Add(candidate);
        }

        var result = new List<ExternalWatchRoot>();
        foreach (var root in kept)
        {
            var kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in roots)
            {
                var p = Path.TrimEndingDirectorySeparator(pair.Key);
                if (string.Equals(p, root, StringComparison.OrdinalIgnoreCase) || p.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    kinds.UnionWith(pair.Value);
            }
            var kind = kinds.Count == 1 ? kinds.First() : "Auto";
            result.Add(new ExternalWatchRoot(root, kind));
        }
        return result;
    }

    private static string DetectSourceName(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (Path.GetFileName(path).Equals("database.xml", StringComparison.OrdinalIgnoreCase)) return "VirtualDJ";
        return ext switch
        {
            ".m3u" or ".m3u8" or ".pls" or ".lst" => "Playlist import",
            ".csv" or ".tsv" or ".txt" => "Delimited import",
            ".db" or ".sqlite" or ".sqlite3" => "SQLite host database",
            ".mdb" or ".accdb" or ".kdb" => "Access/KDB host database",
            ".xml" => "XML host database",
            _ => "External import"
        };
    }

    private static string? Attr(XmlReader reader, params string[] names)
    {
        foreach (var n in names)
        {
            var v = reader.GetAttribute(n);
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }
        if (!reader.HasAttributes) return null;
        if (reader.MoveToFirstAttribute())
        {
            do
            {
                if (names.Any(n => string.Equals(n, reader.LocalName, StringComparison.OrdinalIgnoreCase)))
                {
                    var v = reader.Value?.Trim();
                    reader.MoveToElement();
                    return v;
                }
            } while (reader.MoveToNextAttribute());
            reader.MoveToElement();
        }
        return null;
    }

    private static char GuessDelimiter(string line)
    {
        var candidates = new[] { '\t', ',', ';', '|' };
        return candidates.OrderByDescending(c => line.Count(x => x == c)).First();
    }

    private static List<string> ParseDelimitedLine(string line, char delimiter)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (ch == delimiter && !quoted) { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        result.Add(sb.ToString());
        return result;
    }

    private static int FindColumn(IReadOnlyList<string> headers, params string[] names)
    {
        static string N(string s) => new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        for (var i = 0; i < headers.Count; i++) if (names.Any(n => N(headers[i]) == N(n))) return i;
        for (var i = 0; i < headers.Count; i++) if (names.Any(n => N(headers[i]).Contains(N(n), StringComparison.Ordinal))) return i;
        return -1;
    }

    private static string NormalizeColumn(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string FirstNonBlank(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
    private static string QuoteSqlite(string name) => '"' + name.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
}
