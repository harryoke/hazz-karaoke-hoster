using HazzKaraokeHoster.Core;
using HazzKaraokeHoster.App;
using System.Text;

var root = Path.Combine(AppContext.BaseDirectory, "fixtures-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
void Check(bool value, string message) { if (!value) throw new Exception(message); }
var audio = new byte[417 * 10];
for (var i = 0; i < 10; i++) { audio[i*417] = 0xff; audio[i*417+1] = 0xfb; audio[i*417+2] = 0x90; }
foreach (byte version in new byte[] { 3, 4 })
{
    var path = Path.Combine(root, "v" + version + ".mp3");
    File.WriteAllBytes(path, audio);
    using (var file = TagLib.File.Create(path, TagLib.ReadStyle.None))
    {
        var tag = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true);
        tag.Version = version; tag.Performers = new[] { "Björk", "Artist two" }; tag.Title = "日本語 Song"; tag.Album = "Album";
        file.Save();
    }
    var before = File.ReadAllBytes(path);
    var read = await Mp3Metadata.ReadAsync(path, "Fallback", "Filename");
    Check(read.Artist.Contains("Björk") && read.Title == "日本語 Song", "Unicode v2 tag failed");
    Check(before.SequenceEqual(File.ReadAllBytes(path)), "Reading changed file");
    await Mp3Metadata.SaveAsync(path, "Edited", "New title", "New album");
    read = await Mp3Metadata.ReadAsync(path, "Fallback", "Filename");
    Check(read == ("Edited", "New title"), "Saved tags not readable");
    using (var file = TagLib.File.Create(path, TagLib.ReadStyle.None)) Check(file.Tag.Album == "New album", "Album not saved");
    Check(File.ReadAllBytes(path).AsSpan().IndexOf(audio) >= 0, "Audio bytes changed");
    await Mp3Metadata.SaveAsync(path, "", "", "");
    Check(await Mp3Metadata.ReadAsync(path, "Fallback", "Filename") == ("Fallback", "Filename"), "Blank-tag fallback failed");
}
var v1 = Path.Combine(root, "v1.mp3");
var footer = new byte[128]; Encoding.ASCII.GetBytes("TAG").CopyTo(footer, 0);
Encoding.ASCII.GetBytes("Old title").CopyTo(footer, 3); Encoding.ASCII.GetBytes("Old artist").CopyTo(footer, 33);
File.WriteAllBytes(v1, audio.Concat(footer).ToArray());
Check(await Mp3Metadata.ReadAsync(v1, "", "") == ("Old artist", "Old title"), "ID3v1 failed");
var malformed = Path.Combine(root, "bad.mp3"); File.WriteAllText(malformed, "broken tag");
Check(await Mp3Metadata.ReadAsync(malformed, "A", "T") == ("A", "T"), "Malformed tag fallback failed");
Check(await Mp3Metadata.ReadAsync(Path.Combine(root,"missing.mp3"), "A", "T") == ("A", "T"), "Missing fallback failed");
var favouritesPath = Path.Combine(root, "favourites.json");
new MusicFavourites(favouritesPath).Set(new[] { v1 }, true);
Check(new MusicFavourites(favouritesPath).Contains(v1.ToUpperInvariant()), "Favourite did not persist case-insensitively");
new MusicFavourites(favouritesPath).Set(new[] { v1 }, false);
Check(!new MusicFavourites(favouritesPath).Contains(v1), "Unmark did not persist");
Console.WriteLine("PASS: ID3v1/v2.3/v2.4, Unicode, read-only lookup, tag saving, unchanged audio, missing/malformed/blank fallback, persistent favourites.");
