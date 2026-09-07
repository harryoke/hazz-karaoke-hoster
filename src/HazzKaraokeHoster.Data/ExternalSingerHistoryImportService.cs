using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using Microsoft.Data.Sqlite;

namespace HazzKaraokeHoster.Data;

public sealed class ExternalSingerHistoryImportService(HazzDatabase database) : IExternalSingerHistoryImportService
{
    private sealed record HistoryRow(string Singer, string Artist, string Title, string FilePath, DateTimeOffset SungAt, int KeyChange, double SyncSeconds, int TimesSung);
    private sealed record Mapping(string Singer, string? Artist, string? Title, string? FilePath, string? Date, string? Key, string? Sync, string? Times)
    {
        public override string ToString() => $"Singer={Singer}; Artist={Artist ?? "—"}; Title={Title ?? "—"}; File={FilePath ?? "—"}; Date={Date ?? "—"}; Key={Key ?? "—"}; Times={Times ?? "—"}";
    }

    private static readonly string[] SingerNames = ["singer", "singername", "customer", "customername", "patron", "patronname", "performer", "performername", "username"];
    private static readonly string[] ArtistNames = ["artist", "songartist", "author", "creator"];
    private static readonly string[] TitleNames = ["title", "song", "songtitle", "track", "tracktitle"];
    private static readonly string[] PathNames = ["filepath", "file", "path", "filename", "location", "songpath"];
    private static readonly string[] DateNames = ["sungat", "sungdate", "lastsung", "playedat", "playdate", "date", "datetime", "timestamp"];
    private static readonly string[] KeyNames = ["keychange", "key", "pitch", "transpose", "semitones"];
    private static readonly string[] SyncNames = ["cdgsyncseconds", "syncseconds", "sync", "offset"];
    private static readonly string[] TimesNames = ["timessung", "playcount", "timesplayed", "count", "plays"];

    public async Task<SingerHistoryImportPreview> PreviewAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        sourcePath = NormalizeSource(sourcePath);
        var warnings = new List<string>();
        var result = await Task.Run(() =>
        {
            var rows = new List<HistoryRow>();
            string mapping;
            using var enumerator = ReadRows(sourcePath, warnings, out mapping).GetEnumerator();
            while (rows.Count < 21 && enumerator.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rows.Add(enumerator.Current);
            }
            return (Rows: rows, Mapping: mapping);
        }, cancellationToken);
        var samples = result.Rows.Take(20).Select(ToPreview).ToArray();
        if (samples.Length == 0) warnings.Add("No rows containing both a singer and a song title/file were detected.");
        return new SingerHistoryImportPreview(DetectSource(sourcePath), sourcePath, result.Mapping, samples, result.Rows.Count > 20, warnings);
    }

    public async Task<SingerHistoryImportResult> ImportAsync(string sourcePath, IProgress<SingerHistoryImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        sourcePath = NormalizeSource(sourcePath);
        await database.InitializeAsync(cancellationToken);
        var warnings = new List<string>();
        long read = 0, imported = 0, matched = 0, unmatched = 0, errors = 0;
        var singerIds = new HashSet<long>();

        await using var target = new SqliteConnection(database.ConnectionString);
        await target.OpenAsync(cancellationToken);
        await using var transaction = target.BeginTransaction();
        await using var clear = target.CreateCommand();
        clear.Transaction = transaction;
        clear.CommandText = "DELETE FROM singer_history WHERE imported_from=$source";
        clear.Parameters.AddWithValue("$source", sourcePath);
        await clear.ExecuteNonQueryAsync(cancellationToken);

        await using var singer = target.CreateCommand();
        singer.Transaction = transaction;
        singer.CommandText = """
INSERT INTO singers(display_name,last_seen_utc) VALUES($name,$date)
ON CONFLICT(display_name) DO UPDATE SET last_seen_utc=CASE
 WHEN singers.last_seen_utc IS NULL OR excluded.last_seen_utc>singers.last_seen_utc THEN excluded.last_seen_utc ELSE singers.last_seen_utc END
RETURNING id;
""";
        var singerName = singer.Parameters.Add("$name", SqliteType.Text);
        var singerDate = singer.Parameters.Add("$date", SqliteType.Text);
        singer.Prepare();

        await using var findSong = target.CreateCommand();
        findSong.Transaction = transaction;
        findSong.CommandText = """
SELECT id FROM songs
WHERE ($path<>'' AND file_path=$path COLLATE NOCASE)
   OR ($title<>'' AND title=$title COLLATE NOCASE AND ($artist='' OR artist=$artist COLLATE NOCASE))
ORDER BY CASE WHEN $path<>'' AND file_path=$path COLLATE NOCASE THEN 0 ELSE 1 END,id LIMIT 1;
""";
        var songPath = findSong.Parameters.Add("$path", SqliteType.Text);
        var songTitle = findSong.Parameters.Add("$title", SqliteType.Text);
        var songArtist = findSong.Parameters.Add("$artist", SqliteType.Text);
        findSong.Prepare();

        await using var history = target.CreateCommand();
        history.Transaction = transaction;
        history.CommandText = """
INSERT INTO singer_history(singer_id,song_id,artist,title,file_path,sung_at_utc,key_change,cdg_sync_seconds,times_sung,imported_from)
VALUES($singer,$song,$artist,$title,$path,$date,$key,$sync,$times,$source);
""";
        foreach (var name in new[] { "$singer", "$song", "$artist", "$title", "$path", "$date", "$key", "$sync", "$times", "$source" })
            history.Parameters.Add(name, name is "$singer" or "$song" or "$key" or "$times" ? SqliteType.Integer : name == "$sync" ? SqliteType.Real : SqliteType.Text);
        history.Prepare();

        string ignoredMapping;
        foreach (var row in ReadRows(sourcePath, warnings, out ignoredMapping))
        {
            cancellationToken.ThrowIfCancellationRequested();
            read++;
            try
            {
                singerName.Value = row.Singer;
                singerDate.Value = row.SungAt.UtcDateTime.ToString("O");
                var singerId = Convert.ToInt64(await singer.ExecuteScalarAsync(cancellationToken));
                singerIds.Add(singerId);

                songPath.Value = row.FilePath;
                songTitle.Value = row.Title;
                songArtist.Value = row.Artist;
                var songValue = await findSong.ExecuteScalarAsync(cancellationToken);
                long? songId = songValue is null or DBNull ? null : Convert.ToInt64(songValue);
                if (songId is null) unmatched++; else matched++;

                history.Parameters["$singer"].Value = singerId;
                history.Parameters["$song"].Value = songId ?? (object)DBNull.Value;
                history.Parameters["$artist"].Value = row.Artist;
                history.Parameters["$title"].Value = row.Title;
                history.Parameters["$path"].Value = row.FilePath;
                history.Parameters["$date"].Value = row.SungAt.UtcDateTime.ToString("O");
                history.Parameters["$key"].Value = row.KeyChange;
                history.Parameters["$sync"].Value = row.SyncSeconds;
                history.Parameters["$times"].Value = row.TimesSung;
                history.Parameters["$source"].Value = sourcePath;
                await history.ExecuteNonQueryAsync(cancellationToken);
                imported++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors++;
                if (warnings.Count < 50) warnings.Add($"Row {read:N0} ({row.Singer}): {ex.Message}");
            }
            if (read % 250 == 0) progress?.Report(new SingerHistoryImportProgress(read, imported, matched, unmatched, errors, row.Singer));
        }
        if (imported == 0)
            throw new InvalidDataException("No usable singer-history rows were found. The existing imported history was left unchanged.");
        await transaction.CommitAsync(cancellationToken);
        progress?.Report(new SingerHistoryImportProgress(read, imported, matched, unmatched, errors, string.Empty));
        return new SingerHistoryImportResult(DetectSource(sourcePath), read, singerIds.Count, imported, matched, unmatched, errors, warnings);
    }

    private static IEnumerable<HistoryRow> ReadRows(string path, List<string> warnings, out string mappingText)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".csv" or ".tsv" or ".txt") return ReadDelimited(path, warnings, out mappingText);
        if (extension == ".json") return ReadJson(path, warnings, out mappingText);
        if (extension == ".xml") return ReadXml(path, warnings, out mappingText);
        if (extension is ".db" or ".db3" or ".sqlite" or ".sqlite3" or ".s3db" or ".sqlitedb" or ".musicdb") return ReadSqlite(path, warnings, out mappingText);
        if (extension is ".mdb" or ".accdb" or ".kdb") return ReadAccess(path, warnings, out mappingText);
        throw new InvalidDataException($"Singer history format '{extension}' is not supported. Use CSV, TSV, JSON, XML, SQLite, MDB, ACCDB or KDB.");
    }

    private static IEnumerable<HistoryRow> ReadDelimited(string path, List<string> warnings, out string mappingText)
    {
        var lines = File.ReadLines(path).GetEnumerator();
        if (!lines.MoveNext()) { mappingText = "Empty file"; return []; }
        var delimiter = Path.GetExtension(path).Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : GuessDelimiter(lines.Current);
        var headers = ParseDelimitedLine(lines.Current, delimiter);
        var mapping = DetectMapping(headers);
        mappingText = mapping.ToString();
        if (mapping.Singer.Length == 0) return [];
        return Enumerate();
        IEnumerable<HistoryRow> Enumerate()
        {
            using (lines)
            {
                while (lines.MoveNext())
                {
                    var values = ParseDelimitedLine(lines.Current, delimiter);
                    var dictionary = headers.Select((header, index) => (header, Value: index < values.Count ? values[index] : string.Empty))
                        .ToDictionary(x => x.header, x => x.Value, StringComparer.OrdinalIgnoreCase);
                    if (BuildRow(dictionary, mapping, path) is { } row) yield return row;
                }
            }
        }
    }

    private static IEnumerable<HistoryRow> ReadJson(string path, List<string> warnings, out string mappingText)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var objects = EnumerateJsonObjects(document.RootElement).Select(element => element.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase)).ToArray();
        var headers = objects.SelectMany(x => x.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var mapping = DetectMapping(headers); mappingText = mapping.ToString();
        return objects.Select(x => BuildRow(x, mapping, path)).Where(x => x is not null).Cast<HistoryRow>().ToArray();
    }

    private static IEnumerable<JsonElement> EnumerateJsonObjects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject()) foreach (var child in EnumerateJsonObjects(property.Value)) yield return child;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) foreach (var child in EnumerateJsonObjects(item)) yield return child;
    }

    private static IEnumerable<HistoryRow> ReadXml(string path, List<string> warnings, out string mappingText)
    {
        var document = XDocument.Load(path, LoadOptions.None);
        var records = document.Descendants().Select(element => element.Attributes().Select(a => (a.Name.LocalName, a.Value))
            .Concat(element.Elements().Where(e => !e.HasElements).Select(e => (e.Name.LocalName, e.Value)))
            .GroupBy(x => x.LocalName, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase))
            .Where(x => x.Count >= 2).ToArray();
        var mapping = DetectMapping(records.SelectMany(x => x.Keys).Distinct(StringComparer.OrdinalIgnoreCase)); mappingText = mapping.ToString();
        return records.Select(x => BuildRow(x, mapping, path)).Where(x => x is not null).Cast<HistoryRow>().ToArray();
    }

    private static IEnumerable<HistoryRow> ReadSqlite(string path, List<string> warnings, out string mappingText)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        var candidate = FindSqliteMapping(connection);
        mappingText = candidate is null ? "No singer-history table detected" : $"Table={candidate.Value.Table}; {candidate.Value.Mapping}";
        if (candidate is null) return [];
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM [{candidate.Value.Table.Replace("]", "]]", StringComparison.Ordinal)}]";
        using var reader = command.ExecuteReader();
        var rows = new List<HistoryRow>();
        while (reader.Read())
        {
            var values = Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, i => reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            if (BuildRow(values, candidate.Value.Mapping, path) is { } row) rows.Add(row);
        }
        return rows;
    }

    private static (string Table, Mapping Mapping)? FindSqliteMapping(SqliteConnection connection)
    {
        using var tables = connection.CreateCommand(); tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        using var tableReader = tables.ExecuteReader(); var names = new List<string>(); while (tableReader.Read()) names.Add(tableReader.GetString(0));
        foreach (var table in names.OrderByDescending(x => x.Contains("history", StringComparison.OrdinalIgnoreCase) || x.Contains("singer", StringComparison.OrdinalIgnoreCase)))
        {
            using var columns = connection.CreateCommand(); columns.CommandText = $"PRAGMA table_info([{table.Replace("]", "]]", StringComparison.Ordinal)}])";
            using var columnReader = columns.ExecuteReader(); var headers = new List<string>(); while (columnReader.Read()) headers.Add(columnReader.GetString(1));
            var mapping = DetectMapping(headers); if (mapping.Singer.Length > 0 && (mapping.Title is not null || mapping.FilePath is not null)) return (table, mapping);
        }
        return null;
    }

    private static IEnumerable<HistoryRow> ReadAccess(string path, List<string> warnings, out string mappingText)
    {
        using var connection = new OleDbConnection($"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={path};Mode=Read;Persist Security Info=False;"); connection.Open();
        var tables = connection.GetSchema("Tables").Rows.Cast<DataRow>().Where(r => string.Equals(Convert.ToString(r["TABLE_TYPE"]), "TABLE", StringComparison.OrdinalIgnoreCase)).Select(r => Convert.ToString(r["TABLE_NAME"])!).ToArray();
        foreach (var table in tables.OrderByDescending(x => x.Contains("history", StringComparison.OrdinalIgnoreCase) || x.Contains("singer", StringComparison.OrdinalIgnoreCase)))
        {
            using var command = new OleDbCommand($"SELECT * FROM [{table.Replace("]", "]]", StringComparison.Ordinal)}]", connection); using var reader = command.ExecuteReader(); if (reader is null) continue;
            var headers = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray(); var mapping = DetectMapping(headers);
            if (mapping.Singer.Length == 0 || (mapping.Title is null && mapping.FilePath is null)) continue;
            mappingText = $"Table={table}; {mapping}"; var rows = new List<HistoryRow>();
            while (reader.Read()) { var values = Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, i => reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty, StringComparer.OrdinalIgnoreCase); if (BuildRow(values, mapping, path) is { } row) rows.Add(row); }
            return rows;
        }
        mappingText = "No singer-history table detected"; return [];
    }

    private static Mapping DetectMapping(IEnumerable<string> headers)
    {
        var list = headers.ToArray(); string? Find(string[] choices) => list.FirstOrDefault(header => choices.Contains(NormalizeName(header), StringComparer.OrdinalIgnoreCase));
        return new Mapping(Find(SingerNames) ?? string.Empty, Find(ArtistNames), Find(TitleNames), Find(PathNames), Find(DateNames), Find(KeyNames), Find(SyncNames), Find(TimesNames));
    }

    private static HistoryRow? BuildRow(IReadOnlyDictionary<string, string> values, Mapping mapping, string source)
    {
        string Get(string? key) => key is not null && values.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        var singer = Get(mapping.Singer); var artist = Get(mapping.Artist); var title = Get(mapping.Title); var file = Get(mapping.FilePath);
        if (singer.Length == 0 || (title.Length == 0 && file.Length == 0)) return null;
        if (file.Length > 0 && !Path.IsPathRooted(file)) { try { file = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source) ?? string.Empty, file)); } catch { } }
        if (title.Length == 0 && file.Length > 0) title = Path.GetFileNameWithoutExtension(file);
        var date = ParseDate(Get(mapping.Date), source);
        _ = int.TryParse(Get(mapping.Key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var key);
        _ = double.TryParse(Get(mapping.Sync), NumberStyles.Float, CultureInfo.InvariantCulture, out var sync);
        _ = int.TryParse(Get(mapping.Times), NumberStyles.Integer, CultureInfo.InvariantCulture, out var times);
        return new HistoryRow(singer, artist, title, file, date, Math.Clamp(key, -12, 12), Math.Clamp(sync, -10, 10), Math.Clamp(times, 1, 10_000));
    }

    private static DateTimeOffset ParseDate(string value, string source)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var date) || DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date)) return date;
        return new DateTimeOffset(File.GetLastWriteTimeUtc(source), TimeSpan.Zero);
    }

    private static SingerHistoryImportPreviewRow ToPreview(HistoryRow row) => new(row.Singer, row.Artist, row.Title, row.FilePath, row.SungAt.ToLocalTime().ToString("g"), row.KeyChange, row.TimesSung);
    private static string NormalizeSource(string path) { if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Select a singer-history export or database."); path = Path.GetFullPath(path); if (!File.Exists(path)) throw new FileNotFoundException("Singer-history source not found.", path); return path; }
    private static string DetectSource(string path) { var value = path.ToLowerInvariant(); foreach (var pair in new[] { ("CompuHost", "compuhost"), ("Lyrx", "lyrx"), ("Karma", "karma"), ("OpenKJ", "openkj"), ("Siglos / PowerKaraoke", "siglos"), ("KaraFun", "karafun"), ("PCDJ DEX", "pcdj"), ("MTU Hoster", "hoster"), ("JustKaraoke", "justkaraoke"), ("Sax & Dottys", "sax") }) if (value.Contains(pair.Item2)) return pair.Item1; return $"Generic {Path.GetExtension(path).TrimStart('.').ToUpperInvariant()} singer history"; }
    private static string NormalizeName(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static char GuessDelimiter(string line) => new[] { ',', '\t', ';', '|' }.OrderByDescending(c => line.Count(x => x == c)).First();
    private static List<string> ParseDelimitedLine(string line, char delimiter) { var values = new List<string>(); var current = new System.Text.StringBuilder(); var quoted = false; for (var i = 0; i < line.Length; i++) { var c = line[i]; if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; } else quoted = !quoted; } else if (c == delimiter && !quoted) { values.Add(current.ToString().Trim()); current.Clear(); } else current.Append(c); } values.Add(current.ToString().Trim()); return values; }
}
