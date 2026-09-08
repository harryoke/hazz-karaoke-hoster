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
Console.WriteLine("PASS: create, nesting, rename, multi-folder links, duplicate protection, browse, search, media filtering, remove, empty, cascade delete and media preservation.");
