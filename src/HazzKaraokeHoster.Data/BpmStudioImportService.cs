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

        var allCandidates = EnumerateCandidateFiles(sourcePath).ToArray();
        var files = allCandidates.Where(IsFastImportListFile).ToArray();
        var allArchiveGroupFiles = allCandidates.Where(IsArchiveGroupFile)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var archiveGroupFiles = allArchiveGroupFiles
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(SafeLastWriteTimeUtc)
                .ThenByDescending(SafeFileLength)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .First())
            .ToArray();
        var duplicateArchiveGroupCopiesSkipped = allArchiveGroupFiles.Length - archiveGroupFiles.Length;

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
        int playlists = 0, historyLists = 0, unsupported = 0, virtualFoldersImported = 0;
        long playlistItems = 0, historyItems = 0;
        long virtualFolderTrackLinksImported = 0;
        long itemsProcessed = 0;
        var logicalSignatures = new HashSet<string>(StringComparer.Ordinal);
        var duplicateListsSkipped = 0;

        progress?.Report(new BpmStudioImportProgress(
            files.Length == 0 ? "No supported BPM Studio playlist/history files found." : "Starting fast import…",
            0, files.Length, string.Empty, 0, 0, 0));

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (var importPragmas = connection.CreateCommand())
        {
            // Do not make the final commit checkpoint a very large WAL file on the worker thread.
            // A bounded passive checkpoint is run after the transaction has committed.
            importPragmas.CommandText = "PRAGMA wal_autocheckpoint=0;";
            await importPragmas.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
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
CREATE TEMP TABLE IF NOT EXISTS temp_bpm_virtual_links(
    folder_path TEXT NOT NULL COLLATE NOCASE,
    file_path TEXT NOT NULL COLLATE NOCASE,
    PRIMARY KEY(folder_path,file_path)
);
DELETE FROM temp_bpm_virtual_links;
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

        await using var virtualLinkCmd = connection.CreateCommand();
        virtualLinkCmd.Transaction = (SqliteTransaction)tx;
        virtualLinkCmd.CommandText = "INSERT OR IGNORE INTO temp_bpm_virtual_links(folder_path,file_path) VALUES($folder,$path);";
        var vlFolder = virtualLinkCmd.Parameters.Add("$folder", SqliteType.Text);
        var vlPath = virtualLinkCmd.Parameters.Add("$path", SqliteType.Text);
        virtualLinkCmd.Prepare();

        var processedBpmGroups = new Dictionary<string, (long Size, string LastWriteUtc, string FolderPath, long TrackCount)>(StringComparer.OrdinalIgnoreCase);
        await using (var cachedGroups = connection.CreateCommand())
        {
            cachedGroups.Transaction = (SqliteTransaction)tx;
            cachedGroups.CommandText = "SELECT source_path,file_size,last_write_utc,folder_path,track_count FROM bpm_virtual_folder_sources;";
            await using var cachedReader = await cachedGroups.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await cachedReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                processedBpmGroups[cachedReader.GetString(0)] = (cachedReader.GetInt64(1), cachedReader.GetString(2), cachedReader.GetString(3), cachedReader.GetInt64(4));
        }

        await using var cacheGroupCmd = connection.CreateCommand();
        cacheGroupCmd.Transaction = (SqliteTransaction)tx;
        cacheGroupCmd.CommandText = """
INSERT INTO bpm_virtual_folder_sources(source_path,file_size,last_write_utc,folder_path,track_count,imported_utc)
VALUES($source,$size,$write,$folder,$tracks,CURRENT_TIMESTAMP)
ON CONFLICT(source_path) DO UPDATE SET file_size=excluded.file_size,last_write_utc=excluded.last_write_utc,
folder_path=excluded.folder_path,track_count=excluded.track_count,imported_utc=CURRENT_TIMESTAMP;
""";
        cacheGroupCmd.Parameters.Add("$source", SqliteType.Text);
        cacheGroupCmd.Parameters.Add("$size", SqliteType.Integer);
        cacheGroupCmd.Parameters.Add("$write", SqliteType.Text);
        cacheGroupCmd.Parameters.Add("$folder", SqliteType.Text);
        cacheGroupCmd.Parameters.Add("$tracks", SqliteType.Integer);
        cacheGroupCmd.Prepare();

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

        // BPM Studio stores file-archive groups in .GRP files and playlist groups in .PLG files.
        // Read only their recoverable media paths. This avoids disk probing while retaining the
        // BPM organisation as Hazz virtual folders.
        for (var groupIndex = 0; groupIndex < archiveGroupFiles.Length; groupIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var groupFile = archiveGroupFiles[groupIndex];
            FileInfo groupInfo;
            try
            {
                groupInfo = new FileInfo(groupFile);
                if (!groupInfo.Exists) continue;
            }
            catch (Exception ex)
            {
                unsupported++;
                if (warnings.Count < 100) warnings.Add($"{groupFile}: {ex.Message}");
                continue;
            }
            var groupStamp = groupInfo.LastWriteTimeUtc.ToString("O");
            var expectedFolderPath = BuildVirtualFolderPath(sourcePath, groupFile);
            if (processedBpmGroups.TryGetValue(groupFile, out var cached)
                && cached.Size == groupInfo.Length
                && string.Equals(cached.LastWriteUtc, groupStamp, StringComparison.Ordinal)
                && string.Equals(cached.FolderPath, expectedFolderPath, StringComparison.OrdinalIgnoreCase)
                && cached.TrackCount > 0)
            {
                progress?.Report(new BpmStudioImportProgress(
                    "Skipping unchanged BPM virtual folder…", files.Length, files.Length,
                    Path.GetFileName(groupFile), itemsProcessed, 0, unsupported,
                    90 + (archiveGroupFiles.Length == 0 ? 0 : groupIndex * 7.0 / archiveGroupFiles.Length)));
                continue;
            }
            IReadOnlyList<string> groupPaths;
            try { groupPaths = ExtractTrackPaths(groupFile); }
            catch (Exception ex)
            {
                unsupported++;
                if (warnings.Count < 100) warnings.Add($"{groupFile}: {ex.Message}");
                continue;
            }

            if (groupPaths.Count == 0)
            {
                unsupported++;
                if (warnings.Count < 100) warnings.Add($"No track paths could be recovered from BPM group {Path.GetFileName(groupFile)}.");
                continue;
            }

            var folderPath = expectedFolderPath;
            var importedGroupTracks = 0L;
            foreach (var raw in groupPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var resolved = ResolveTrackPath(groupFile, raw);
                if (string.IsNullOrWhiteSpace(resolved)) continue;
                var parsed = LibraryImportService.ParseName(resolved);
                ttPath.Value = resolved;
                ttArtist.Value = parsed.Artist;
                ttTitle.Value = parsed.Title;
                ttFormat.Value = Path.GetExtension(resolved).TrimStart('.').ToUpperInvariant();
                await tempTrackCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                vlFolder.Value = folderPath;
                vlPath.Value = resolved;
                await virtualLinkCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                importedGroupTracks++;
            }

            cacheGroupCmd.Parameters["$source"].Value = groupFile;
            cacheGroupCmd.Parameters["$size"].Value = groupInfo.Length;
            cacheGroupCmd.Parameters["$write"].Value = groupStamp;
            cacheGroupCmd.Parameters["$folder"].Value = folderPath;
            cacheGroupCmd.Parameters["$tracks"].Value = importedGroupTracks;
            await cacheGroupCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            progress?.Report(new BpmStudioImportProgress(
                "Reading BPM virtual folders…", files.Length, files.Length,
                Path.GetFileName(groupFile), itemsProcessed, 0, unsupported,
                90 + (archiveGroupFiles.Length == 0 ? 0 : groupIndex * 7.0 / archiveGroupFiles.Length)));
        }

        // The FTS trigger runs for every new song. A single INSERT can therefore look frozen for
        // a large BPM archive. Keep the transaction, but commit the work in bounded SQL batches so
        // the UI receives progress and cancellation remains responsive.
        long uniqueTrackCandidates;
        await using (var countCandidates = connection.CreateCommand())
        {
            countCandidates.Transaction = (SqliteTransaction)tx;
            countCandidates.CommandText = "SELECT COUNT(*) FROM temp_bpm_tracks";
            uniqueTrackCandidates = Convert.ToInt64(await countCandidates.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        }

        progress?.Report(new BpmStudioImportProgress(
            "Indexing unique referenced music tracks…", files.Length, files.Length,
            uniqueTrackCandidates == 0 ? string.Empty : $"0 / {uniqueTrackCandidates:N0}",
            itemsProcessed, 0, unsupported, 90));

        const int indexBatchSize = 1000;
        long indexedCandidates = 0;
        long lastRowId = 0;
        // A row-by-row songs_ai trigger turns a 100k+ import into a long apparent freeze.
        // Temporarily defer that trigger and write missing FTS rows in the same bounded batches.
        // DDL is transactional here: cancellation rolls back and leaves the original trigger.
        await using (var dropFtsTrigger = connection.CreateCommand())
        {
            dropFtsTrigger.Transaction = (SqliteTransaction)tx;
            dropFtsTrigger.CommandText = "DROP TRIGGER IF EXISTS songs_ai;";
            await dropFtsTrigger.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var bulkSongs = connection.CreateCommand())
        {
            bulkSongs.Transaction = (SqliteTransaction)tx;
            bulkSongs.CommandText = """
INSERT INTO songs(artist,title,manufacturer,disc_id,file_path,format,file_size,date_added,cdg_sync_seconds,preferred_key,last_seen_utc,media_kind)
SELECT artist,title,'','',file_path,format,0,NULL,0,0,CURRENT_TIMESTAMP,'Music'
FROM temp_bpm_tracks
WHERE rowid > $after
ORDER BY rowid
LIMIT $batch
ON CONFLICT(file_path) DO NOTHING;
""";
            bulkSongs.Parameters.Add("$after", SqliteType.Integer);
            bulkSongs.Parameters.Add("$batch", SqliteType.Integer);

            await using var bulkFts = connection.CreateCommand();
            bulkFts.Transaction = (SqliteTransaction)tx;
            bulkFts.CommandText = """
INSERT INTO songs_fts(rowid,artist,title,manufacturer,disc_id,file_path)
SELECT s.id,s.artist,s.title,s.manufacturer,s.disc_id,s.file_path
FROM songs s
JOIN temp_bpm_tracks t ON t.file_path=s.file_path COLLATE NOCASE
WHERE t.rowid > $after AND t.rowid <= $through
  AND NOT EXISTS (SELECT 1 FROM songs_fts f WHERE f.rowid=s.id);
""";
            bulkFts.Parameters.Add("$after", SqliteType.Integer);
            bulkFts.Parameters.Add("$through", SqliteType.Integer);

            while (indexedCandidates < uniqueTrackCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bulkSongs.Parameters["$after"].Value = lastRowId;
                bulkSongs.Parameters["$batch"].Value = indexBatchSize;
                var inserted = await bulkSongs.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                // Advance by the candidate rows, not inserted rows: existing library songs are
                // intentionally skipped but must not cause the loop to repeat forever.
                await using var last = connection.CreateCommand();
                last.Transaction = (SqliteTransaction)tx;
                last.CommandText = "SELECT COALESCE(MAX(rowid),$after) FROM (SELECT rowid FROM temp_bpm_tracks WHERE rowid > $after ORDER BY rowid LIMIT $batch);";
                last.Parameters.AddWithValue("$after", lastRowId);
                last.Parameters.AddWithValue("$batch", indexBatchSize);
                var nextRowId = Convert.ToInt64(await last.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
                if (nextRowId <= lastRowId) break;

                bulkFts.Parameters["$after"].Value = lastRowId;
                bulkFts.Parameters["$through"].Value = nextRowId;
                await bulkFts.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                lastRowId = nextRowId;
                indexedCandidates = Math.Min(uniqueTrackCandidates, indexedCandidates + indexBatchSize);
                var percent = 90 + (uniqueTrackCandidates == 0 ? 7 : 7 * indexedCandidates / (double)uniqueTrackCandidates);
                progress?.Report(new BpmStudioImportProgress(
                    "Indexing unique referenced music tracks…", files.Length, files.Length,
                    $"{indexedCandidates:N0} / {uniqueTrackCandidates:N0}", itemsProcessed,
                indexedCandidates, unsupported, percent));
            }
        }

        await using (var restoreFtsTrigger = connection.CreateCommand())
        {
            restoreFtsTrigger.Transaction = (SqliteTransaction)tx;
            restoreFtsTrigger.CommandText = """
CREATE TRIGGER IF NOT EXISTS songs_ai AFTER INSERT ON songs BEGIN
  INSERT INTO songs_fts(rowid, artist, title, manufacturer, disc_id, file_path)
  VALUES (new.id, new.artist, new.title, new.manufacturer, new.disc_id, new.file_path);
END;
""";
            await restoreFtsTrigger.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (archiveGroupFiles.Length > 0)
        {
            (virtualFoldersImported, virtualFolderTrackLinksImported) =
                await ImportVirtualFoldersAsync(connection, (SqliteTransaction)tx, progress, files.Length,
                    itemsProcessed, uniqueTrackCandidates, unsupported, cancellationToken).ConfigureAwait(false);
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
            "Committing database transaction", itemsProcessed, uniqueReferencedTracks, unsupported, 99.2));

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        progress?.Report(new BpmStudioImportProgress(
            "Finalizing database…", files.Length, files.Length,
            "Completing background database checkpoint", itemsProcessed, uniqueReferencedTracks, unsupported, 99.7));

        await using (var finishPragmas = connection.CreateCommand())
        {
            finishPragmas.CommandText = "PRAGMA wal_checkpoint(PASSIVE); PRAGMA wal_autocheckpoint=1000;";
            await finishPragmas.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(new BpmStudioImportProgress(
            "Import complete", files.Length, files.Length,
            string.Empty, itemsProcessed, uniqueReferencedTracks, unsupported, 100));

        if (archiveGroupFiles.Length > 0)
            warnings.Add($"Imported {virtualFoldersImported:N0} BPM Studio virtual folder(s) with {virtualFolderTrackLinksImported:N0} unique track link(s). Source files were read-only.");
        if (datedHistoryCopiesSkipped > 0)
            warnings.Add($"Collapsed {datedHistoryCopiesSkipped:N0} extra physical BPM daily-history copy file(s) by calendar date. The most complete copy for each day was retained.");
        if (duplicateListsSkipped > 0)
            warnings.Add($"Collapsed {duplicateListsSkipped:N0} duplicate BPM Studio playlist/history copy file(s). Only one logical copy was retained.");
        if (duplicateArchiveGroupCopiesSkipped > 0)
            warnings.Add($"Skipped {duplicateArchiveGroupCopiesSkipped:N0} older BPM Studio .GRP/.PLG copy file(s) with duplicate names. The newest modified copy was imported.");
        warnings.Add("BPM dated played-song lists are imported as both Music History rows and loadable 'BPM Daily History' lists, preserving their play order.");
        warnings.Add("Fast BPM import does not probe every referenced music file on disk. Missing/moved files remain visible in imported lists and can be checked later by a library rescan.");

        return new BpmStudioImportResult(playlists, historyLists, playlistItems, historyItems, uniqueReferencedTracks,
            virtualFoldersImported, virtualFolderTrackLinksImported, unsupported, warnings);
    }

    private static bool IsArchiveGroupFile(string file)
    {
        var ext = Path.GetExtension(file);
        return ext.Equals(".grp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".plg", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime SafeLastWriteTimeUtc(string path)
    {
        try { return File.GetLastWriteTimeUtc(path); }
        catch { return DateTime.MinValue; }
    }

    private static long SafeFileLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return -1; }
    }

    private static string BuildVirtualFolderPath(string selectedSource, string groupFile)
    {
        var parts = new List<string> { "BPM Studio" };
        if (Directory.Exists(selectedSource))
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(selectedSource), Path.GetDirectoryName(groupFile) ?? selectedSource);
            if (relative != ".")
                parts.AddRange(relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        var groupName = Path.GetFileNameWithoutExtension(groupFile).Trim();
        if (groupName.Length > 0 && !parts.Last().Equals(groupName, StringComparison.OrdinalIgnoreCase)) parts.Add(groupName);
        return string.Join("/", parts.Select(SanitizeVirtualFolderName));
    }

    private static string SanitizeVirtualFolderName(string value)
    {
        value = Regex.Replace(value.Trim(), @"[\x00-\x1F]", " ");
        return value.Length == 0 ? "Imported" : value[..Math.Min(80, value.Length)];
    }

    private static async Task<(int Folders, long Links)> ImportVirtualFoldersAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IProgress<BpmStudioImportProgress>? progress,
        int fileCount,
        long itemsProcessed,
        long indexedTracks,
        int unsupported,
        CancellationToken token)
    {
        var folderIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var created = 0;
        long links = 0;

        await using var read = connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText = "SELECT DISTINCT folder_path FROM temp_bpm_virtual_links ORDER BY folder_path COLLATE NOCASE;";
        await using var reader = await read.ExecuteReaderAsync(token).ConfigureAwait(false);
        var folderPaths = new List<string>();
        while (await reader.ReadAsync(token).ConfigureAwait(false)) folderPaths.Add(reader.GetString(0));
        await reader.DisposeAsync();

        await using var findFolder = connection.CreateCommand();
        findFolder.Transaction = transaction;
        findFolder.CommandText = "SELECT id FROM virtual_folders WHERE COALESCE(parent_id,0)=COALESCE($parent,0) AND name=$name COLLATE NOCASE LIMIT 1;";
        var ffParent = findFolder.Parameters.Add("$parent", SqliteType.Integer);
        var ffName = findFolder.Parameters.Add("$name", SqliteType.Text);

        await using var createFolder = connection.CreateCommand();
        createFolder.Transaction = transaction;
        createFolder.CommandText = "INSERT INTO virtual_folders(parent_id,name) VALUES($parent,$name) RETURNING id;";
        var cfParent = createFolder.Parameters.Add("$parent", SqliteType.Integer);
        var cfName = createFolder.Parameters.Add("$name", SqliteType.Text);

        await using (var tempMap = connection.CreateCommand())
        {
            tempMap.Transaction = transaction;
            tempMap.CommandText = """
CREATE TEMP TABLE IF NOT EXISTS temp_bpm_folder_ids(
    folder_path TEXT PRIMARY KEY COLLATE NOCASE,
    folder_id INTEGER NOT NULL
);
DELETE FROM temp_bpm_folder_ids;
""";
            await tempMap.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        await using var mapFolder = connection.CreateCommand();
        mapFolder.Transaction = transaction;
        mapFolder.CommandText = "INSERT OR REPLACE INTO temp_bpm_folder_ids(folder_path,folder_id) VALUES($path,$id);";
        var mfPath = mapFolder.Parameters.Add("$path", SqliteType.Text);
        var mfId = mapFolder.Parameters.Add("$id", SqliteType.Integer);
        mapFolder.Prepare();

        foreach (var folderPath in folderPaths)
        {
            token.ThrowIfCancellationRequested();
            long? parent = null;
            var accumulated = "";
            foreach (var part in folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                accumulated = accumulated.Length == 0 ? part : accumulated + "/" + part;
                if (folderIds.TryGetValue(accumulated, out var cached)) { parent = cached; continue; }
                ffParent.Value = parent ?? (object)DBNull.Value; ffName.Value = part;
                var found = await findFolder.ExecuteScalarAsync(token).ConfigureAwait(false);
                long id;
                if (found is not null && found is not DBNull) id = Convert.ToInt64(found);
                else
                {
                    cfParent.Value = parent ?? (object)DBNull.Value; cfName.Value = part;
                    id = Convert.ToInt64(await createFolder.ExecuteScalarAsync(token).ConfigureAwait(false));
                    created++;
                }
                folderIds[accumulated] = id; parent = id;
            }
            if (parent is null) continue;
            mfPath.Value = folderPath;
            mfId.Value = parent.Value;
            await mapFolder.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        long totalLinkCandidates;
        await using (var countLinks = connection.CreateCommand())
        {
            countLinks.Transaction = transaction;
            countLinks.CommandText = "SELECT COUNT(*) FROM temp_bpm_virtual_links;";
            totalLinkCandidates = Convert.ToInt64(await countLinks.ExecuteScalarAsync(token).ConfigureAwait(false));
        }

        progress?.Report(new BpmStudioImportProgress(
            "Linking BPM virtual folders…", fileCount, fileCount,
            totalLinkCandidates == 0 ? string.Empty : $"0 / {totalLinkCandidates:N0} track links",
            itemsProcessed, indexedTracks, unsupported, 97));

        const int linkBatchSize = 5000;
        long linkedCandidates = 0;
        long lastLinkRowId = 0;
        await using var bulkLinks = connection.CreateCommand();
        bulkLinks.Transaction = transaction;
        bulkLinks.CommandText = """
INSERT OR IGNORE INTO virtual_folder_songs(folder_id,song_id)
SELECT m.folder_id,s.id
FROM (
    SELECT rowid,folder_path,file_path
    FROM temp_bpm_virtual_links
    WHERE rowid > $after
    ORDER BY rowid
    LIMIT $batch
) l
JOIN temp_bpm_folder_ids m ON m.folder_path=l.folder_path COLLATE NOCASE
JOIN songs s ON s.file_path=l.file_path COLLATE NOCASE;
""";
        bulkLinks.Parameters.Add("$after", SqliteType.Integer);
        bulkLinks.Parameters.Add("$batch", SqliteType.Integer);

        while (linkedCandidates < totalLinkCandidates)
        {
            token.ThrowIfCancellationRequested();
            bulkLinks.Parameters["$after"].Value = lastLinkRowId;
            bulkLinks.Parameters["$batch"].Value = linkBatchSize;
            links += await bulkLinks.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            await using var last = connection.CreateCommand();
            last.Transaction = transaction;
            last.CommandText = "SELECT COALESCE(MAX(rowid),$after) FROM (SELECT rowid FROM temp_bpm_virtual_links WHERE rowid > $after ORDER BY rowid LIMIT $batch);";
            last.Parameters.AddWithValue("$after", lastLinkRowId);
            last.Parameters.AddWithValue("$batch", linkBatchSize);
            var nextRowId = Convert.ToInt64(await last.ExecuteScalarAsync(token).ConfigureAwait(false));
            if (nextRowId <= lastLinkRowId) break;
            lastLinkRowId = nextRowId;
            linkedCandidates = Math.Min(totalLinkCandidates, linkedCandidates + linkBatchSize);
            var percent = 97 + (totalLinkCandidates == 0 ? 2 : 2 * linkedCandidates / (double)totalLinkCandidates);
            progress?.Report(new BpmStudioImportProgress(
                "Linking BPM virtual folders…", fileCount, fileCount,
                $"{linkedCandidates:N0} / {totalLinkCandidates:N0} track links",
                itemsProcessed, indexedTracks, unsupported, percent));
        }
        return (created, links);
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
        var groups = allFiles.Where(IsArchiveGroupFile)
            .Select(Path.GetFileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
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
