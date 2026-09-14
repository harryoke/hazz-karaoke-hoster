namespace HazzKaraokeHoster.Core;

/// <summary>Read-only tag lookup. Limit concurrent disk reads independently of playback.</summary>
public static class Mp3Metadata
{
    private static readonly SemaphoreSlim Readers = new(2);

    public static Task SaveAsync(string path, string artist, string title, string album) => Task.Run(() =>
    {
        if (!Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only MP3 tag editing is supported.", nameof(path));
        var original = new FileInfo(path);
        var size = original.Length; var stamp = original.LastWriteTimeUtc;
        var temporary = Path.Combine(original.DirectoryName!, ".hazz-tags-" + Guid.NewGuid().ToString("N") + ".mp3");
        try
        {
            File.Copy(path, temporary);
            using (var file = TagLib.File.Create(temporary, TagLib.ReadStyle.None))
            {
                if (!string.Equals(string.Join(" / ", file.Tag.Performers), artist, StringComparison.Ordinal))
                    file.Tag.Performers = string.IsNullOrWhiteSpace(artist) ? Array.Empty<string>() : new[] { artist };
                file.Tag.Title = title; file.Tag.Album = album;
                file.Save();
            }
            original.Refresh();
            if (original.Length != size || original.LastWriteTimeUtc != stamp)
                throw new IOException("The original file changed while editing. Reopen the tag editor and try again.");
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    });

    public static async Task<(string Artist, string Title)> ReadAsync(
        string path, string artist, string title, CancellationToken token = default)
    {
        if (!string.Equals(Path.GetExtension(path), ".mp3", StringComparison.OrdinalIgnoreCase))
            return (artist, title);
        await Readers.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var file = TagLib.File.Create(path, TagLib.ReadStyle.None);
                    var taggedArtist = string.Join(" / ", file.Tag.Performers.Where(x => !string.IsNullOrWhiteSpace(x)));
                    var taggedTitle = file.Tag.Title;
                    return (string.IsNullOrWhiteSpace(taggedArtist) ? artist : taggedArtist.Trim(),
                        string.IsNullOrWhiteSpace(taggedTitle) ? title : taggedTitle.Trim());
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Missing, locked or malformed tags must never prevent playback.
                    return (artist, title);
                }
            }, token).ConfigureAwait(false);
        }
        finally { Readers.Release(); }
    }
}
