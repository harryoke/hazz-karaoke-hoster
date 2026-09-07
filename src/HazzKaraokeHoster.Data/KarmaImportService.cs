using System.Data;
using System.Data.OleDb;
using System.Text.Json;
using System.Xml.Linq;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

/// <summary>
/// Read-only Karma migration. Never writes to the source Karma files.
/// Karma schemas have changed over time, so singer/history import remains conservative while
/// the media-library importer performs schema inspection and shows the detected mapping first.
/// </summary>
public sealed class KarmaImportService(HazzDatabase database, ISingerRepository singers) : IKarmaImportService
{
    private static readonly HashSet<string> KnownMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".cdg", ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".aif", ".aiff",
        ".mp4", ".mkv", ".avi", ".mov", ".mpeg", ".mpg", ".wmv", ".m4v", ".vob", ".ts", ".m2ts", ".webm", ".divx"
    };

    public async Task<KarmaImportPreview> PreviewAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        sourcePath = NormalizeSource(sourcePath);
        if (Directory.Exists(sourcePath))
        {
            var files = Directory.EnumerateFiles(sourcePath, "*", SearchOption.TopDirectoryOnly)
                .Where(IsKarmaDatabaseOrXml)
                .Take(100).ToArray();
            return new KarmaImportPreview("Karaosoft Data folder", sourcePath, files, Array.Empty<string>());
        }

        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Karma source not found.", sourcePath);
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (ext is ".kdb" or ".mdb" or ".accdb")
        {
            var warnings = new List<string>();
            var tables = new List<string>();
            try { tables.AddRange(ReadAccessTableNames(sourcePath)); }
            catch (Exception ex) { warnings.Add("Could not open the KDB through ACE OLE DB: " + ex.Message); }
            return new KarmaImportPreview("Karma relational KDB/Access", sourcePath, tables, warnings);
        }
        if (ext == ".xml")
        {
            var doc = await LoadXmlAsync(sourcePath, cancellationToken);
            var names = doc.Descendants().Select(x => x.Name.LocalName).Distinct(StringComparer.OrdinalIgnoreCase).Order().Take(100).ToArray();
            return new KarmaImportPreview("Legacy Karma XML", sourcePath, names, Array.Empty<string>());
        }
        return new KarmaImportPreview("Unknown", sourcePath, Array.Empty<string>(), new[]{"Choose the Karaosoft Data folder, a KDB/MDB/ACCDB file, or legacy datamain.xml."});
    }

    public async Task<KarmaImportResult> ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        sourcePath = NormalizeSource(sourcePath);
        if (Directory.Exists(sourcePath))
        {
            var kdb = FindKarmaDatabase(sourcePath);
            if (kdb is not null) return await ImportAccessAsync(kdb, cancellationToken);
            var xml = Directory.EnumerateFiles(sourcePath, "datamain.xml", SearchOption.AllDirectories).FirstOrDefault()
                   ?? Directory.EnumerateFiles(sourcePath, "*.xml", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (xml is not null) return await ImportLegacyXmlAsync(xml, cancellationToken);
            return new KarmaImportResult(0,0,0,new[]{"No supported Karma database found in that folder."},Array.Empty<string>());
        }
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (ext is ".kdb" or ".mdb" or ".accdb") return await ImportAccessAsync(sourcePath, cancellationToken);
        if (ext == ".xml") return await ImportLegacyXmlAsync(sourcePath, cancellationToken);
        return new KarmaImportResult(0,0,0,new[]{"Unsupported Karma source."},Array.Empty<string>());
    }

    public Task<KarmaLibraryInspection> InspectLibraryAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        sourcePath = ResolveKarmaDatabasePath(sourcePath);
        cancellationToken.ThrowIfCancellationRequested();
        var warnings = new List<string>();
        var schemaSummary = new List<string>();
        KarmaLibraryColumnMapping? bestMapping = null;
        var bestScore = int.MinValue;
        long bestCount = 0;
        var previewRows = new List<KarmaLibraryPreviewRow>();

        try
        {
            using var connection = OpenAccess(sourcePath);
            connection.Open();
            foreach (var table in GetTableNames(connection))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var columns = GetColumnNames(connection, table);
                schemaSummary.Add($"{table}: {string.Join(", ", columns)}");
                if (columns.Count == 0) continue;

                var samples = ReadSampleRows(connection, table, columns, 20);
                var candidate = DetectLibraryMapping(table, columns, samples, out var score);
                if (candidate is null || score <= bestScore) continue;

                bestScore = score;
                bestMapping = candidate;
                bestCount = TryCountRows(connection, table);
                previewRows = BuildPreviewRows(sourcePath, candidate, columns, samples).Take(20).ToList();
            }
        }
        catch (Exception ex)
        {
            warnings.Add("Could not inspect the Karma database: " + ex.Message);
            warnings.Add("Hazz only opens Karma data READ-ONLY. If ACE OLE DB is missing, install the x64 Microsoft Access Database Engine.");
        }

        if (bestMapping is null)
            warnings.Add("Hazz could not safely identify a media-library table. The table/column list is shown so the mapping can be extended for this Karma version without guessing.");
        else if (previewRows.Count == 0)
            warnings.Add("A likely Karma media table was found, but no usable file paths were present in the first sample rows.");

        return Task.FromResult(new KarmaLibraryInspection(sourcePath, bestMapping, bestCount, previewRows, schemaSummary, warnings));
    }

    public async Task<KarmaLibraryImportResult> ImportLibraryAsync(
        string sourcePath,
        KarmaLibraryColumnMapping mapping,
        bool verifyFilePaths = false,
        IProgress<KarmaLibraryImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        sourcePath = ResolveKarmaDatabasePath(sourcePath);
        await database.InitializeAsync(cancellationToken);
        var warnings = new List<string>();
        if (!verifyFilePaths) warnings.Add("Fast import was used: file locations were copied from Karma without checking every file on disk. Use Hazz library verification/rescan if required.");
        long rowsRead = 0, imported = 0, missing = 0, errors = 0;
        var inferredRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var karma = OpenAccess(sourcePath);
        karma.Open();
        var columns = GetColumnNames(karma, mapping.TableName);
        if (columns.Count == 0) throw new InvalidDataException($"Karma table '{mapping.TableName}' could not be opened.");
        var ordinals = columns.Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

        using var sourceCommand = new OleDbCommand($"SELECT * FROM [{EscapeIdentifier(mapping.TableName)}]", karma);
        using var reader = sourceCommand.ExecuteReader(CommandBehavior.SequentialAccess)
            ?? throw new InvalidDataException("Karma media table returned no reader.");

        await using var hazz = new SqliteConnection(database.ConnectionString);
        await hazz.OpenAsync(cancellationToken);
        SqliteTransaction? tx = hazz.BeginTransaction();
        await using var insert = hazz.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
INSERT INTO songs(artist,title,manufacturer,disc_id,file_path,format,file_size,date_added,cdg_sync_seconds,preferred_key,last_seen_utc,media_kind)
VALUES($artist,$title,$manufacturer,$disc,$path,$format,$size,$added,0,0,CURRENT_TIMESTAMP,'Karaoke')
ON CONFLICT(file_path) DO UPDATE SET
 artist=CASE WHEN excluded.artist<>'' THEN excluded.artist ELSE songs.artist END,
 title=CASE WHEN excluded.title<>'' THEN excluded.title ELSE songs.title END,
 manufacturer=CASE WHEN excluded.manufacturer<>'' THEN excluded.manufacturer ELSE songs.manufacturer END,
 disc_id=CASE WHEN excluded.disc_id<>'' THEN excluded.disc_id ELSE songs.disc_id END,
 format=excluded.format,file_size=CASE WHEN excluded.file_size>0 THEN excluded.file_size ELSE songs.file_size END,
 date_added=COALESCE(songs.date_added,excluded.date_added),media_kind='Karaoke',last_seen_utc=CURRENT_TIMESTAMP;
""";
        var pArtist = insert.Parameters.Add("$artist", SqliteType.Text);
        var pTitle = insert.Parameters.Add("$title", SqliteType.Text);
        var pManufacturer = insert.Parameters.Add("$manufacturer", SqliteType.Text);
        var pDisc = insert.Parameters.Add("$disc", SqliteType.Text);
        var pPath = insert.Parameters.Add("$path", SqliteType.Text);
        var pFormat = insert.Parameters.Add("$format", SqliteType.Text);
        var pSize = insert.Parameters.Add("$size", SqliteType.Integer);
        var pAdded = insert.Parameters.Add("$added", SqliteType.Text);
        insert.Prepare();

        const int batchSize = 5000;
        var batch = 0;
        try
        {
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowsRead++;
                try
                {
                    var path = BuildMediaPath(sourcePath, mapping, ordinals, reader);
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    path = NormalizeImportedMediaPath(path, sourcePath);
                    var ext = Path.GetExtension(path);
                    if (!KnownMediaExtensions.Contains(ext)) continue;

                    var parsed = LibraryImportService.ParseName(path);
                    var artist = ReadMappedString(mapping.ArtistColumn, ordinals, reader);
                    var title = ReadMappedString(mapping.TitleColumn, ordinals, reader);
                    var manufacturer = ReadMappedString(mapping.ManufacturerColumn, ordinals, reader);
                    var disc = ReadMappedString(mapping.DiscIdColumn, ordinals, reader);
                    if (string.IsNullOrWhiteSpace(artist)) artist = parsed.Artist;
                    if (string.IsNullOrWhiteSpace(title)) title = parsed.Title;
                    if (string.IsNullOrWhiteSpace(manufacturer)) manufacturer = parsed.Manufacturer;
                    if (string.IsNullOrWhiteSpace(disc)) disc = parsed.DiscId;

                    long size = 0;
                    object added = DBNull.Value;
                    if (verifyFilePaths)
                    {
                        if (!File.Exists(path)) missing++;
                        else
                        {
                            var info = new FileInfo(path);
                            size = info.Length;
                            if (info.CreationTimeUtc != DateTime.MinValue) added = info.CreationTimeUtc.ToString("O");
                        }
                    }

                    pArtist.Value = artist.Trim();
                    pTitle.Value = title.Trim();
                    pManufacturer.Value = manufacturer.Trim();
                    pDisc.Value = disc.Trim();
                    pPath.Value = path;
                    pFormat.Value = ext.TrimStart('.').ToUpperInvariant();
                    pSize.Value = size;
                    pAdded.Value = added;
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                    imported++;
                    batch++;

                    var root = InferWatchRoot(path);
                    if (!string.IsNullOrWhiteSpace(root)) inferredRoots.Add(root);

                    if (batch >= batchSize)
                    {
                        await tx!.CommitAsync(cancellationToken);
                        await tx.DisposeAsync();
                        tx = hazz.BeginTransaction();
                        insert.Transaction = tx;
                        batch = 0;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    errors++;
                    if (warnings.Count < 100) warnings.Add($"Row {rowsRead:N0}: {ex.Message}");
                }

                if (rowsRead % 1000 == 0)
                    progress?.Report(new KarmaLibraryImportProgress(rowsRead, imported, missing, errors, SafeCurrentPath(mapping, ordinals, reader)));
            }
            await tx!.CommitAsync(cancellationToken);
        }
        catch
        {
            if (tx is not null) { try { await tx.RollbackAsync(CancellationToken.None); } catch { } }
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        var roots = CollapseRoots(inferredRoots).Take(64).ToArray();
        if (inferredRoots.Count > roots.Length)
            warnings.Add($"Karma paths implied {inferredRoots.Count:N0} top-level locations; Hazz retained the first {roots.Length:N0} non-overlapping watch roots. Add any others through Import Karaoke Folders.");
        if (roots.Length > 0)
        {
            var rootRepo = new LibraryRootRepository(database);
            await rootRepo.UpsertRootsAsync(roots, "Karaoke", true, cancellationToken);
        }

        progress?.Report(new KarmaLibraryImportProgress(rowsRead, imported, missing, errors, string.Empty));
        await LogImportAsync("Karma Karaoke Library", sourcePath, 0, 0,
            warnings.Concat(new[] {$"Rows read: {rowsRead:N0}", $"Karaoke records imported/updated: {imported:N0}"}).ToArray(), cancellationToken);
        return new KarmaLibraryImportResult(rowsRead, imported, missing, errors, roots, warnings);
    }

    private async Task<KarmaImportResult> ImportLegacyXmlAsync(string path, CancellationToken ct)
    {
        var doc = await LoadXmlAsync(path, ct);
        var warnings = new List<string>();
        var singerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in doc.Descendants())
        {
            if (IsSingerNameField(el.Name.LocalName)) AddCandidate(el.Value, singerNames);
            foreach (var a in el.Attributes()) if (IsSingerNameField(a.Name.LocalName)) AddCandidate(a.Value, singerNames);
        }

        var imported = 0;
        foreach (var name in singerNames)
        {
            ct.ThrowIfCancellationRequested();
            await singers.UpsertSingerAsync(name, "Imported from legacy Karma XML", ct);
            imported++;
        }
        if (imported == 0) warnings.Add("No unambiguous singer-name fields were found. Keep the source file and provide it for an exact schema mapping before importing history.");
        await LogImportAsync("Karma XML", path, imported, 0, warnings, ct);
        return new KarmaImportResult(imported,0,0,warnings,doc.Descendants().Select(x=>x.Name.LocalName).Distinct().Take(100).ToArray());
    }

    private async Task<KarmaImportResult> ImportAccessAsync(string path, CancellationToken ct)
    {
        await database.InitializeAsync(ct);
        var warnings = new List<string>();
        var sourceTables = new List<string>();
        var importedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var singerExternalIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var historyRows = 0;
        long performancesPreserved = 0;
        var songsMatched = 0;
        var historyTablesUsed = new List<string>();

        try
        {
            using var connection = OpenAccess(path);
            connection.Open();
            sourceTables.AddRange(GetTableNames(connection));

            // Pass 1: discover singer tables and build an external Singer-ID -> display-name map.
            // Karma generations often keep the name in one table and only SingerID in history rows.
            foreach (var table in sourceTables)
            {
                ct.ThrowIfCancellationRequested();
                var columns = GetColumnNames(connection, table);
                if (columns.Count == 0) continue;
                var singerCol = columns.FirstOrDefault(IsSingerNameField);
                if (singerCol is null) continue;
                var singerIdCol = FindBestIdColumn(columns, "singer");

                var selected = singerIdCol is null
                    ? $"[{EscapeIdentifier(singerCol)}]"
                    : $"[{EscapeIdentifier(singerIdCol)}],[{EscapeIdentifier(singerCol)}]";
                using var cmd = new OleDbCommand($"SELECT {selected} FROM [{EscapeIdentifier(table)}] WHERE [{EscapeIdentifier(singerCol)}] IS NOT NULL", connection);
                using var reader = cmd.ExecuteReader();
                if (reader is null) continue;
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    var nameOrdinal = singerIdCol is null ? 0 : 1;
                    var name = CleanText(Convert.ToString(reader[nameOrdinal]));
                    if (!IsUsableSingerName(name)) continue;
                    importedNames.Add(name);
                    if (singerIdCol is not null && !reader.IsDBNull(0))
                    {
                        var externalId = NormalizeExternalId(reader[0]);
                        if (externalId.Length > 0) singerExternalIds.TryAdd(externalId, name);
                    }
                }
            }

            // Create Hazz singers now, and retain their IDs for history import.
            var hazzSingerIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in importedNames)
            {
                ct.ThrowIfCancellationRequested();
                hazzSingerIds[name] = await singers.UpsertSingerAsync(name, "Imported from Karma KDB", ct);
            }

            // Find the most likely Karma media table. This lets history tables containing only SongID
            // resolve back to artist/title/file without a full media re-scan.
            KarmaLibraryColumnMapping? mediaMapping = null;
            string? mediaIdColumn = null;
            int bestMediaScore = int.MinValue;
            IReadOnlyList<string> mediaColumns = Array.Empty<string>();
            foreach (var table in sourceTables)
            {
                ct.ThrowIfCancellationRequested();
                var columns = GetColumnNames(connection, table);
                if (columns.Count == 0) continue;
                var samples = ReadSampleRows(connection, table, columns, 12);
                var candidate = DetectLibraryMapping(table, columns, samples, out var score);
                if (candidate is null || score <= bestMediaScore) continue;
                bestMediaScore = score;
                mediaMapping = candidate;
                mediaColumns = columns;
                mediaIdColumn = FindBestIdColumn(columns, "song") ?? FindBestIdColumn(columns, "track") ?? FindBestIdColumn(columns, "media") ?? FindBestIdColumn(columns, null);
            }

            using var hazz = new SqliteConnection(database.ConnectionString);
            await hazz.OpenAsync(ct);

            // Cache Karma media lookups because the same song is commonly sung by many singers.
            var mediaCache = new Dictionary<string, KarmaResolvedHistorySong>(StringComparer.OrdinalIgnoreCase);

            // Pass 2: identify history-like tables. A row is considered history only when it can
            // identify a singer (name or SingerID) AND a song (title/path or SongID).
            foreach (var table in sourceTables)
            {
                ct.ThrowIfCancellationRequested();
                var columns = GetColumnNames(connection, table);
                if (columns.Count == 0) continue;

                var singerNameCol = columns.FirstOrDefault(IsSingerNameField);
                var singerRefCol = columns.FirstOrDefault(IsSingerReferenceField);
                var titleCol = FindHistoryColumn(columns, HistoryField.Title);
                var artistCol = FindHistoryColumn(columns, HistoryField.Artist);
                var fileCol = FindHistoryColumn(columns, HistoryField.FilePath);
                var dateCol = FindHistoryColumn(columns, HistoryField.Date);
                var keyCol = FindHistoryColumn(columns, HistoryField.Key);
                var syncCol = FindHistoryColumn(columns, HistoryField.Sync);
                var countCol = FindHistoryColumn(columns, HistoryField.Count);
                var songRefCol = columns.FirstOrDefault(IsSongReferenceField);

                if (singerNameCol is null && singerRefCol is null) continue;
                if (titleCol is null && fileCol is null && songRefCol is null) continue;

                // Avoid treating the main singers table itself as history.
                if (titleCol is null && fileCol is null && songRefCol is null) continue;

                var wanted = new[] { singerNameCol, singerRefCol, titleCol, artistCol, fileCol, dateCol, keyCol, syncCol, countCol, songRefCol }
                    .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (wanted.Length == 0) continue;

                var select = string.Join(",", wanted.Select(x => $"[{EscapeIdentifier(x)}]"));
                using var cmd = new OleDbCommand($"SELECT {select} FROM [{EscapeIdentifier(table)}]", connection);
                using var reader = cmd.ExecuteReader();
                if (reader is null) continue;
                var ord = wanted.Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
                var tableImported = 0;

                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    string singerName = string.Empty;
                    if (singerNameCol is not null)
                    {
                        var rawSinger = ReadRawValue(reader, ord, singerNameCol);
                        var directSinger = CleanText(Convert.ToString(rawSinger, System.Globalization.CultureInfo.InvariantCulture));
                        var possibleSingerId = NormalizeExternalId(rawSinger);
                        if (importedNames.Contains(directSinger)) singerName = directSinger;
                        else if (possibleSingerId.Length > 0 && singerExternalIds.TryGetValue(possibleSingerId, out var mappedDirectSinger)) singerName = mappedDirectSinger;
                        else singerName = directSinger;
                    }
                    if (!IsUsableSingerName(singerName) && singerRefCol is not null)
                    {
                        var externalSinger = NormalizeExternalId(ReadRawValue(reader, ord, singerRefCol));
                        if (externalSinger.Length > 0 && singerExternalIds.TryGetValue(externalSinger, out var mappedSinger)) singerName = mappedSinger;
                    }
                    if (!IsUsableSingerName(singerName)) continue;

                    if (!hazzSingerIds.TryGetValue(singerName, out var singerId))
                    {
                        singerId = await singers.UpsertSingerAsync(singerName, "Imported from Karma KDB history", ct);
                        hazzSingerIds[singerName] = singerId;
                        importedNames.Add(singerName);
                    }

                    var rawTitle = titleCol is null ? null : ReadRawValue(reader, ord, titleCol);
                    var title = titleCol is null ? string.Empty : CleanText(Convert.ToString(rawTitle, System.Globalization.CultureInfo.InvariantCulture));
                    var artist = artistCol is null ? string.Empty : CleanText(ReadValue(reader, ord, artistCol));
                    var mediaPath = fileCol is null ? string.Empty : CleanText(ReadValue(reader, ord, fileCol));
                    if (mediaPath.Length > 0) mediaPath = NormalizeImportedMediaPath(mediaPath, path);
                    var externalSongId = songRefCol is null ? string.Empty : NormalizeExternalId(ReadRawValue(reader, ord, songRefCol));
                    // Some Karma generations call the foreign-key column simply "Song"/"Track".
                    // If that field is numeric/GUID-like, treat it as a reference rather than a title.
                    if (externalSongId.Length == 0 && LooksLikeReferenceValue(rawTitle) && mediaMapping is not null && mediaIdColumn is not null)
                    {
                        externalSongId = NormalizeExternalId(rawTitle);
                        title = string.Empty;
                    }

                    KarmaResolvedHistorySong? resolved = null;
                    if ((title.Length == 0 && mediaPath.Length == 0) && externalSongId.Length > 0 && mediaMapping is not null && mediaIdColumn is not null)
                    {
                        if (!mediaCache.TryGetValue(externalSongId, out var cached))
                        {
                            cached = ResolveKarmaMediaByExternalId(connection, path, mediaMapping, mediaColumns, mediaIdColumn, externalSongId);
                            mediaCache[externalSongId] = cached;
                        }
                        resolved = cached;
                        if (resolved is not null)
                        {
                            if (title.Length == 0) title = resolved.Title;
                            if (artist.Length == 0) artist = resolved.Artist;
                            if (mediaPath.Length == 0) mediaPath = resolved.FilePath;
                        }
                    }

                    // Ignore rows that still do not describe a song after resolving references.
                    if (title.Length == 0 && mediaPath.Length == 0) continue;
                    if (title.Length == 0 && mediaPath.Length > 0) title = LibraryImportService.ParseName(mediaPath).Title;
                    if (artist.Length == 0 && mediaPath.Length > 0) artist = LibraryImportService.ParseName(mediaPath).Artist;

                    var sungAt = dateCol is null
                        ? GetSourceFallbackDate(path)
                        : ParseKarmaDate(ReadRawValue(reader, ord, dateCol), GetSourceFallbackDate(path));
                    var key = keyCol is null ? 0 : ParseInt(ReadRawValue(reader, ord, keyCol), 0, -6, 6);
                    var sync = syncCol is null ? 0.0 : ParseDouble(ReadRawValue(reader, ord, syncCol), 0.0, -10.0, 10.0);
                    var timesSung = countCol is null ? 1 : ParseInt(ReadRawValue(reader, ord, countCol), 1, 1, 1_000_000);

                    long? songId = null;
                    if (mediaPath.Length > 0)
                        songId = await FindHazzSongByPathAsync(hazz, mediaPath, ct);
                    if (songId is null && (title.Length > 0 || artist.Length > 0))
                        songId = await FindHazzSongByArtistTitleAsync(hazz, artist, title, ct);
                    if (songId is not null) songsMatched++;

                    var importedFrom = $"Karma KDB|{Path.GetFullPath(path)}|{table}";
                    var historyInserted = await InsertKarmaHistoryIfMissingAsync(hazz, singerId, songId, artist, title, mediaPath, sungAt, key, sync, timesSung, importedFrom, ct);
                    performancesPreserved += timesSung;
                    if (historyInserted)
                    {
                        historyRows++;
                        tableImported++;
                    }
                }

                if (tableImported > 0) historyTablesUsed.Add($"{table} ({tableImported:N0})");
            }

            if (historyRows == 0)
            {
                warnings.Add("Singer names were imported, but no linked Karma history rows could be resolved automatically. If this Karma version uses different field names, provide the KDB and Hazz can add an exact schema adapter without altering the source database.");
            }
            else
            {
                warnings.Add($"Imported {historyRows:N0} singer-history song row(s) from: {string.Join(", ", historyTablesUsed)}.");
                if (performancesPreserved > historyRows) warnings.Add($"Preserved Karma song counts representing {performancesPreserved:N0} total singer performances.");
                if (songsMatched > 0) warnings.Add($"Matched {songsMatched:N0} history row(s) back to tracks already in the Hazz karaoke library.");
            }
        }
        catch (Exception ex)
        {
            warnings.Add("KDB import could not be completed: " + ex.Message);
            warnings.Add("Hazz did not modify the Karma file. If ACE OLE DB is missing, install the Microsoft Access Database Engine matching the app architecture.");
        }

        var count = importedNames.Count;
        if (sourceTables.Count > 0 && count == 0)
            warnings.Add("The KDB opened, but its singer fields were not recognised. The source was left unchanged.");

        await LogImportAsync("Karma KDB", path, count, historyRows, warnings, ct);
        return new KarmaImportResult(count, historyRows, songsMatched, warnings, sourceTables);
    }

    private enum HistoryField { Title, Artist, FilePath, Date, Key, Sync, Count }

    private sealed record KarmaResolvedHistorySong(string Artist, string Title, string FilePath);

    private static string? FindHistoryColumn(IReadOnlyList<string> columns, HistoryField field)
    {
        string? best = null;
        var bestScore = int.MinValue;
        foreach (var col in columns)
        {
            var n = NormalizeFieldName(col);
            var score = field switch
            {
                HistoryField.Title => n switch
                {
                    "title" => 120, "songtitle" => 140, "tracktitle" => 130, "songname" => 120, "trackname" => 110,
                    "song" => 70, "track" => 60, _ when n.Contains("title") => 80, _ => -100
                },
                HistoryField.Artist => n switch
                {
                    "artist" => 140, "songartist" => 150, "trackartist" => 140, "performer" => 100, "originalartist" => 120,
                    _ when n.Contains("artist") => 90, _ => -100
                },
                HistoryField.FilePath =>
                    (n.Contains("filepath") ? 150 : n.Contains("filename") ? 125 : n is "file" or "songfile" or "trackfile" ? 115 : n.Contains("path") ? 90 : n.Contains("location") ? 60 : -100)
                    - (n.Contains("image") || n.Contains("picture") || n.Contains("cover") ? 200 : 0),
                HistoryField.Date => n switch
                {
                    "sungat" => 170, "sungdate" => 165, "playedat" => 160, "playeddate" => 155, "performancedate" => 150,
                    "historydate" => 145, "datetime" => 110, "date" => 90, "time" => 50,
                    _ when n.Contains("sung") && (n.Contains("date") || n.Contains("time")) => 150,
                    _ when n.Contains("played") && (n.Contains("date") || n.Contains("time")) => 145,
                    _ when n.Contains("date") => 70, _ => -100
                },
                HistoryField.Key => n switch
                {
                    "key" => 120, "keychange" => 150, "pitch" => 90, "pitchchange" => 110,
                    _ when n.Contains("key") => 80, _ => -100
                },
                HistoryField.Sync => n switch
                {
                    "sync" => 120, "avsync" => 150, "cdgsync" => 160, "offset" => 80, "timingoffset" => 120,
                    _ when n.Contains("sync") => 100, _ => -100
                },
                HistoryField.Count => n switch
                {
                    "timesung" => 220, "timessung" => 220, "timesang" => 215, "timessang" => 215,
                    "singcount" => 210, "sungcount" => 210, "songcount" => 185, "playcount" => 200,
                    "timesplayed" => 200, "plays" => 175, "playstotal" => 190, "performedcount" => 195,
                    "numberoftimes" => 180, "nooftimes" => 180, "count" => 100,
                    _ when (n.Contains("count") || n.Contains("times")) &&
                           (n.Contains("sing") || n.Contains("sung") || n.Contains("sang") || n.Contains("play") || n.Contains("song")) => 170,
                    _ => -100
                },
                _ => -100
            };
            if (score > bestScore) { bestScore = score; best = col; }
        }
        return bestScore > 0 ? best : null;
    }

    private static string? FindBestIdColumn(IReadOnlyList<string> columns, string? subject)
    {
        string? best = null;
        var bestScore = int.MinValue;
        foreach (var col in columns)
        {
            var n = NormalizeFieldName(col);
            var score = n == "id" ? 100 : n.EndsWith("id", StringComparison.Ordinal) ? 60 : -100;
            if (!string.IsNullOrWhiteSpace(subject))
            {
                if (n == subject + "id" || n == "id" + subject) score += 120;
                else if (n.Contains(subject, StringComparison.Ordinal) && (n.EndsWith("id", StringComparison.Ordinal) || n.StartsWith("id", StringComparison.Ordinal))) score += 80;
            }
            if (score > bestScore) { bestScore = score; best = col; }
        }
        return bestScore > 0 ? best : null;
    }

    private static bool IsSingerReferenceField(string value)
    {
        var n = NormalizeFieldName(value);
        return n is "singerid" or "idsinger" or "singerkey" or "singerref" or "singerindex" or "performerid" or "idperformer" or "userid" or "userkey"
            || ((n.Contains("singer") || n.Contains("performer")) && (n.EndsWith("id") || n.StartsWith("id") || n.Contains("ref") || n.Contains("key") || n.Contains("index")));
    }

    private static bool IsSongReferenceField(string value)
    {
        var n = NormalizeFieldName(value);
        return n is "songid" or "idsong" or "trackid" or "idtrack" or "mediaid" or "idmedia" or "karaokeid" or "fileid" or "idfile" or "songkey" or "trackkey"
            || ((n.Contains("song") || n.Contains("track") || n.Contains("media") || n.Contains("karaoke") || n.Contains("file")) &&
                (n.EndsWith("id") || n.StartsWith("id") || n.Contains("ref") || n.Contains("key") || n.Contains("index")));
    }

    private static string CleanText(string? value)
        => (value ?? string.Empty).Replace('\0', ' ').Trim().Trim('"', '\'');

    private static bool IsUsableSingerName(string value)
        => value.Length is >= 1 and <= 120 && !value.Contains('\\') && !value.Contains('/') && !KnownMediaExtensions.Any(x => value.EndsWith(x, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeReferenceValue(object? value)
    {
        if (value is null || value is DBNull) return false;
        return value is byte or sbyte or short or ushort or int or uint or long or ulong or decimal or Guid;
    }

    private static string NormalizeExternalId(object? value)
    {
        if (value is null || value is DBNull) return string.Empty;
        if (value is Guid g) return g.ToString("D");
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static object? ReadRawValue(IDataRecord reader, IReadOnlyDictionary<string, int> ordinals, string column)
        => ordinals.TryGetValue(column, out var ordinal) && !reader.IsDBNull(ordinal) ? reader.GetValue(ordinal) : null;

    private static string ReadValue(IDataRecord reader, IReadOnlyDictionary<string, int> ordinals, string column)
        => CleanText(Convert.ToString(ReadRawValue(reader, ordinals, column), System.Globalization.CultureInfo.InvariantCulture));

    private static DateTimeOffset GetSourceFallbackDate(string path)
    {
        try { return new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero); }
        catch { return DateTimeOffset.UtcNow; }
    }

    private static DateTimeOffset ParseKarmaDate(object? value, DateTimeOffset fallback)
    {
        if (value is null || value is DBNull) return fallback;
        if (value is DateTime dt)
            return dt.Kind == DateTimeKind.Utc ? new DateTimeOffset(dt) : new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Local)).ToUniversalTime();
        if (value is DateTimeOffset dto) return dto.ToUniversalTime();
        if (value is double oa)
        {
            try { return new DateTimeOffset(DateTime.FromOADate(oa), TimeZoneInfo.Local.GetUtcOffset(DateTime.FromOADate(oa))).ToUniversalTime(); }
            catch { }
        }
        var text = Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture);
        if (DateTimeOffset.TryParse(text, out var parsed)) return parsed.ToUniversalTime();
        if (DateTime.TryParse(text, out var parsedDt)) return new DateTimeOffset(parsedDt).ToUniversalTime();
        return fallback;
    }

    private static int ParseInt(object? value, int fallback, int min, int max)
    {
        if (value is null || value is DBNull) return fallback;
        if (int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out var i)) return Math.Clamp(i, min, max);
        return fallback;
    }

    private static double ParseDouble(object? value, double fallback, double min, double max)
    {
        if (value is null || value is DBNull) return fallback;
        if (double.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
            return Math.Clamp(d, min, max);
        return fallback;
    }

    private static KarmaResolvedHistorySong ResolveKarmaMediaByExternalId(
        OleDbConnection connection, string sourcePath, KarmaLibraryColumnMapping mapping, IReadOnlyList<string> mediaColumns,
        string mediaIdColumn, string externalId)
    {
        try
        {
            using var cmd = new OleDbCommand($"SELECT TOP 1 * FROM [{EscapeIdentifier(mapping.TableName)}] WHERE [{EscapeIdentifier(mediaIdColumn)}]=?", connection);
            object idValue = externalId;
            if (long.TryParse(externalId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var numericId)) idValue = numericId;
            else if (Guid.TryParse(externalId, out var guidId)) idValue = guidId;
            cmd.Parameters.AddWithValue("@id", idValue);
            using var reader = cmd.ExecuteReader();
            if (reader is null || !reader.Read()) return new KarmaResolvedHistorySong(string.Empty, string.Empty, string.Empty);
            var ord = mediaColumns.Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
            var title = mapping.TitleColumn is null ? string.Empty : CleanText(ReadValue(reader, ord, mapping.TitleColumn));
            var artist = mapping.ArtistColumn is null ? string.Empty : CleanText(ReadValue(reader, ord, mapping.ArtistColumn));
            var path = BuildMediaPath(sourcePath, mapping, ord, reader);
            if (!string.IsNullOrWhiteSpace(path)) path = NormalizeImportedMediaPath(path, sourcePath);
            return new KarmaResolvedHistorySong(artist, title, path);
        }
        catch
        {
            return new KarmaResolvedHistorySong(string.Empty, string.Empty, string.Empty);
        }
    }

    private static async Task<long?> FindHazzSongByPathAsync(SqliteConnection connection, string path, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id FROM songs WHERE file_path=$path LIMIT 1";
        cmd.Parameters.AddWithValue("$path", path);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is null || value is DBNull ? null : Convert.ToInt64(value);
    }

    private static async Task<long?> FindHazzSongByArtistTitleAsync(SqliteConnection connection, string artist, string title, CancellationToken ct)
    {
        if (title.Length == 0) return null;
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
SELECT id FROM songs
WHERE media_kind='Karaoke'
  AND title=$title COLLATE NOCASE
  AND ($artist='' OR artist=$artist COLLATE NOCASE)
ORDER BY CASE WHEN artist=$artist COLLATE NOCASE THEN 0 ELSE 1 END,id
LIMIT 1;
""";
        cmd.Parameters.AddWithValue("$artist", artist);
        cmd.Parameters.AddWithValue("$title", title);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is null || value is DBNull ? null : Convert.ToInt64(value);
    }

    private static async Task<bool> InsertKarmaHistoryIfMissingAsync(
        SqliteConnection connection, long singerId, long? songId, string artist, string title, string filePath,
        DateTimeOffset sungAt, int key, double sync, int timesSung, string importedFrom, CancellationToken ct)
    {
        timesSung = Math.Clamp(timesSung, 1, 1_000_000);
        var date = sungAt.ToUniversalTime().ToString("O");

        await using (var find = connection.CreateCommand())
        {
            find.CommandText = """
SELECT id,times_sung FROM singer_history
WHERE singer_id=$singer AND imported_from=$source
  AND title=$title COLLATE NOCASE AND artist=$artist COLLATE NOCASE
  AND file_path=$path COLLATE NOCASE AND sung_at_utc=$date
LIMIT 1;
""";
            find.Parameters.AddWithValue("$singer", singerId);
            find.Parameters.AddWithValue("$source", importedFrom);
            find.Parameters.AddWithValue("$title", title);
            find.Parameters.AddWithValue("$artist", artist);
            find.Parameters.AddWithValue("$path", filePath);
            find.Parameters.AddWithValue("$date", date);
            await using var r = await find.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                var id = r.GetInt64(0);
                var existingTimes = r.IsDBNull(1) ? 1 : r.GetInt32(1);
                await r.DisposeAsync();
                await using var update = connection.CreateCommand();
                update.CommandText = """
UPDATE singer_history
SET song_id=COALESCE(song_id,$song), key_change=$key, cdg_sync_seconds=$sync,
    times_sung=CASE WHEN times_sung < $times THEN $times ELSE times_sung END
WHERE id=$id;
""";
                update.Parameters.AddWithValue("$song", songId ?? (object)DBNull.Value);
                update.Parameters.AddWithValue("$key", key);
                update.Parameters.AddWithValue("$sync", sync);
                update.Parameters.AddWithValue("$times", Math.Max(existingTimes, timesSung));
                update.Parameters.AddWithValue("$id", id);
                await update.ExecuteNonQueryAsync(ct);
                return false;
            }
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
INSERT INTO singer_history(singer_id,song_id,artist,title,file_path,sung_at_utc,key_change,cdg_sync_seconds,times_sung,imported_from)
VALUES($singer,$song,$artist,$title,$path,$date,$key,$sync,$times,$source);
""";
        cmd.Parameters.AddWithValue("$singer", singerId);
        cmd.Parameters.AddWithValue("$song", songId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$artist", artist);
        cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$path", filePath);
        cmd.Parameters.AddWithValue("$date", date);
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$sync", sync);
        cmd.Parameters.AddWithValue("$times", timesSung);
        cmd.Parameters.AddWithValue("$source", importedFrom);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    private static KarmaLibraryColumnMapping? DetectLibraryMapping(string table, IReadOnlyList<string> columns, IReadOnlyList<object?[]> samples, out int score)
    {
        score = 0;
        if (columns.Count == 0) return null;

        var fileScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var folderScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columns)
        {
            var norm = NormalizeFieldName(col);
            var fileScore = 0;
            if (norm.Contains("filepath")) fileScore += 120;
            else if (norm.Contains("filename")) fileScore += 100;
            else if (norm is "file" or "mediafile" or "songfile" or "trackfile") fileScore += 80;
            else if (norm.Contains("path")) fileScore += 65;
            else if (norm.Contains("location")) fileScore += 35;
            if (norm.Contains("image") || norm.Contains("picture") || norm.Contains("cover")) fileScore -= 100;

            var folderScore = 0;
            if (norm.Contains("folder") || norm.Contains("directory")) folderScore += 90;
            else if (norm is "path" or "mediapath" or "songpath") folderScore += 60;

            var ordinal = columns.IndexOf(col);
            foreach (var row in samples)
            {
                if (ordinal < 0 || ordinal >= row.Length) continue;
                var value = Convert.ToString(row[ordinal])?.Trim() ?? string.Empty;
                if (value.Length == 0) continue;
                var ext = Path.GetExtension(value);
                if (KnownMediaExtensions.Contains(ext)) fileScore += 22;
                if ((value.Contains('\\') || value.Contains('/')) && KnownMediaExtensions.Contains(ext)) fileScore += 14;
                if ((value.Contains('\\') || value.Contains('/')) && !KnownMediaExtensions.Contains(ext)) folderScore += 5;
            }
            fileScores[col] = fileScore;
            folderScores[col] = folderScore;
        }

        var pathColumn = fileScores.OrderByDescending(x => x.Value).First();
        if (pathColumn.Value < 40) return null;
        var pathOrdinal = columns.IndexOf(pathColumn.Key);
        var fullPathSamples = samples.Count(row => pathOrdinal >= 0 && pathOrdinal < row.Length && LooksLikeFullMediaPath(Convert.ToString(row[pathOrdinal]) ?? string.Empty));
        string? folderColumn = null;
        if (fullPathSamples == 0)
        {
            var folder = folderScores.Where(x => !x.Key.Equals(pathColumn.Key, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Value).FirstOrDefault();
            if (folder.Value >= 40) folderColumn = folder.Key;
        }

        var artist = RejectLikelyForeignKey(BestNamedColumn(columns, "artist", "artistname", "performer", "performername"), columns, samples);
        var title = RejectLikelyForeignKey(BestNamedColumn(columns, "title", "songtitle", "tracktitle", "songname", "trackname"), columns, samples);
        if (title is null)
        {
            var song = columns.FirstOrDefault(c => NormalizeFieldName(c) is "song" or "name");
            if (song is not null && !song.Equals(pathColumn.Key, StringComparison.OrdinalIgnoreCase)) title = RejectLikelyForeignKey(song, columns, samples);
        }
        var manufacturer = RejectLikelyForeignKey(BestNamedColumn(columns, "manufacturer", "manu", "mfr", "brand", "label"), columns, samples);
        var disc = BestNamedColumn(columns, "discid", "discnumber", "discno", "disc", "catalog", "catalogue", "code");

        score = pathColumn.Value + (artist is null ? 0 : 25) + (title is null ? 0 : 25) + (manufacturer is null ? 0 : 6) + (disc is null ? 0 : 8);
        var tableNorm = NormalizeFieldName(table);
        if (tableNorm.Contains("song") || tableNorm.Contains("track") || tableNorm.Contains("media") || tableNorm.Contains("karaoke")) score += 20;
        if (tableNorm.Contains("history") || tableNorm.Contains("singer")) score -= 15;
        return new KarmaLibraryColumnMapping(table, pathColumn.Key, folderColumn, artist, title, manufacturer, disc);
    }

    private static List<object?[]> ReadSampleRows(OleDbConnection connection, string table, IReadOnlyList<string> columns, int count)
    {
        var result = new List<object?[]>();
        try
        {
            using var command = new OleDbCommand($"SELECT TOP {Math.Clamp(count, 1, 100)} * FROM [{EscapeIdentifier(table)}]", connection);
            using var reader = command.ExecuteReader();
            if (reader is null) return result;
            while (reader.Read())
            {
                var row = new object[reader.FieldCount];
                reader.GetValues(row);
                result.Add(row);
            }
        }
        catch { }
        return result;
    }

    private static IEnumerable<KarmaLibraryPreviewRow> BuildPreviewRows(string sourcePath, KarmaLibraryColumnMapping mapping, IReadOnlyList<string> columns, IReadOnlyList<object?[]> samples)
    {
        var ordinals = columns.Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
        foreach (var row in samples)
        {
            var path = BuildMediaPath(sourcePath, mapping, ordinals, row);
            if (string.IsNullOrWhiteSpace(path)) continue;
            path = NormalizeImportedMediaPath(path, sourcePath);
            if (!KnownMediaExtensions.Contains(Path.GetExtension(path))) continue;
            var parsed = LibraryImportService.ParseName(path);
            var artist = ReadMappedString(mapping.ArtistColumn, ordinals, row);
            var title = ReadMappedString(mapping.TitleColumn, ordinals, row);
            var manufacturer = ReadMappedString(mapping.ManufacturerColumn, ordinals, row);
            var disc = ReadMappedString(mapping.DiscIdColumn, ordinals, row);
            yield return new KarmaLibraryPreviewRow(
                string.IsNullOrWhiteSpace(artist) ? parsed.Artist : artist,
                string.IsNullOrWhiteSpace(title) ? parsed.Title : title,
                string.IsNullOrWhiteSpace(manufacturer) ? parsed.Manufacturer : manufacturer,
                string.IsNullOrWhiteSpace(disc) ? parsed.DiscId : disc,
                path);
        }
    }

    private static string BuildMediaPath(string sourcePath, KarmaLibraryColumnMapping mapping, IReadOnlyDictionary<string,int> ordinals, IDataRecord row)
    {
        var file = ReadMappedString(mapping.PathColumn, ordinals, row);
        var folder = ReadMappedString(mapping.FolderColumn, ordinals, row);
        return CombineFolderAndFile(folder, file);
    }

    private static string BuildMediaPath(string sourcePath, KarmaLibraryColumnMapping mapping, IReadOnlyDictionary<string,int> ordinals, object?[] row)
    {
        var file = ReadMappedString(mapping.PathColumn, ordinals, row);
        var folder = ReadMappedString(mapping.FolderColumn, ordinals, row);
        return CombineFolderAndFile(folder, file);
    }

    private static string CombineFolderAndFile(string folder, string file)
    {
        file = file.Trim().Trim('"');
        folder = folder.Trim().Trim('"');
        if (file.Length == 0) return string.Empty;
        if (Path.IsPathRooted(file) || folder.Length == 0) return file;
        try { return Path.Combine(folder, file); }
        catch { return file; }
    }

    private static string NormalizeImportedMediaPath(string path, string sourcePath)
    {
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')).Replace('/', Path.DirectorySeparatorChar);
        if (!Path.IsPathRooted(path))
        {
            var baseDir = Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
            try { path = Path.Combine(baseDir, path); } catch { }
        }
        try { return Path.GetFullPath(path); } catch { return path; }
    }

    private static string ReadMappedString(string? column, IReadOnlyDictionary<string,int> ordinals, IDataRecord row)
    {
        if (string.IsNullOrWhiteSpace(column) || !ordinals.TryGetValue(column, out var ordinal) || ordinal < 0 || ordinal >= row.FieldCount || row.IsDBNull(ordinal)) return string.Empty;
        return Convert.ToString(row.GetValue(ordinal))?.Trim() ?? string.Empty;
    }

    private static string ReadMappedString(string? column, IReadOnlyDictionary<string,int> ordinals, object?[] row)
    {
        if (string.IsNullOrWhiteSpace(column) || !ordinals.TryGetValue(column, out var ordinal) || ordinal < 0 || ordinal >= row.Length || row[ordinal] is null || row[ordinal] == DBNull.Value) return string.Empty;
        return Convert.ToString(row[ordinal])?.Trim() ?? string.Empty;
    }

    private static string SafeCurrentPath(KarmaLibraryColumnMapping mapping, IReadOnlyDictionary<string,int> ordinals, IDataRecord row)
    {
        try { return ReadMappedString(mapping.PathColumn, ordinals, row); } catch { return string.Empty; }
    }

    private static bool LooksLikeFullMediaPath(string value)
    {
        value = value.Trim();
        return (value.Contains('\\') || value.Contains('/')) && KnownMediaExtensions.Contains(Path.GetExtension(value));
    }

    private static string? RejectLikelyForeignKey(string? column, IReadOnlyList<string> columns, IReadOnlyList<object?[]> samples)
    {
        if (string.IsNullOrWhiteSpace(column)) return null;
        var norm = NormalizeFieldName(column);
        if (norm.EndsWith("id", StringComparison.Ordinal) && norm is not "discid") return null;
        var ordinal = columns.IndexOf(column);
        if (ordinal < 0) return column;
        var values = samples.Where(r => ordinal < r.Length && r[ordinal] is not null && r[ordinal] != DBNull.Value)
            .Select(r => Convert.ToString(r[ordinal])?.Trim() ?? string.Empty).Where(v => v.Length > 0).Take(12).ToArray();
        if (values.Length >= 3 && values.Count(v => long.TryParse(v, out _)) >= Math.Ceiling(values.Length * 0.8)) return null;
        return column;
    }

    private static string? BestNamedColumn(IReadOnlyList<string> columns, params string[] desired)
    {
        foreach (var wanted in desired)
        {
            var exact = columns.FirstOrDefault(c => NormalizeFieldName(c) == wanted);
            if (exact is not null) return exact;
        }
        foreach (var wanted in desired)
        {
            var partial = columns.FirstOrDefault(c => NormalizeFieldName(c).Contains(wanted, StringComparison.Ordinal));
            if (partial is not null) return partial;
        }
        return null;
    }

    private static string NormalizeFieldName(string value)
        => new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static long TryCountRows(OleDbConnection connection, string table)
    {
        try
        {
            using var command = new OleDbCommand($"SELECT COUNT(*) FROM [{EscapeIdentifier(table)}]", connection);
            return Convert.ToInt64(command.ExecuteScalar());
        }
        catch { return 0; }
    }

    private static List<string> GetColumnNames(OleDbConnection connection, string table)
    {
        using var command = new OleDbCommand($"SELECT * FROM [{EscapeIdentifier(table)}] WHERE 1=0", connection);
        using var reader = command.ExecuteReader(CommandBehavior.SchemaOnly);
        if (reader is null) return new List<string>();
        return Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    private static string ResolveKarmaDatabasePath(string sourcePath)
    {
        sourcePath = NormalizeSource(sourcePath);
        if (Directory.Exists(sourcePath))
            return FindKarmaDatabase(sourcePath) ?? throw new FileNotFoundException("No KDB/MDB/ACCDB Karma database was found in that folder.", sourcePath);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Karma database not found.", sourcePath);
        var ext = Path.GetExtension(sourcePath);
        if (!ext.Equals(".kdb", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".mdb", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".accdb", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Karma karaoke-library import requires a KDB/MDB/ACCDB database.");
        return sourcePath;
    }

    private static string? FindKarmaDatabase(string folder)
        => Directory.EnumerateFiles(folder, "*.kdb", SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
        ?? Directory.EnumerateFiles(folder, "*.mdb", SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
        ?? Directory.EnumerateFiles(folder, "*.accdb", SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();

    private static bool IsKarmaDatabaseOrXml(string p)
    {
        var ext = Path.GetExtension(p);
        return ext.Equals(".kdb", StringComparison.OrdinalIgnoreCase) || ext.Equals(".mdb", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".accdb", StringComparison.OrdinalIgnoreCase) || ext.Equals(".xml", StringComparison.OrdinalIgnoreCase);
    }

    private static string? InferWatchRoot(string mediaPath)
    {
        try
        {
            if (!Path.IsPathRooted(mediaPath)) return null;
            var root = Path.GetPathRoot(mediaPath);
            if (string.IsNullOrWhiteSpace(root)) return null;
            var directory = Path.GetDirectoryName(mediaPath);
            if (string.IsNullOrWhiteSpace(directory)) return root;
            var relative = Path.GetRelativePath(root, directory);
            if (relative == ".") return Path.TrimEndingDirectorySeparator(root);
            var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault(x => x.Length > 0);
            return string.IsNullOrWhiteSpace(first) ? Path.TrimEndingDirectorySeparator(root) : Path.Combine(root, first);
        }
        catch { return null; }
    }

    private static string[] CollapseRoots(IEnumerable<string> roots)
    {
        var ordered = roots.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => Path.TrimEndingDirectorySeparator(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Length).ToList();
        var kept = new List<string>();
        foreach (var candidate in ordered)
        {
            if (kept.Any(parent => string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase)
                                || candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) continue;
            kept.Add(candidate);
        }
        return kept.ToArray();
    }

    private static bool IsSingerNameField(string value)
    {
        var n = value.Replace("_", "").Replace(" ", "").ToLowerInvariant();
        return n is "singer" or "singername" or "screenname" or "displayname" or "kjname";
    }

    private static void AddCandidate(string value, HashSet<string> set)
    {
        value = value.Trim();
        if (value.Length is < 1 or > 120) return;
        if (value.Contains('\\') || value.Contains('/') || value.Contains(".mp3", StringComparison.OrdinalIgnoreCase)) return;
        set.Add(value);
    }

    private static string NormalizeSource(string source) => Environment.ExpandEnvironmentVariables(source.Trim().Trim('"'));

    private static OleDbConnection OpenAccess(string path)
    {
        var cs = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={path};Persist Security Info=False;Mode=Read;";
        return new OleDbConnection(cs);
    }

    private static IReadOnlyList<string> ReadAccessTableNames(string path)
    {
        using var c = OpenAccess(path); c.Open(); return GetTableNames(c);
    }

    private static List<string> GetTableNames(OleDbConnection c)
    {
        var dt = c.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, new object?[]{null,null,null,"TABLE"});
        if (dt is null) return new List<string>();
        return dt.Rows.Cast<DataRow>().Select(r => Convert.ToString(r["TABLE_NAME"]) ?? string.Empty)
            .Where(n => n.Length > 0 && !n.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
    }

    private static string EscapeIdentifier(string value) => value.Replace("]", "]]", StringComparison.Ordinal);

    private static async Task<XDocument> LoadXmlAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await XDocument.LoadAsync(stream, LoadOptions.None, ct);
    }

    private async Task LogImportAsync(string type, string path, int singersImported, int historyRows, IReadOnlyList<string> warnings, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO import_log(source_type,source_path,singers_imported,history_rows_imported,report_json) VALUES($t,$p,$s,$h,$r)";
        cmd.Parameters.AddWithValue("$t", type); cmd.Parameters.AddWithValue("$p", path); cmd.Parameters.AddWithValue("$s", singersImported);
        cmd.Parameters.AddWithValue("$h", historyRows); cmd.Parameters.AddWithValue("$r", JsonSerializer.Serialize(warnings));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

internal static class ReadOnlyListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> values, string value)
    {
        for (var i = 0; i < values.Count; i++) if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}
