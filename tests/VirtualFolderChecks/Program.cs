using HazzKaraokeHoster.Core.Models;
using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;

var dbPath = Path.Combine(AppContext.BaseDirectory, "virtual-folders-" + Guid.NewGuid() + ".db");
var db = new HazzDatabase(dbPath);
await db.InitializeAsync();
var repo = new LibraryRepository(db);

var rockSong = new SongRecord(0, "Queen", "Hammer To Fall", "", "", @"C:\Music\Queen.mp4", "MP4", 100, DateTimeOffset.UtcNow, MediaKind: "Music");
var jingle = new SongRecord(0, "Station", "Top Of Hour", "", "", @"C:\Jingles\Top.mp3", "MP3", 50, DateTimeOffset.UtcNow, MediaKind: "Music");
var karaoke = new SongRecord(0, "Bon Jovi", "Livin On A Prayer", "Sunfly", "SF001", @"C:\Karaoke\Bon Jovi.zip", "ZIP", 75, DateTimeOffset.UtcNow, MediaKind: "Karaoke");
var rockId = await repo.UpsertSongAsync(rockSong);
var jingleId = await repo.UpsertSongAsync(jingle);
var karaokeId = await repo.UpsertSongAsync(karaoke);
var videoSearch = await repo.SearchByKindAsync("Queen", "MusicVideo", 100);
if (videoSearch.Count != 1 || videoSearch[0].Id != rockId)
    throw new Exception("Music Video search did not return the matching music-library video.");
if ((await repo.SearchByKindAsync("Station", "MusicVideo", 100)).Count != 0)
    throw new Exception("Music Video search included an audio-only track.");

var eighties = await repo.CreateVirtualFolderAsync("80s");
var rock = await repo.CreateVirtualFolderAsync("Rock", eighties);
var jingles = await repo.CreateVirtualFolderAsync("Jingles");
await repo.AddSongToVirtualFolderAsync(rock, rockId);
await repo.AddSongToVirtualFolderAsync(rock, karaokeId);
await repo.AddSongToVirtualFolderAsync(jingles, jingleId);
await repo.AddSongToVirtualFolderAsync(eighties, rockId); // Same song may appear in several virtual folders.
await repo.AddSongToVirtualFolderAsync(rock, rockId);     // Duplicate assignment stays one link.

var folders = await repo.GetVirtualFoldersAsync();
if (folders.Count != 3 || folders.Single(x => x.Id == rock).ParentId != eighties || folders.Single(x => x.Id == rock).TrackCount != 2)
    throw new Exception("Folder hierarchy/count failed.");
var musicPage = await repo.BrowseVirtualFolderAsync(rock, "Music", "hammer", "Artist", false, 0, 500);
if (musicPage.TotalCount != 1 || musicPage.Items[0].Title != "Hammer To Fall") throw new Exception("Folder browse/filter failed.");
var karaokePage = await repo.BrowseVirtualFolderAsync(rock, "Karaoke", "", "Title", false, 0, 500);
if (karaokePage.TotalCount != 1 || karaokePage.Items[0].Id != karaokeId) throw new Exception("Media-kind filter failed.");

await repo.RenameVirtualFolderAsync(jingles, "Show Jingles");
await repo.RemoveSongFromVirtualFolderAsync(rock, rockId);
if ((await repo.BrowseVirtualFolderAsync(rock, "Music", "", "Artist", false, 0, 500)).TotalCount != 0)
    throw new Exception("Remove link failed.");
await repo.EmptyVirtualFolderAsync(jingles);
await repo.DeleteVirtualFolderAsync(eighties);
folders = await repo.GetVirtualFoldersAsync();
if (folders.Count != 1 || folders[0].Name != "Show Jingles") throw new Exception("Cascade delete failed.");

using var connection = new SqliteConnection(db.ConnectionString);
connection.Open();
using var count = connection.CreateCommand();
count.CommandText = "SELECT COUNT(*) FROM songs";
if (Convert.ToInt32(count.ExecuteScalar()) != 3) throw new Exception("Virtual folder changes affected library songs.");

var bpmRoot = Path.Combine(AppContext.BaseDirectory, "bpm-" + Guid.NewGuid());
var bpmGroupDir = Path.Combine(bpmRoot, "FileArchive");
Directory.CreateDirectory(bpmGroupDir);
var newestGroup = Path.Combine(bpmGroupDir, "80s.grp");
var bulkGroupPaths = Enumerable.Range(0, 5102).Select(i => $@"C:\BpmBulk\Artist {i:D4} - Track {i:D4}.mp3").ToArray();
await File.WriteAllLinesAsync(newestGroup, bulkGroupPaths);
var oldGroupDir = Path.Combine(bpmRoot, "OldBackup");
Directory.CreateDirectory(oldGroupDir);
var oldGroup = Path.Combine(oldGroupDir, "80s.grp");
await File.WriteAllTextAsync(oldGroup, "C:\\Music\\Should Not Import.mp3\r\n");
File.SetLastWriteTimeUtc(oldGroup, DateTime.UtcNow.AddDays(-2));
File.SetLastWriteTimeUtc(newestGroup, DateTime.UtcNow);
var bpmResult = await new BpmStudioImportService(db).ImportAsync(bpmRoot);
if (bpmResult.VirtualFoldersImported != 3 || bpmResult.VirtualFolderTrackLinksImported != bulkGroupPaths.Length)
    throw new Exception("BPM virtual-folder import counts failed.");
folders = await repo.GetVirtualFoldersAsync();
var bpmFolder = folders.Single(x => x.Name == "BPM Studio");
var archiveFolder = folders.Single(x => x.Name == "FileArchive" && x.ParentId == bpmFolder.Id);
var importedEighties = folders.Single(x => x.Name == "80s" && x.ParentId == archiveFolder.Id);
if (folders.Any(x => x.Name == "OldBackup" && x.ParentId == bpmFolder.Id))
    throw new Exception("Older duplicate BPM group was imported.");
if ((await repo.BrowseVirtualFolderAsync(importedEighties.Id, "Music", "", "Artist", false, 0, 500)).TotalCount != bulkGroupPaths.Length)
    throw new Exception("BPM virtual-folder track links failed.");
using (var ftsCount = connection.CreateCommand())
{
    ftsCount.CommandText = "SELECT COUNT(*) FROM songs_fts";
    var ftsRows = Convert.ToInt32(ftsCount.ExecuteScalar());
    if (ftsRows != 3 + bulkGroupPaths.Length) throw new Exception($"BPM bulk full-text index was not populated (rows={ftsRows}).");
}
using (var triggerCheck = connection.CreateCommand())
{
    triggerCheck.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name='songs_ai'";
    if (Convert.ToInt32(triggerCheck.ExecuteScalar()) != 1) throw new Exception("BPM bulk import did not restore the songs search trigger.");
}
var repeatProgress = new List<BpmStudioImportProgress>();
var bpmRepeat = await new BpmStudioImportService(db).ImportAsync(bpmRoot, new Progress<BpmStudioImportProgress>(p => repeatProgress.Add(p)));
if (bpmRepeat.VirtualFoldersImported != 0 || bpmRepeat.VirtualFolderTrackLinksImported != 0)
    throw new Exception("Repeated BPM virtual-folder import created duplicates.");
if (!repeatProgress.Any(p => p.Phase.Contains("Skipping unchanged BPM virtual folder", StringComparison.OrdinalIgnoreCase)))
    throw new Exception("Repeated BPM virtual-folder import did not use the unchanged-source cache.");

Console.WriteLine("PASS: create, nesting, rename, multi-folder links, duplicate protection, browse, search, media filtering, remove, empty, cascade delete, media preservation and BPM Studio group import.");
