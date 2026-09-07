using HazzKaraokeHoster.Core.Models;
using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;

if (args is ["--preview-mediamonkey", var mediaMonkeyDatabase])
{
    var probeRoot = Directory.CreateTempSubdirectory("hazz-mediamonkey-preview-");
    try
    {
        var probeDatabase = new HazzDatabase(Path.Combine(probeRoot.FullName, "hazz-probe.db"));
        var probeImporter = new ExternalLibraryImportService(probeDatabase);
        var realPreview = await probeImporter.PreviewAsync(mediaMonkeyDatabase);
        Require(realPreview.DetectedSource == "MediaMonkey", "real MediaMonkey detection");
        Require(realPreview.SampleRows.Count > 0, "real MediaMonkey preview rows");
        Require(realPreview.SampleRows.All(x => Path.IsPathRooted(x.FilePath)), "real MediaMonkey absolute paths");
        var existing = realPreview.SampleRows.Count(x => File.Exists(x.FilePath));
        Console.WriteLine($"PASS: real MediaMonkey preview decoded {realPreview.SampleRows.Count:N0} absolute sample paths; {existing:N0} currently exist.");
    }
    finally
    {
        try { probeRoot.Delete(true); } catch { }
    }
    return;
}

if (args is ["--benchmark-mediamonkey", var benchmarkDatabase])
{
    var benchmarkRoot = Directory.CreateTempSubdirectory("hazz-mediamonkey-benchmark-");
    try
    {
        var benchmarkTarget = new HazzDatabase(Path.Combine(benchmarkRoot.FullName, "hazz-benchmark.db"));
        var benchmarkImporter = new ExternalLibraryImportService(benchmarkTarget);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var benchmarkResult = await benchmarkImporter.ImportAsync(benchmarkDatabase, ExternalMediaKindMode.Auto, false);
        stopwatch.Stop();
        var rate = benchmarkResult.RowsRead / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
        Console.WriteLine($"PASS: imported {benchmarkResult.RecordsImported:N0}/{benchmarkResult.RowsRead:N0} real MediaMonkey rows in {stopwatch.Elapsed.TotalSeconds:N1}s ({rate:N0} rows/second), errors={benchmarkResult.Errors:N0}.");
        Require(benchmarkResult.RecordsImported > 0 && benchmarkResult.Errors == 0, "real MediaMonkey benchmark import");
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        try { benchmarkRoot.Delete(true); } catch { }
    }
    return;
}

var root = Directory.CreateTempSubdirectory("hazz-smart-import-");
try
{
    var media = Path.Combine(root.FullName, "Karaoke", "ABBA - Waterloo.mp3");
    Directory.CreateDirectory(Path.GetDirectoryName(media)!);
    await File.WriteAllBytesAsync(media, new byte[] { 1 });
    var mediaMonkeyFolder = Directory.CreateDirectory(Path.Combine(root.FullName, "MediaMonkey"));
    var mm = Path.Combine(mediaMonkeyFolder.FullName, "MM5.DB");
    var encodedMediaMonkeyPath = media[1..];
    var driveIndex = char.ToUpperInvariant(Path.GetPathRoot(media)![0]) - 'A';
    await using (var connection = new SqliteConnection($"Data Source={mm}"))
    {
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
CREATE TABLE Medias(IDMedia INTEGER PRIMARY KEY, DriveLetter INTEGER, Location TEXT);
INSERT INTO Medias VALUES(5,$drive,'');
CREATE TABLE Songs(SongPath TEXT, Artist TEXT, Author TEXT, SongTitle TEXT, Publisher TEXT, DiscNumber TEXT, IDMedia INTEGER);
INSERT INTO Songs VALUES($p,'ABBA','','Waterloo','','',5);
CREATE VIRTUAL TABLE ASongsText USING fts5(SongPath, Artist, SongTitle);
PRAGMA writable_schema=ON;
UPDATE sqlite_master
SET sql='CREATE VIRTUAL TABLE ASongsText USING fts5(SongPath, Artist, SongTitle, tokenize=''mm'')'
WHERE name='ASongsText';
PRAGMA writable_schema=OFF;
""";
        command.Parameters.AddWithValue("$p", encodedMediaMonkeyPath);
        command.Parameters.AddWithValue("$drive", driveIndex);
        await command.ExecuteNonQueryAsync();
    }
    SqliteConnection.ClearAllPools();

    var target = new HazzDatabase(Path.Combine(root.FullName, "hazz-test.db"));
    var importer = new ExternalLibraryImportService(target);
    var preview = await importer.PreviewAsync(mediaMonkeyFolder.FullName);
    Require(preview.DetectedSource == "MediaMonkey", "MediaMonkey folder detection with proprietary mm tokenizer");
    Require(preview.SampleRows.Count == 1 && preview.SampleRows[0].Title == "Waterloo", "MediaMonkey schema mapping");
    Require(Path.GetFullPath(preview.SampleRows[0].FilePath) == Path.GetFullPath(media), "MediaMonkey IDMedia/DriveLetter path decoding");
    Require(preview.DetectedObjects.Any(x => x.Contains("confidence", StringComparison.OrdinalIgnoreCase)), "confidence evidence");
    await target.InitializeAsync();
    var legacyPath = Path.Combine(Path.GetDirectoryName(mm)!, ";" + encodedMediaMonkeyPath[1..]);
    long legacySongId;
    await using (var hazz = new SqliteConnection(target.ConnectionString))
    {
        await hazz.OpenAsync();
        await using var seed = hazz.CreateCommand();
        seed.CommandText = """
INSERT INTO songs(artist,title,file_path,format,media_kind) VALUES('ABBA','Waterloo',$legacy,'MP3','Karaoke') RETURNING id;
""";
        seed.Parameters.AddWithValue("$legacy", legacyPath);
        legacySongId = Convert.ToInt64(await seed.ExecuteScalarAsync());
        await using var source = hazz.CreateCommand();
        source.CommandText = "INSERT INTO song_sources(song_id,source_type,source_path) VALUES($id,'MediaMonkey',$source)";
        source.Parameters.AddWithValue("$id", legacySongId);
        source.Parameters.AddWithValue("$source", mm);
        await source.ExecuteNonQueryAsync();
    }
    SqliteConnection.ClearAllPools();
    var before = File.ReadAllBytes(mm);
    var result = await importer.ImportAsync(mediaMonkeyFolder.FullName, ExternalMediaKindMode.Auto, true);
    Require(result.RecordsImported == 1 && result.KaraokeImported == 1,
        $"read-only MediaMonkey import (read={result.RowsRead}, imported={result.RecordsImported}, karaoke={result.KaraokeImported}, missing={result.MissingFiles}, errors={result.Errors}: {string.Join(" | ", result.Warnings)})");
    SqliteConnection.ClearAllPools();
    Require(before.SequenceEqual(File.ReadAllBytes(mm)), "source database unchanged");
    await using (var hazz = new SqliteConnection(target.ConnectionString))
    {
        await hazz.OpenAsync();
        await using var repaired = hazz.CreateCommand();
        repaired.CommandText = "SELECT file_path FROM songs WHERE id=$id";
        repaired.Parameters.AddWithValue("$id", legacySongId);
        Require(string.Equals(Convert.ToString(await repaired.ExecuteScalarAsync()), media, StringComparison.OrdinalIgnoreCase), "legacy MediaMonkey path repaired in place");
    }

    var csv = Path.Combine(root.FullName, "CompuHost-library.csv");
    await File.WriteAllTextAsync(csv, $"FilePath,Artist,SongTitle{Environment.NewLine}\"{media}\",ABBA,Waterloo");
    Require((await importer.PreviewAsync(csv)).DetectedSource == "CompuHost", "CompuHost export detection");

    var lyrxFolder = Directory.CreateDirectory(Path.Combine(root.FullName, "Lyrx"));
    File.Copy(mm, Path.Combine(lyrxFolder.FullName, "library.db"));
    Require((await importer.PreviewAsync(lyrxFolder.FullName)).DetectedSource == "Lyrx", "Lyrx folder detection");

    var json = Path.Combine(root.FullName, "KaraFun-export.json");
    await File.WriteAllTextAsync(json, $$"""{"tracks":[{"filePath":"{{media.Replace("\\", "\\\\")}}","artist":"ABBA","title":"Waterloo"}]}""");
    Require((await importer.PreviewAsync(json)).DetectedSource == "KaraFun", "KaraFun JSON detection/mapping");

    var xml = Path.Combine(root.FullName, "database.xml");
    await File.WriteAllTextAsync(xml, $"<VirtualDJ><Song FilePath=\"{System.Security.SecurityElement.Escape(media)}\" Author=\"ABBA\" Title=\"Waterloo\" /></VirtualDJ>");
    Require((await importer.PreviewAsync(xml)).DetectedSource == "VirtualDJ", "VirtualDJ XML detection/mapping");

    var itunes = Path.Combine(root.FullName, "iTunes Music Library.xml");
    await File.WriteAllTextAsync(itunes, $"<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>Major Version</key><integer>1</integer><key>Application Version</key><string>iTunes 12</string><key>Tracks</key><dict><key>1</key><dict><key>Name</key><string>Waterloo</string><key>Artist</key><string>ABBA</string><key>Location</key><string>{System.Security.SecurityElement.Escape(new Uri(media).AbsoluteUri)}</string></dict></dict></dict></plist>");
    var itunesPreview = await importer.PreviewAsync(itunes);
    Require(itunesPreview.DetectedSource == "Apple Music / iTunes" && itunesPreview.SampleRows.Count == 1, "iTunes plist detection/mapping");

    var m3u = Path.Combine(root.FullName, "playlist.m3u8");
    await File.WriteAllTextAsync(m3u, "#EXTM3U\n#EXTINF:180,ABBA - Waterloo\n" + media);
    Require((await importer.PreviewAsync(m3u)).SampleRows.Count == 1, "generic playlist fallback");

    var xspf = Path.Combine(root.FullName, "Mixxx-playlist.xspf");
    await File.WriteAllTextAsync(xspf, $"<playlist><trackList><track><location>{System.Security.SecurityElement.Escape(new Uri(media).AbsoluteUri)}</location></track></trackList></playlist>");
    var xspfPreview = await importer.PreviewAsync(xspf);
    Require(xspfPreview.DetectedSource == "Mixxx" && xspfPreview.SampleRows.Count == 1, "Mixxx XSPF detection/mapping");

    Console.WriteLine("PASS: smart folder/file detection, MediaMonkey SQLite, CompuHost CSV, Lyrx database, KaraFun JSON, VirtualDJ XML, iTunes plist, Mixxx XSPF, generic playlist, read-only source and Hazz import.");
}
finally
{
    try { root.Delete(true); } catch { }
}

static void Require(bool value, string check)
{
    if (!value) throw new InvalidOperationException("Failed: " + check);
}
