using System.Windows;
using System.Windows.Controls;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private void QueueSelectedNext(MusicDeckId deck)
    {
        var list = PlaylistFor(deck);
        var current = CurrentMusicItemFor(deck);
        var selected = list.Items.OfType<MusicQueueItem>().Where(x => list.SelectedItems.Contains(x) && !ReferenceEquals(x, current)).ToArray();
        foreach (var item in selected) list.Items.Remove(item);
        var insertion = current is null ? 0 : Math.Max(0, list.Items.IndexOf(current) + 1);
        foreach (var item in selected) list.Items.Insert(insertion++, item);
        RecalculateMusicDeckOrder(deck);
        MarkMusicDeckQueuesDirty();
        UpdateMusicAutomationStatus($"{selected.Length} track(s) queued next on {DeckName(deck)} • current playback continues");
    }

    private void MoveSelectedToOtherDeck(MusicDeckId deck)
    {
        var source = PlaylistFor(deck); var destination = PlaylistFor(Opposite(deck));
        var selected = source.Items.OfType<MusicQueueItem>().Where(x => source.SelectedItems.Contains(x) && !ReferenceEquals(x, CurrentMusicItemFor(deck))).ToArray();
        foreach (var item in selected) { source.Items.Remove(item); destination.Items.Add(item); }
        RecalculateMusicDeckOrder(deck); RecalculateMusicDeckOrder(Opposite(deck));
        MarkMusicDeckQueuesDirty();
        UpdateMusicAutomationStatus($"Moved {selected.Length} track(s) to {DeckName(Opposite(deck))}");
    }

    private async Task AddPlaylistToFolderAsync(MusicQueueItem[] items)
    {
        if (items.Length == 0) return;
        var folders = await _library.GetVirtualFoldersAsync();
        if (folders.Count == 0) { MessageBox.Show(this, "Create a virtual folder in Library first, then try again.", "Add to virtual folder"); return; }
        var picker = new VirtualFolderPickerDialog(folders, null, items.Length) { Owner = this };
        if (picker.ShowDialog() != true || picker.SelectedFolderId is not long folder) return;
        var added = 0;
        foreach (var item in items)
        {
            var song = await _library.FindByFilePathAsync(item.FilePath);
            if (song is null)
            {
                await _libraryImporter.IndexFileAsync(item.FilePath, LibraryImportMode.Music);
                song = await _library.FindByFilePathAsync(item.FilePath);
            }
            if (song is null) continue;
            await _library.AddSongToVirtualFolderAsync(folder, song.Id); added++;
        }
        SearchStatus.Text = $"Added {added} of {items.Length} track(s) to virtual folder";
    }

    private async Task LocateMusicFileAsync(MusicQueueItem? item)
    {
        if (item is null) return;
        if (await Task.Run(() => File.Exists(item.FilePath))) { MessageBox.Show(this, "This track's file is already present.", "Locate missing file"); return; }
        if (IsMusicFileLoaded(item.FilePath)) { MessageBox.Show(this, "Load another track on players using this file first.", "Track is loaded"); return; }
        var choose = new Microsoft.Win32.OpenFileDialog { Title = "Locate the missing track — update playlist entries", CheckFileExists = true, Filter = "Media files|*.mp3;*.wav;*.flac;*.m4a;*.wma;*.ogg;*.mp4;*.mkv;*.avi;*.wmv|All files|*.*" };
        if (choose.ShowDialog(this) != true) return;
        await _libraryImporter.IndexFileAsync(choose.FileName, LibraryImportMode.Music);
        var song = await _library.FindByFilePathAsync(choose.FileName);
        if (song is null) throw new InvalidOperationException("The selected file could not be indexed as music.");
        if (item.IsFavourite) await Task.Run(() => MusicFavourites.Default.Set(new[] { choose.FileName }, true));
        foreach (var deck in new[] { MusicDeckId.Deck1, MusicDeckId.Deck2 })
        {
            var list = PlaylistFor(deck);
            for (var i = 0; i < list.Items.Count; i++)
                if (list.Items[i] is MusicQueueItem row && string.Equals(row.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase))
                { list.Items.RemoveAt(i); list.Items.Insert(i, CreateMusicQueueItem(song)); }
            RecalculateMusicDeckOrder(deck);
        }
        MarkMusicDeckQueuesDirty();
        SearchStatus.Text = "Playlist entries now use " + choose.FileName;
    }

    private async Task ShowMusicInfoAsync(MusicQueueItem? item)
    {
        if (item is null) return;
        var text = $"Artist: {item.DisplayArtist}\nTitle: {item.DisplayTitle}\nFile: {item.FilePath}\nSaved tempo: {LoadTempo(item.FilePath):P0}\nFavourite: {(item.IsFavourite ? "Yes" : "No")}";
        var media = await Task.Run(() =>
        {
            try { using var file = TagLib.File.Create(item.FilePath); return $"\nAlbum: {file.Tag.Album}\nDuration: {file.Properties.Duration}\nBitrate: {file.Properties.AudioBitrate} kbps\nSample rate: {file.Properties.AudioSampleRate} Hz"; }
            catch (Exception ex) { return "\nMedia information unavailable: " + ex.Message; }
        });
        MessageBox.Show(this, text + media, "Track information");
    }
}
