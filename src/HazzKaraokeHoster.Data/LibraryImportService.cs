using System.Diagnostics;
using System.Text.RegularExpressions;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

public sealed class LibraryImportService(HazzDatabase database) : ILibraryImportService
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".aif", ".aiff" };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".mp4", ".mkv", ".avi", ".mov", ".mpeg", ".mpg", ".wmv", ".m4v", ".vob", ".ts", ".m2ts", ".webm", ".divx" };

    private static readonly Regex DiscPrefixRegex = new(@"^(?<disc>[A-Za-z][A-Za-z0-9_-]{1,18}\d)(?:[-_ ]?(?<track>\d{1,3}))?$", RegexOptions.Compiled);
    private static readonly Regex ManufacturerRegex = new(@"^[A-Za-z]+", RegexOptions.Compiled);

    public async Task<LibraryImportResult> ImportAsync(
        LibraryImportOptions options,
        IProgress<LibraryImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var roots = CollapseOverlappingRoots((options.RootPaths ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase));
        if (roots.Length == 0) throw new DirectoryNotFoundException("No library folders were selected.");
        var missing = roots.Where(x => !Directory.Exists(x)).ToArray();
        if (missing.Length > 0) throw new DirectoryNotFoundException("One or more selected folders do not exist:\n" + string.Join("\n", missing));

        await database.InitializeAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();
        long scanned = 0, imported = 0, skipped = 0, errors = 0, companion = 0, unsupported = 0;

        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA busy_timeout=10000; PRAGMA cache_size=-131072;";
            await pragma.ExecuteNonQueryAsync(cancellationToken);
        }

        SqliteTransaction? transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO songs(artist,title,manufacturer,disc_id,file_path,format,file_size,date_added,cdg_sync_seconds,preferred_key,last_seen_utc,media_kind)
VALUES($artist,$title,$manufacturer,$disc,$path,$format,$size,$added,0,0,CURRENT_TIMESTAMP,$kind)
ON CONFLICT(file_path) DO UPDATE SET
 artist=excluded.artist,title=excluded.title,manufacturer=excluded.manufacturer,disc_id=excluded.disc_id,
 format=excluded.format,file_size=excluded.file_size,media_kind=excluded.media_kind,
 date_added=COALESCE(songs.date_added,excluded.date_added),last_seen_utc=CURRENT_TIMESTAMP;
""";
        var pArtist = command.Parameters.Add("$artist", SqliteType.Text);
        var pTitle = command.Parameters.Add("$title", SqliteType.Text);
        var pManufacturer = command.Parameters.Add("$manufacturer", SqliteType.Text);
        var pDisc = command.Parameters.Add("$disc", SqliteType.Text);
        var pPath = command.Parameters.Add("$path", SqliteType.Text);
        var pFormat = command.Parameters.Add("$format", SqliteType.Text);
        var pSize = command.Parameters.Add("$size", SqliteType.Integer);
        var pAdded = command.Parameters.Add("$added", SqliteType.Text);
        var pKind = command.Parameters.Add("$kind", SqliteType.Text);
        command.Prepare();

        const int batchSize = 2000;
        var batchCount = 0;

        try
        {
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var root = roots[rootIndex];
                foreach (var pathRaw in EnumerateFilesSafe(root, options.IncludeSubfolders, warnings, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var path = Path.GetFullPath(pathRaw);
                    scanned++;

                    try
                    {
                        var classification = Classify(path, options.Mode);
                        if (classification == ImportClassification.CompanionAudio)
                        {
                            companion++; skipped++;
                        }
                        else if (classification == ImportClassification.Unsupported)
                        {
                            unsupported++; skipped++;
                        }
                        else
                        {
                            var info = new FileInfo(path);
                            if (!info.Exists) { skipped++; }
                            else
                            {
                                var parsed = ParseName(path);
                                pArtist.Value = parsed.Artist;
                                pTitle.Value = parsed.Title;
                                pManufacturer.Value = parsed.Manufacturer;
                                pDisc.Value = parsed.DiscId;
                                pPath.Value = path;
                                pFormat.Value = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
                                pSize.Value = info.Length;
                                pAdded.Value = info.CreationTimeUtc == DateTime.MinValue ? DBNull.Value : info.CreationTimeUtc.ToString("O");
                                pKind.Value = classification == ImportClassification.Karaoke ? "Karaoke" : "Music";
                                await command.ExecuteNonQueryAsync(cancellationToken);
                                imported++;
                                batchCount++;

                                if (batchCount >= batchSize)
                                {
                                    await transaction!.CommitAsync(cancellationToken);
                                    await transaction.DisposeAsync();
                                    transaction = connection.BeginTransaction();
                                    command.Transaction = transaction;
                                    batchCount = 0;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        errors++;
                        if (warnings.Count < 100) warnings.Add($"{path}: {ex.Message}");
                    }

                    if (scanned % 250 == 0)
                        progress?.Report(new LibraryImportProgress(scanned, imported, skipped, path, companion, unsupported, errors, rootIndex + 1, roots.Length));
                }
            }

            await transaction!.CommitAsync(cancellationToken);
            progress?.Report(new LibraryImportProgress(scanned, imported, skipped, string.Empty, companion, unsupported, errors, roots.Length, roots.Length));
        }
        catch
        {
            if (transaction is not null)
            {
                try { await transaction.RollbackAsync(CancellationToken.None); } catch { }
            }
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }

        stopwatch.Stop();
        return new LibraryImportResult(scanned, imported, skipped, errors, stopwatch.Elapsed, warnings, companion, unsupported, roots.Length);
    }


    public async Task<SingleFileIndexResult> IndexFileAsync(
        string path,
        LibraryImportMode mode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new SingleFileIndexResult(SingleFileIndexOutcome.Missing, string.Empty, "No file path was supplied.");

        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            return new SingleFileIndexResult(SingleFileIndexOutcome.Missing, path, "File does not exist.");

        var classification = Classify(path, mode);
        if (classification == ImportClassification.CompanionAudio)
            return new SingleFileIndexResult(SingleFileIndexOutcome.CompanionAudioIgnored, path);
        if (classification == ImportClassification.Unsupported)
            return new SingleFileIndexResult(SingleFileIndexOutcome.Unsupported, path);

        try
        {
            await database.InitializeAsync(cancellationToken);
            var info = new FileInfo(path);
            var parsed = ParseName(path);
            await using var connection = new SqliteConnection(database.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
INSERT INTO songs(artist,title,manufacturer,disc_id,file_path,format,file_size,date_added,cdg_sync_seconds,preferred_key,last_seen_utc,media_kind)
VALUES($artist,$title,$manufacturer,$disc,$path,$format,$size,$added,0,0,CURRENT_TIMESTAMP,$kind)
ON CONFLICT(file_path) DO UPDATE SET
 artist=excluded.artist,title=excluded.title,manufacturer=excluded.manufacturer,disc_id=excluded.disc_id,
 format=excluded.format,file_size=excluded.file_size,media_kind=excluded.media_kind,
 date_added=COALESCE(songs.date_added,excluded.date_added),last_seen_utc=CURRENT_TIMESTAMP;
""";
            command.Parameters.AddWithValue("$artist", parsed.Artist);
            command.Parameters.AddWithValue("$title", parsed.Title);
            command.Parameters.AddWithValue("$manufacturer", parsed.Manufacturer);
            command.Parameters.AddWithValue("$disc", parsed.DiscId);
            command.Parameters.AddWithValue("$path", path);
            command.Parameters.AddWithValue("$format", Path.GetExtension(path).TrimStart('.').ToUpperInvariant());
            command.Parameters.AddWithValue("$size", info.Length);
            command.Parameters.AddWithValue("$added", info.CreationTimeUtc == DateTime.MinValue ? DBNull.Value : info.CreationTimeUtc.ToString("O"));
            command.Parameters.AddWithValue("$kind", classification == ImportClassification.Karaoke ? "Karaoke" : "Music");
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new SingleFileIndexResult(SingleFileIndexOutcome.Indexed, path);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new SingleFileIndexResult(SingleFileIndexOutcome.Error, path, ex.Message);
        }
    }


    private static string[] CollapseOverlappingRoots(IEnumerable<string> roots)
    {
        // Avoid a HashSet containing millions of file paths. If both a parent and one of its
        // subfolders were selected, scanning the parent already covers the child.
        var ordered = roots
            .Select(NormalizeRoot)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Length)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var kept = new List<string>();
        foreach (var candidate in ordered)
        {
            if (kept.Any(parent => IsSameOrChildPath(candidate, parent))) continue;
            kept.Add(candidate);
        }
        return kept.ToArray();
    }

    private static string NormalizeRoot(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsSameOrChildPath(string candidate, string parent)
    {
        if (string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase)) return true;
        var prefix = parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root, bool recursive, List<string> warnings, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(directory); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                if (warnings.Count < 100) warnings.Add($"Skipped folder {directory}: {ex.Message}");
                continue;
            }

            using (var enumerator = files.GetEnumerator())
            {
                while (true)
                {
                    string current;
                    try { if (!enumerator.MoveNext()) break; current = enumerator.Current; }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                        if (warnings.Count < 100) warnings.Add($"Stopped reading {directory}: {ex.Message}");
                        break;
                    }
                    yield return current;
                }
            }

            if (!recursive) continue;
            try { foreach (var child in Directory.EnumerateDirectories(directory)) pending.Push(child); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                if (warnings.Count < 100) warnings.Add($"Could not enumerate subfolders of {directory}: {ex.Message}");
            }
        }
    }

    private enum ImportClassification { Unsupported, CompanionAudio, Karaoke, Music }

    private static ImportClassification Classify(string path, LibraryImportMode mode)
    {
        var ext = Path.GetExtension(path);
        if (mode == LibraryImportMode.Karaoke)
        {
            if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) || ext.Equals(".cdg", StringComparison.OrdinalIgnoreCase) || VideoExtensions.Contains(ext))
                return ImportClassification.Karaoke;
            if (AudioExtensions.Contains(ext) && File.Exists(Path.ChangeExtension(path, ".cdg"))) return ImportClassification.CompanionAudio;
            return ImportClassification.Unsupported;
        }
        if (mode == LibraryImportMode.Music)
            return AudioExtensions.Contains(ext) || VideoExtensions.Contains(ext) ? ImportClassification.Music : ImportClassification.Unsupported;

        if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) || ext.Equals(".cdg", StringComparison.OrdinalIgnoreCase)) return ImportClassification.Karaoke;
        if (AudioExtensions.Contains(ext))
        {
            if (File.Exists(Path.ChangeExtension(path, ".cdg"))) return ImportClassification.CompanionAudio;
            return ImportClassification.Music;
        }
        if (VideoExtensions.Contains(ext)) return ImportClassification.Music;
        return ImportClassification.Unsupported;
    }

    internal static (string Artist, string Title, string Manufacturer, string DiscId) ParseName(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path).Trim();
        var parts = stem.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string artist = string.Empty, title = stem, manufacturer = string.Empty, disc = string.Empty;
        if (parts.Length >= 3 && DiscPrefixRegex.IsMatch(parts[0]))
        {
            disc = parts[0]; manufacturer = ManufacturerRegex.Match(disc).Value.ToUpperInvariant(); artist = parts[1]; title = string.Join(" - ", parts.Skip(2));
        }
        else if (parts.Length >= 2) { artist = parts[0]; title = string.Join(" - ", parts.Skip(1)); }
        return (artist, title, manufacturer, disc);
    }
}
