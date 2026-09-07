using System.Text;
using System.Text.RegularExpressions;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

/// <summary>
/// Read-only migration of BPM Studio playlist/file-archive data into Hazz.
/// BPM Studio source files are never modified. The importer understands ordinary
/// M3U/PLS plus BPM Studio LST/GRP files when they contain recoverable file paths.
/// </summary>
public sealed class BpmStudioImportService(HazzDatabase database) : IBpmStudioImportService
{
    private static readonly HashSet<string> SupportedListExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".lst", ".m3u", ".m3u8", ".pls", ".grp", ".plg" };

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".aif", ".aiff", ".mp4", ".mkv", ".avi", ".wmv", ".mov", ".mpeg", ".mpg", ".m4v", ".vob" };

    private static readonly Regex AbsoluteMediaPath = new(
        "(?im)(?<path>(?:[A-Z]:\\\\|\\\\\\\\)[^\r\n\0<>\"|]+?\\.(?:mp3|wav|wma|m4a|aac|flac|ogg|aif|aiff|mp4|mkv|avi|wmv|mov|mpeg|mpg|m4v|vob))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Task<BpmStudioImportPreview> PreviewAsync(string sourcePath, CancellationToken cancellationToken = default)
        => Task.Run(() => Preview(sourcePath, cancellationToken), cancellationToken);

    public Task<BpmStudioImportResult> ImportAsync(
        string sourcePath,
        IProgress<BpmStudioImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.Run(() => ImportWorkerAsync(sourcePath, progress, cancellationToken), cancellationToken);

    private async Task<BpmStudioImportResult> ImportWorkerAsync(
        string sourcePath,
        IProgress<BpmStudioImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        if (!Directory.Exists(sourcePath) && !File.Exists(sourcePath))
            throw new FileNotFoundException("The selected BPM Studio data folder/file does not exist.", sourcePath);

        progress?.Report(new BpmStudioImportProgress(
            "Preparing fast BPM Studio import…", 0, 0, sourcePath, 0, 0, 0));

        await database.InitializeAsync(cancellationToken).ConfigureAwait(false);

        // Fast playlist/history migration deliberately ignores BPM Studio .GRP/.PLG archive-group
        // containers. Those files can be huge and often repeat the same tracks already referenced
        // by the actual .LST/.M3U/.PLS lists. Importing them made a large BPM archive take hours.
        var allCandidates = EnumerateCandidateFiles(sourcePath).ToArray();
        var files = allCandidates.Where(IsFastImportListFile).ToArray();
        var archiveGroupsSkipped = allCandidates.Length - files.Length;

        // BPM Studio can leave several physical snapshots/copies of the same dated daily play list.
        // Keep only the most complete copy for each explicit calendar date. This makes a BPM day
        // appear once in Hazz instead of 20-30 near-identical lists. The largest file wins; ties
        // prefer the newest modified copy. No track files are probed here.
        var chosenDailyHistory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in files)
        {
            if (!IsHistoryList(candidate) || !TryGetHistoryDateFromName(candidate, out var day)) continue;
            var key = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            if (!chosenDailyHistory.TryGetValue(key, out var existing) || IsBetterDailyHistoryCopy(candidate, existing))
                chosenDailyHistory[key] = candidate;
        }
        var datedHistoryCopiesSkipped = 0;
        if (chosenDailyHistory.Count > 0)
        {
            files = files.Where(candidate =>
            {
                if (!IsHistoryList(candidate) || !TryGetHistoryDateFromName(candidate, out var day)) return true;
                var key = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                var keep = chosenDailyHistory.TryGetValue(key, out var selected)
                    && string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(selected), StringComparison.OrdinalIgnoreCase);
                if (!keep) datedHistoryCopiesSkipped++;
                return keep;
            }).ToArray();
        }

        var warnings = new List<string>();
        int playlists = 0, historyLists = 0, unsupported = 0;
        long playlistItems = 0, historyItems = 0;
        long itemsProcessed = 0;
        var logicalSignatures = new HashSet<string>(StringComparer.Ordinal);
        var duplicateListsSkipped = 0;

        progress?.Report(new BpmStudioImportProgress(
            files.Length == 0 ? "No supported BPM Studio playlist/history files found." : "Starting fast import…",
            0, files.Length, string.Empty, 0, 0, 0));

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // One cleanup for the selected BPM root replaces thousands of per-source DELETE commands.
        await CleanupExistingImportRootAsync(connection, tx, sourcePath, cancellationToken).ConfigureAwait(false);

        // Build a temporary set of unique referenced music files. We do NOT call File.Exists or
        // FileInfo for every playlist item. That was the main cause of multi-hour imports when old
        // drives or network paths were referenced repeatedly.
        await using (var temp = connection.CreateCommand())
        {
            temp.Transaction = (SqliteTransaction)tx;
            temp.CommandText = """
CREATE TEMP TABLE IF NOT EXISTS temp_bpm_tracks(
    file_path TEXT PRIMARY KEY COLLATE NOCASE,
    artist TEXT NOT NULL,
    title TEXT NOT NULL,
    format TEXT NOT NULL
);
DELETE FROM temp_bpm_tracks;
""";
            await temp.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var tempTrackCmd = connection.CreateCommand();
        tempTrackCmd.Transaction = (SqliteTransaction)tx;
        tempTrackCmd.CommandText = """
INSERT OR IGNORE INTO temp_bpm_tracks(file_path,artist,title,format)
VALUES($path,$artist,$title,$format);
""";
        var ttPath = tempTrackCmd.Parameters.Add("$path", SqliteType.Text);
        var ttArtist = tempTrackCmd.Parameters.Add("$artist", SqliteType.Text);
        var ttTitle = tempTrackCmd.Parameters.Add("$title", SqliteType.Text);
        var ttFormat = tempTrackCmd.Parameters.Add("$format", SqliteType.Text);
        tempTrackCmd.Prepare();

        await using var playlistItemCmd = connection.CreateCommand();
        playlistItemCmd.Transaction = (SqliteTransaction)tx;
        playlistItemCmd.CommandText = """
INSERT INTO music_playlist_items(playlist_id,position,song_id,file_path,artist,title)
VALUES($p,$pos,NULL,$path,$artist,$title);
""";
        var piPlaylist = playlistItemCmd.Parameters.Add("$p", SqliteType.Integer);
        var piPosition = playlistItemCmd.Parameters.Add("$pos", SqliteType.Integer);
        var piPath = playlistItemCmd.Parameters.Add("$path", SqliteType.Text);
        var piArtist = playlistItemCmd.Parameters.Add("$artist", SqliteType.Text);
        var piTitle = playlistItemCmd.Parameters.Add("$title", SqliteType.Text);
        playlistItemCmd.Prepare();

        await using var historyItemCmd = connection.CreateCommand();
        historyItemCmd.Transaction = (SqliteTransaction)tx;
        historyItemCmd.CommandText = """
INSERT INTO music_history(source_list,position,song_id,file_path,artist,title,played_at_utc,imported_from)
VALUES($list,$pos,NULL,$path,$artist,$title,$played,$source);
""";
        var hiList = historyItemCmd.Parameters.Add("$list", SqliteType.Text);
        var hiPosition = historyItemCmd.Parameters.Add("$pos", SqliteType.Integer);
        var hiPath = historyItemCmd.Parameters.Add("$path", SqliteType.Text);
        var hiArtist = historyItemCmd.Parameters.Add("$artist", SqliteType.Text);
        var hiTitle = historyItemCmd.Parameters.Add("$title", SqliteType.Text);
        var hiPlayed = historyItemCmd.Parameters.Add("$played", SqliteType.Text);
        var hiSource = historyItemCmd.Parameters.Add("$source", SqliteType.Text);
        historyItemCmd.Prepare();

        for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[fileIndex];
            progress?.Report(new BpmStudioImportProgress(
                "Reading playlist/history file…", fileIndex, files.Length,
                Path.GetFileName(file), itemsProcessed, 0, unsupported));

            IReadOnlyList<string> paths;
            try { paths = ExtractTrackPaths(file); }
            catch (Exception ex)
            {
                unsupported++;
                if (warnings.Count < 100) warnings.Add($"{file}: {ex.Message}");
                progress?.Report(new BpmStudioImportProgress(
                    "Skipped unreadable list file", fileIndex + 1, files.Length,
                    Path.GetFileName(file), itemsProcessed, 0, unsupported));
                continue;
            }

            if (paths.Count == 0)
            {
                unsupported++;
                if (warnings.Count < 100) warnings.Add($"No track paths could be recovered from {Path.GetFileName(file)}.");
                progress?.Report(new BpmStudioImportProgress(
                    "No media paths found in list", fileIndex + 1, files.Length,
                    Path.GetFileName(file), itemsProcessed, 0, unsupported));
                continue;
            }

            var history = IsHistoryList(file);
            var playedAt = history ? GuessHistoryDate(file) : null;
            var listName = history ? BuildHistoryListName(file, playedAt) : BuildListName(file);

            var signature = BuildLogicalListSignature(history, listName, playedAt, paths);
            if (!logicalSignatures.Add(signature))
            {
                duplicateListsSkipped++;
                progress?.Report(new BpmStudioImportProgress(
                    "Duplicate BPM list ignored", fileIndex + 1, files.Length,
                    Path.GetFileName(file), itemsProcessed, 0, unsupported));
                continue;
            }

            // Daily BPM history is saved twice by design: as dated play-history rows AND as a
            // loadable ordered list in Music Lists. That mirrors BPM Studio's "list" view and lets
            // the host reload an entire day's music directly to either deck.
            var playlistSourceType = history ? "BPM Daily History" : "BPM Studio";
            var playlistId = await ReplacePlaylistAsync(connection, tx, listName, file, playlistSourceType, cancellationToken).ConfigureAwait(false);
            if (history) historyLists++; else playlists++;

            var position = 0;
            foreach (var raw in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var resolved = ResolveTrackPath(file, raw);
                if (string.IsNullOrWhiteSpace(resolved)) continue;

                position++;
                itemsProcessed++;
                var parsed = LibraryImportService.ParseName(resolved);
                var format = Path.GetExtension(resolved).TrimStart('.').ToUpperInvariant();

                ttPath.Value = resolved;
                ttArtist.Value = parsed.Artist;
                ttTitle.Value = parsed.Title;
                ttFormat.Value = format;
                await tempTrackCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                if (history)
                {
                    hiList.Value = listName;
                    hiPosition.Value = position;
                    hiPath.Value = resolved;
                    hiArtist.Value = parsed.Artist;
                    hiTitle.Value = parsed.Title;
                    // BPM daily lists normally provide the calendar day and play order, not a
                    // reliable clock time for each track. Keep the shared day timestamp and preserve
                    // exact list order separately in Position rather than inventing play times.
                    hiPlayed.Value = playedAt?.ToString("O") ?? (object)DBNull.Value;
                    hiSource.Value = file;
                    await historyItemCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    historyItems++;

                    piPlaylist.Value = playlistId;
                    piPosition.Value = position;
                    piPath.Value = resolved;
                    piArtist.Value = parsed.Artist;
                    piTitle.Value = parsed.Title;
                    await playlistItemCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    piPlaylist.Value = playlistId;
                    piPosition.Value = position;
                    piPath.Value = resolved;
                    piArtist.Value = parsed.Artist;
                    piTitle.Value = parsed.Title;
                    await playlistItemCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    playlistItems++;
                }

                if ((position % 500) == 0)
                {
                    var percent = files.Length <= 0
                        ? 0
                        : ((fileIndex + (position / (double)Math.Max(1, paths.Count))) / files.Length) * 100.0;
                    progress?.Report(new BpmStudioImportProgress(
                        history ? "Fast-importing history…" : "Fast-importing playlist…",
                        fileIndex, files.Length, Path.GetFileName(file),
                        itemsProcessed, 0, unsupported, percent));
                }
            }

            progress?.Report(new BpmStudioImportProgress(
                history ? "History list imported" : "Playlist imported",
                fileIndex + 1, files.Length, Path.GetFileName(file),
                itemsProcessed, 0, unsupported));
        }

        progress?.Report(new BpmStudioImportProgress(
            "Indexing unique referenced music tracks…", files.Length, files.Length,
            string.Empty, itemsProcessed, 0, unsupported, 98));

        // One database-native bulk insert indexes each unique referenced path once. It performs no
        // HDD/network probe. Existing Hazz library metadata is left untouched.
        await using (var bulkSongs = connection.CreateCommand())
        {
            bulkSongs.Transaction = (SqliteTransaction)tx;
            bulkSongs.CommandText = """
INSERT INTO songs(artist,title,manufacturer,disc_id,file_path,format,file_size,date_added,cdg_sync_seconds,preferred_key,last_seen_utc,media_kind)
SELECT artist,title,'','',file_path,format,0,NULL,0,0,CURRENT_TIMESTAMP,'Music'
FROM temp_bpm_tracks
WHERE 1
ON CONFLICT(file_path) DO NOTHING;
""";
            await bulkSongs.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        long uniqueReferencedTracks;
        await using (var countTracks = connection.CreateCommand())
        {
            countTracks.Transaction = (SqliteTransaction)tx;
            countTracks.CommandText = "SELECT COUNT(*) FROM temp_bpm_tracks";
            uniqueReferencedTracks = Convert.ToInt64(await countTracks.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        }

        progress?.Report(new BpmStudioImportProgress(
            "Saving imported BPM Studio data…", files.Length, files.Length,
            string.Empty, itemsProcessed, uniqueReferencedTracks, unsupported, 99));

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        progress?.Report(new BpmStudioImportProgress(
            "Import complete", files.Length, files.Length,
            string.Empty, itemsProcessed, uniqueReferencedTracks, unsupported, 100));

        if (archiveGroupsSkipped > 0)
            warnings.Add($"Fast import skipped {archiveGroupsSkipped:N0} BPM .GRP/.PLG archive-group file(s). Playlist/history .LST/.M3U/.PLS files were imported; use Hazz's normal Music Library scan if you also want to index every archive-group track.");
        if (datedHistoryCopiesSkipped > 0)
            warnings.Add($"Collapsed {datedHistoryCopiesSkipped:N0} extra physical BPM daily-history copy file(s) by calendar date. The most complete copy for each day was retained.");
        if (duplicateListsSkipped > 0)
            warnings.Add($"Collapsed {duplicateListsSkipped:N0} duplicate BPM Studio playlist/history copy file(s). Only one logical copy was retained.");
        warnings.Add("BPM dated played-song lists are imported as both Music History rows and loadable 'BPM Daily History' lists, preserving their play order.");
        warnings.Add("Fast BPM import does not probe every referenced music file on disk. Missing/moved files remain visible in imported lists and can be checked later by a library rescan.");

        return new BpmStudioImportResult(playlists, historyLists, playlistItems, historyItems, uniqueReferencedTracks, unsupported, warnings);
    }

    private static bool IsFastImportListFile(string file)
    {
        var ext = Path.GetExtension(file);
        return ext.Equals(".lst", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".m3u", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".m3u8", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".pls", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CleanupExistingImportRootAsync(
        SqliteConnection c,
        System.Data.Common.DbTransaction tx,
        string sourcePath,
        CancellationToken token)
    {
        var isDirectory = Directory.Exists(sourcePath);
        var rootPrefix = isDirectory
            ? Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar
            : Path.GetFullPath(sourcePath);

        string MatchClause(string column)
            => isDirectory
                ? $"({column}=$exact OR substr({column},1,length($prefix))=$prefix)"
                : $"{column}=$exact";

        await using (var hist = c.CreateCommand())
        {
            hist.Transaction = (SqliteTransaction)tx;
            hist.CommandText = $"DELETE FROM music_history WHERE imported_from IS NOT NULL AND {MatchClause("imported_from")}";
            hist.Parameters.AddWithValue("$exact", sourcePath);
            hist.Parameters.AddWithValue("$prefix", rootPrefix);
            await hist.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        await using (var items = c.CreateCommand())
        {
            items.Transaction = (SqliteTransaction)tx;
            items.CommandText = $"""
DELETE FROM music_playlist_items
WHERE playlist_id IN (
    SELECT id FROM music_playlists
    WHERE source_type IN ('BPM Studio','BPM Daily History')
      AND source_path IS NOT NULL
      AND {MatchClause("source_path")}
);
""";
            items.Parameters.AddWithValue("$exact", sourcePath);
            items.Parameters.AddWithValue("$prefix", rootPrefix);
            await items.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        await using (var lists = c.CreateCommand())
        {
            lists.Transaction = (SqliteTransaction)tx;
            lists.CommandText = $"""
DELETE FROM music_playlists
WHERE source_type IN ('BPM Studio','BPM Daily History')
  AND source_path IS NOT NULL
  AND {MatchClause("source_path")};
""";
            lists.Parameters.AddWithValue("$exact", sourcePath);
            lists.Parameters.AddWithValue("$prefix", rootPrefix);
            await lists.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
    }

    private static BpmStudioImportPreview Preview(string sourcePath, CancellationToken token)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        if (!Directory.Exists(sourcePath) && !File.Exists(sourcePath))
            throw new FileNotFoundException("The selected BPM Studio data folder/file does not exist.", sourcePath);
        var allFiles = EnumerateCandidateFiles(sourcePath).ToArray();
        var fastFiles = allFiles.Where(IsFastImportListFile).ToArray();
        var historyCandidates = fastFiles.Where(IsHistoryList).ToArray();
        var datedHistoryDays = historyCandidates
            .Select(x => TryGetHistoryDateFromName(x, out var day) ? day.ToString("yyyy-MM-dd") : null)
            .Where(x => x is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var undatedHistory = historyCandidates.Count(x => !TryGetHistoryDateFromName(x, out _));
        var history = datedHistoryDays + undatedHistory;
        var groups = allFiles.Count(x => Path.GetExtension(x).Equals(".grp", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(x).Equals(".plg", StringComparison.OrdinalIgnoreCase));
        var playlists = fastFiles.Count(x => !IsHistoryList(x));
        return new BpmStudioImportPreview(sourcePath, playlists, history, groups, fastFiles.Take(12).Select(Path.GetFileName).ToArray()!);
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            if (SupportedListExtensions.Contains(Path.GetExtension(sourcePath))) yield return sourcePath;
            yield break;
        }
        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
            if (SupportedListExtensions.Contains(Path.GetExtension(file))) yield return file;
    }

    private static IReadOnlyList<string> ExtractTrackPaths(string file)
    {
        var ext = Path.GetExtension(file);
        var bytes = File.ReadAllBytes(file);
        var text = DecodeBestEffort(bytes);
        var result = new List<string>();

        void Add(string candidate)
        {
            candidate = candidate.Trim().Trim('"', '\'', '\0');
            if (candidate.Length == 0) return;
            var extension = Path.GetExtension(candidate);
            if (!MediaExtensions.Contains(extension)) return;
            result.Add(candidate);
        }

        if (ext.Equals(".pls", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in SplitLines(text))
            {
                var equals = line.IndexOf('=');
                if (equals > 0 && line[..equals].Trim().StartsWith("File", StringComparison.OrdinalIgnoreCase)) Add(line[(equals + 1)..]);
            }
        }
        else if (ext.Equals(".m3u", StringComparison.OrdinalIgnoreCase) || ext.Equals(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in SplitLines(text)) if (!line.TrimStart().StartsWith('#')) Add(line);
        }
        else
        {
            // Native BPM Studio LST files can contain binary-ish metadata around absolute paths.
            // Use one extraction strategy only so a path found by the regex is not added a second
            // time by the line fallback. Repeated occurrences inside the actual list are retained: a
            // daily history must show a song twice if it really was played twice that day.
            var matches = AbsoluteMediaPath.Matches(text);
            if (matches.Count > 0)
            {
                foreach (Match match in matches) Add(match.Groups["path"].Value);
            }
            else
            {
                foreach (var line in SplitLines(text))
                {
                    var value = line.Trim();
                    if (value.StartsWith("File", StringComparison.OrdinalIgnoreCase) && value.Contains('='))
                        value = value[(value.IndexOf('=') + 1)..].Trim();
                    if (MediaExtensions.Contains(Path.GetExtension(value))) Add(value);
                }
            }
        }
        return result;
    }

    private static IEnumerable<string> SplitLines(string text)
        => text.Replace('\0', '\n').Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string DecodeBestEffort(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(bytes);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8.GetString(bytes);

        // Older/native BPM Studio lists can be UTF-16 without a BOM. Detect the common
        // alternating-NUL pattern before falling back to strict UTF-8 / Latin-1.
        var sample = Math.Min(bytes.Length, 4096);
        if (sample >= 8)
        {
            var oddZeros = 0;
            var evenZeros = 0;
            for (var i = 0; i < sample; i++)
            {
                if (bytes[i] != 0) continue;
                if ((i & 1) == 0) evenZeros++; else oddZeros++;
            }
            if (oddZeros > sample / 6) return Encoding.Unicode.GetString(bytes);
            if (evenZeros > sample / 6) return Encoding.BigEndianUnicode.GetString(bytes);
        }

        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(bytes); }
    }

    private static bool IsHistoryList(string file)
    {
        var pieces = file.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Any(x =>
            x.Contains("history", StringComparison.OrdinalIgnoreCase)
            || x.Contains("historie", StringComparison.OrdinalIgnoreCase)
            || x.Contains("played", StringComparison.OrdinalIgnoreCase)
            || x.Contains("playlog", StringComparison.OrdinalIgnoreCase)
            || x.Contains("play log", StringComparison.OrdinalIgnoreCase)))
            return true;

        // BPM Studio's automatic daily played-song lists are commonly named with the calendar
        // date but live in the ordinary Lists area rather than a folder literally called History.
        // A strong date token in the filename is therefore treated as daily music history.
        return TryGetHistoryDateFromName(file, out _);
    }

    private static string BuildListName(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file).Trim();
        return string.IsNullOrWhiteSpace(name) ? "BPM Studio Playlist" : name;
    }

    private static string BuildHistoryListName(string file, DateTimeOffset? playedAt)
    {
        if (TryGetHistoryDateFromName(file, out var explicitDate))
            return $"BPM Daily History — {explicitDate:yyyy-MM-dd}";
        if (playedAt is DateTimeOffset inferred)
            return $"BPM History — {inferred.ToLocalTime():yyyy-MM-dd} — {BuildListName(file)}";
        return $"BPM History — {BuildListName(file)}";
    }

    private static DateTimeOffset? GuessHistoryDate(string file)
    {
        if (TryGetHistoryDateFromName(file, out var explicitDate))
            return explicitDate;
        try { return new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero); }
        catch { return null; }
    }

    private static bool TryGetHistoryDateFromName(string file, out DateTimeOffset date)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var candidates = new[]
        {
            (Pattern: @"\d{4}[.\-_]\d{1,2}[.\-_]\d{1,2}", Formats: new[] { "yyyy.MM.dd", "yyyy-MM-dd", "yyyy_MM_dd" }),
            (Pattern: @"\d{1,2}[.\-_]\d{1,2}[.\-_]\d{4}", Formats: new[] { "dd.MM.yyyy", "dd-MM-yyyy", "dd_MM_yyyy" }),
            (Pattern: @"(?<!\d)\d{8}(?!\d)", Formats: new[] { "yyyyMMdd", "ddMMyyyy" })
        };

        foreach (var candidate in candidates)
        {
            var match = Regex.Match(name, candidate.Pattern);
            if (!match.Success) continue;
            foreach (var format in candidate.Formats)
            {
                if (!DateTime.TryParseExact(match.Value, format, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt)) continue;
                // Keep the calendar date in local time. Converting midnight to UTC here can move
                // a UK summer date back to the previous UTC day, which would label the daily list
                // with the wrong date. SQLite can still normalize this offset-aware value later.
                date = new DateTimeOffset(dt.Date, TimeZoneInfo.Local.GetUtcOffset(dt.Date));
                return true;
            }
        }
        date = default;
        return false;
    }

    private static bool IsBetterDailyHistoryCopy(string candidate, string existing)
    {
        try
        {
            var c = new FileInfo(candidate);
            var e = new FileInfo(existing);
            if (c.Length != e.Length) return c.Length > e.Length;
            return c.LastWriteTimeUtc > e.LastWriteTimeUtc;
        }
        catch
        {
            return string.Compare(candidate, existing, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }

    private static string ResolveTrackPath(string playlistFile, string raw)
    {
        raw = raw.Trim().Trim('"');
        if (raw.Length == 0) return string.Empty;
        try
        {
            if (Path.IsPathRooted(raw)) return Path.GetFullPath(raw);
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(playlistFile) ?? string.Empty, raw));
        }
        catch { return raw; }
    }

    private static async Task CleanupExistingCandidateSourcesAsync(
        SqliteConnection c, System.Data.Common.DbTransaction tx, IReadOnlyList<string> files, CancellationToken token)
    {
        foreach (var source in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            await using (var hist = c.CreateCommand())
            {
                hist.Transaction = (SqliteTransaction)tx;
                hist.CommandText = "DELETE FROM music_history WHERE imported_from=$source";
                hist.Parameters.AddWithValue("$source", source);
                await hist.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await using (var items = c.CreateCommand())
            {
                items.Transaction = (SqliteTransaction)tx;
                items.CommandText = "DELETE FROM music_playlist_items WHERE playlist_id IN (SELECT id FROM music_playlists WHERE source_type IN ('BPM Studio','BPM Daily History') AND source_path=$source)";
                items.Parameters.AddWithValue("$source", source);
                await items.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await using (var list = c.CreateCommand())
            {
                list.Transaction = (SqliteTransaction)tx;
                list.CommandText = "DELETE FROM music_playlists WHERE source_type IN ('BPM Studio','BPM Daily History') AND source_path=$source";
                list.Parameters.AddWithValue("$source", source);
                await list.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }
    }

    private static string BuildLogicalListSignature(bool history, string listName, DateTimeOffset? playedAt, IReadOnlyList<string> paths)
    {
        var logicalName = NormalizeLogicalListName(listName);
        var date = history ? playedAt?.ToLocalTime().ToString("yyyy-MM-dd") ?? string.Empty : string.Empty;
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        void Feed(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value.ToUpperInvariant());
            hash.AppendData(bytes);
            hash.AppendData(new byte[] { 0 });
        }
        Feed(history ? "H" : "P");
        Feed(date);
        // For a dated BPM history, the date + ordered tracks define the logical list. BPM can
        // keep many physical copies under slightly different filenames, so do not let the copy
        // filename defeat deduplication. Undated lists still use the normalized logical name.
        Feed(history && date.Length > 0 ? "DATED-HISTORY" : logicalName);
        foreach (var path in paths) Feed(path.Trim().Replace('/', '\\'));
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string NormalizeLogicalListName(string name)
    {
        name = name.Trim();
        // Common backup/copy suffixes produced by archive tools should not create 30 visible copies.
        name = Regex.Replace(name, @"\s*\((?:copy\s*)?\d+\)\s*$", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"\s*[-_]\s*copy(?:\s*\d+)?\s*$", "", RegexOptions.IgnoreCase);
        return name.Trim();
    }

    private static async Task<long> ReplacePlaylistAsync(SqliteConnection c, System.Data.Common.DbTransaction tx, string name, string sourcePath, string sourceType, CancellationToken token)
    {
        // BPM Studio frequently stores multiple physical copies of the same logical playlist.
        // Hazz exposes one playlist per logical name and refreshes it on re-import.
        await using var find = c.CreateCommand();
        find.Transaction = (SqliteTransaction)tx;
        find.CommandText = "SELECT id FROM music_playlists WHERE source_type=$type AND name=$name COLLATE NOCASE ORDER BY id LIMIT 1";
        find.Parameters.AddWithValue("$name", name);
        find.Parameters.AddWithValue("$type", sourceType);
        var existing = await find.ExecuteScalarAsync(token);
        if (existing is not null && existing is not DBNull)
        {
            var id = Convert.ToInt64(existing);
            await using (var dupItems = c.CreateCommand())
            {
                dupItems.Transaction = (SqliteTransaction)tx;
                dupItems.CommandText = "DELETE FROM music_playlist_items WHERE playlist_id IN (SELECT id FROM music_playlists WHERE source_type=$type AND name=$name COLLATE NOCASE AND id<>$id)";
                dupItems.Parameters.AddWithValue("$name", name); dupItems.Parameters.AddWithValue("$type", sourceType); dupItems.Parameters.AddWithValue("$id", id);
                await dupItems.ExecuteNonQueryAsync(token);
            }
            await using (var dups = c.CreateCommand())
            {
                dups.Transaction = (SqliteTransaction)tx;
                dups.CommandText = "DELETE FROM music_playlists WHERE source_type=$type AND name=$name COLLATE NOCASE AND id<>$id";
                dups.Parameters.AddWithValue("$name", name); dups.Parameters.AddWithValue("$type", sourceType); dups.Parameters.AddWithValue("$id", id);
                await dups.ExecuteNonQueryAsync(token);
            }
            await using var del = c.CreateCommand(); del.Transaction = (SqliteTransaction)tx;
            del.CommandText = "DELETE FROM music_playlist_items WHERE playlist_id=$id"; del.Parameters.AddWithValue("$id", id);
            await del.ExecuteNonQueryAsync(token);
            await using var upd = c.CreateCommand(); upd.Transaction = (SqliteTransaction)tx;
            upd.CommandText = "UPDATE music_playlists SET name=$name,source_type=$type,source_path=$path,imported_utc=CURRENT_TIMESTAMP WHERE id=$id";
            upd.Parameters.AddWithValue("$name", name); upd.Parameters.AddWithValue("$type", sourceType); upd.Parameters.AddWithValue("$path", sourcePath); upd.Parameters.AddWithValue("$id", id);
            await upd.ExecuteNonQueryAsync(token);
            return id;
        }
        await using var ins = c.CreateCommand(); ins.Transaction = (SqliteTransaction)tx;
        ins.CommandText = "INSERT INTO music_playlists(name,source_type,source_path) VALUES($name,$type,$path); SELECT last_insert_rowid();";
        ins.Parameters.AddWithValue("$name", name); ins.Parameters.AddWithValue("$type", sourceType); ins.Parameters.AddWithValue("$path", sourcePath);
        return Convert.ToInt64(await ins.ExecuteScalarAsync(token));
    }

    private static async Task<long?> FindSongIdAsync(SqliteConnection c, System.Data.Common.DbTransaction tx, string path, CancellationToken token)
    {
        await using var cmd = c.CreateCommand(); cmd.Transaction = (SqliteTransaction)tx;
        cmd.CommandText = "SELECT id FROM songs WHERE file_path=$path LIMIT 1"; cmd.Parameters.AddWithValue("$path", path);
        var value = await cmd.ExecuteScalarAsync(token); return value is null || value is DBNull ? null : Convert.ToInt64(value);
    }

    private static async Task<long> UpsertMusicSongAsync(SqliteConnection c, System.Data.Common.DbTransaction tx, string path, string artist, string title, CancellationToken token)
    {
        var info = new FileInfo(path);
        await using var cmd = c.CreateCommand(); cmd.Transaction = (SqliteTransaction)tx;
        cmd.CommandText = """
INSERT INTO songs(artist,title,manufacturer,disc_id,file_path,format,file_size,date_added,cdg_sync_seconds,preferred_key,last_seen_utc,media_kind)
VALUES($artist,$title,'','',$path,$format,$size,$added,0,0,CURRENT_TIMESTAMP,'Music')
ON CONFLICT(file_path) DO UPDATE SET artist=excluded.artist,title=excluded.title,format=excluded.format,file_size=excluded.file_size,media_kind='Music',last_seen_utc=CURRENT_TIMESTAMP
RETURNING id;
""";
        cmd.Parameters.AddWithValue("$artist", artist); cmd.Parameters.AddWithValue("$title", title); cmd.Parameters.AddWithValue("$path", path);
        cmd.Parameters.AddWithValue("$format", Path.GetExtension(path).TrimStart('.').ToUpperInvariant()); cmd.Parameters.AddWithValue("$size", info.Length);
        cmd.Parameters.AddWithValue("$added", info.CreationTimeUtc == DateTime.MinValue ? DBNull.Value : info.CreationTimeUtc.ToString("O"));
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(token));
    }

    private static async Task InsertPlaylistItemAsync(SqliteConnection c, System.Data.Common.DbTransaction tx, long playlistId, int position, long? songId, string path, string artist, string title, CancellationToken token)
    {
        await using var cmd = c.CreateCommand(); cmd.Transaction = (SqliteTransaction)tx;
        cmd.CommandText = "INSERT INTO music_playlist_items(playlist_id,position,song_id,file_path,artist,title) VALUES($p,$pos,$song,$path,$artist,$title)";
        cmd.Parameters.AddWithValue("$p", playlistId); cmd.Parameters.AddWithValue("$pos", position); cmd.Parameters.AddWithValue("$song", songId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$path", path); cmd.Parameters.AddWithValue("$artist", artist); cmd.Parameters.AddWithValue("$title", title);
        await cmd.ExecuteNonQueryAsync(token);
    }

    private static async Task DeleteHistoryFromSourceAsync(SqliteConnection c, System.Data.Common.DbTransaction tx, string source, CancellationToken token)
    {
        await using var cmd = c.CreateCommand(); cmd.Transaction = (SqliteTransaction)tx;
        cmd.CommandText = "DELETE FROM music_history WHERE imported_from=$source";
        cmd.Parameters.AddWithValue("$source", source);
        await cmd.ExecuteNonQueryAsync(token);
    }

    private static async Task InsertHistoryAsync(SqliteConnection c, System.Data.Common.DbTransaction tx, string list, int position, long? songId, string path, string artist, string title, DateTimeOffset? played, string source, CancellationToken token)
    {
        await using var cmd = c.CreateCommand(); cmd.Transaction = (SqliteTransaction)tx;
        cmd.CommandText = "INSERT INTO music_history(source_list,position,song_id,file_path,artist,title,played_at_utc,imported_from) VALUES($list,$pos,$song,$path,$artist,$title,$played,$source)";
        cmd.Parameters.AddWithValue("$list", list); cmd.Parameters.AddWithValue("$pos", position); cmd.Parameters.AddWithValue("$song", songId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$path", path); cmd.Parameters.AddWithValue("$artist", artist); cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$played", played?.ToString("O") ?? (object)DBNull.Value); cmd.Parameters.AddWithValue("$source", source);
        await cmd.ExecuteNonQueryAsync(token);
    }
}
