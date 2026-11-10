using System.Windows;
using System.Windows.Controls;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private LocalFilesWindow? _localFiles;
    private void OpenLocalFiles_Click(object sender, RoutedEventArgs e)
    {
        if (_localFiles is not null) { if (_localFiles.WindowState == WindowState.Minimized) _localFiles.WindowState = WindowState.Normal; _localFiles.Activate(); return; }
        // Independent, modeless window: the host can be brought in front during a drag.
        _localFiles = new LocalFilesWindow(); _localFiles.Closed += (_, _) => _localFiles = null; _localFiles.Show();
    }
    private void InitializeLocalFileDrops()
    {
        foreach (var pair in new[] { (Panel: MusicDeckAPanel, Deck: MusicDeckId.Deck1), (Panel: MusicDeckBPanel, Deck: MusicDeckId.Deck2) })
        {
            pair.Panel.AllowDrop = true;
            pair.Panel.DragOver += (_, e) =>
            {
                if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
                e.Effects = files.Length is > 0 and <= LocalFileCatalog.DropLimit && files.Any(p => LocalFileCatalog.Supported(p, musicOnly: true)) ? DragDropEffects.Copy : DragDropEffects.None;
                e.Handled = true;
            };
            pair.Panel.Drop += async (_, e) =>
            {
                if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
                e.Handled = true; await AddLocalMusicFilesAsync(PlaylistFor(pair.Deck), pair.Deck, files, PlaylistFor(pair.Deck).Items.Count);
            };
        }
        Closed += (_, _) => _localFiles?.Close();
    }
    private async Task AddLocalMusicFilesAsync(ListBox target, MusicDeckId deck, string[] paths, int requestedIndex)
    {
        if (paths.Length is 0 or > LocalFileCatalog.DropLimit)
        { UpdateMusicAutomationStatus($"Choose between 1 and {LocalFileCatalog.DropLimit} files per drop"); return; }
        try
        {
            var token = _lifetime.Token;
            var files = await Task.Run(() => LocalFileCatalog.ExistingMusicFiles(paths, token), token);
            token.ThrowIfCancellationRequested();
            var current = CurrentMusicItemFor(deck);
            int liveIndex = current is null ? -1 : target.Items.IndexOf(current);
            int index = LocalFileCatalog.SafeInsertIndex(requestedIndex, target.Items.Count, liveIndex), added = 0;
            MusicQueueItem? first = null;
            foreach (var file in files)
            {
                var item = CreateMusicQueueItem(file); target.Items.Insert(index++, item); first ??= item; added++;
            }
            RecalculateMusicDeckOrder(deck); MarkMusicDeckQueuesDirty();
            if (first is not null) { target.SelectedItem = first; target.ScrollIntoView(first); }
            UpdateMusicAutomationStatus($"{DeckName(deck)}: added {added:N0} file(s) • skipped {paths.Length - added:N0} unsupported or unavailable file(s). Playback unchanged.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { UpdateMusicAutomationStatus("Could not add local files: " + ex.Message); }
    }
}
