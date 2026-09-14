using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private void SetupMusicContextMenus()
    {
        foreach (var list in new[] { DeckAPlaylist, DeckBPlaylist })
        {
            list.PreviewMouseRightButtonDown += (_, e) =>
            {
                var row = ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) as ListBoxItem;
                if (row is null) return;
                if (!row.IsSelected) { list.SelectedItems.Clear(); row.IsSelected = true; }
                row.Focus();
            };
            var menu = new ContextMenu();
            var deck = ReferenceEquals(list, DeckAPlaylist) ? MusicDeckId.Deck1 : MusicDeckId.Deck2;
            MenuItem Action(string label, Func<Task> run)
            {
                var action = new MenuItem { Header = label };
                action.Click += async (_, _) => { try { await run(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, label); } };
                menu.Items.Add(action); return action;
            }
            var next = Action("Play next on this deck", () => { QueueSelectedNext(deck); return Task.CompletedTask; });
            var move = Action("Move to other deck", () => { MoveSelectedToOtherDeck(deck); return Task.CompletedTask; });
            var folder = Action("Add to virtual folder…", () => AddPlaylistToFolderAsync(list.SelectedItems.OfType<MusicQueueItem>().ToArray()));
            menu.Items.Add(new Separator());
            var edit = new MenuItem { Header = "Edit MP3 tags…" };
            var favourite = new MenuItem { Header = "Mark as favourite", Icon = new TextBlock { Text = "★", Foreground = Brushes.Gold } };
            menu.Items.Add(edit); menu.Items.Add(favourite);
            var show = Action("Show in folder", () =>
            {
                if (list.SelectedItem is MusicQueueItem row)
                {
                    var info = new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                    info.Arguments = "/select,\"" + row.FilePath.Replace("\"", "") + "\"";
                    System.Diagnostics.Process.Start(info);
                }
                return Task.CompletedTask;
            });
            var locate = Action("Locate missing file…", () => LocateMusicFileAsync(list.SelectedItem as MusicQueueItem));
            var details = Action("Track information…", () => ShowMusicInfoAsync(list.SelectedItem as MusicQueueItem));
            menu.Opened += (_, _) =>
            {
                var selected = list.SelectedItems.OfType<MusicQueueItem>().ToArray();
                edit.IsEnabled = selected.Length == 1 && Path.GetExtension(selected[0].FilePath).Equals(".mp3", StringComparison.OrdinalIgnoreCase);
                favourite.IsEnabled = selected.Length > 0;
                favourite.Header = selected.Length > 0 && selected.All(x => x.IsFavourite) ? "Unmark favourite" : "Mark as favourite";
                next.IsEnabled = selected.Any(x => !ReferenceEquals(x, CurrentMusicItemFor(deck))) && !(_singleDeckMode && deck == MusicDeckId.Deck2);
                move.IsEnabled = selected.Any(x => !ReferenceEquals(x, CurrentMusicItemFor(deck)));
                move.Header = "Move to " + DeckName(Opposite(deck));
                folder.IsEnabled = selected.Length > 0;
                show.IsEnabled = locate.IsEnabled = details.IsEnabled = selected.Length == 1;
            };
            edit.Click += async (_, _) => { if (list.SelectedItem is MusicQueueItem item) await EditMusicTagsAsync(item); };
            favourite.Click += async (_, _) =>
            {
                var selected = list.SelectedItems.OfType<MusicQueueItem>().ToArray();
                if (selected.Length == 0) return;
                var mark = !selected.All(x => x.IsFavourite);
                var paths = selected.Select(x => x.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                try
                {
                    await Task.Run(() => MusicFavourites.Default.Set(paths, mark));
                    foreach (var row in DeckAPlaylist.Items.OfType<MusicQueueItem>().Concat(DeckBPlaylist.Items.OfType<MusicQueueItem>()))
                        if (paths.Contains(row.FilePath)) row.IsFavourite = mark;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save favourites"); }
            };
            list.ContextMenu = menu;
        }
    }

    private bool IsMusicFileLoaded(string path) => new[] { DeckAMedia, DeckBMedia, QuickMusicMedia, StandbyMusicMedia }
        .Any(m => string.Equals(m.Source?.LocalPath, path, StringComparison.OrdinalIgnoreCase))
        || string.Equals(_karaokePackage?.PlaybackPath, path, StringComparison.OrdinalIgnoreCase);

    private async Task EditMusicTagsAsync(MusicQueueItem item)
    {
        var path = item.FilePath;
        if (IsMusicFileLoaded(path))
        {
            MessageBox.Show(this, "Load a different song on any player using this file before editing its tags, including paused or cued players.", "MP3 is in use");
            return;
        }
        try
        {
            var tags = await Task.Run(() => { using var file = TagLib.File.Create(path, TagLib.ReadStyle.None); return (Artist: string.Join(" / ", file.Tag.Performers), Title: file.Tag.Title ?? "", Album: file.Tag.Album ?? ""); });
            var dialog = new Window { Owner = this, Title = "Edit MP3 tags", Width = 540, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = new SolidColorBrush(Color.FromRgb(16,24,32)), Foreground = Brushes.White };
            var panel = new StackPanel { Margin = new Thickness(18) };
            dialog.Content = panel;
            panel.Children.Add(new TextBlock { Text = path, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) });
            TextBox Field(string name, string value) { panel.Children.Add(new TextBlock { Text = name }); var box = new TextBox { Text = value, Margin = new Thickness(0,4,0,10) }; panel.Children.Add(box); return box; }
            var artist = Field("Artist", tags.Artist); var title = Field("Song title", tags.Title); var album = Field("Album", tags.Album);
            panel.Children.Add(new TextBlock { Text = "Save changes writes these tags to this MP3 file. Its filename stays the same.", TextWrapping = TextWrapping.Wrap });
            var save = new Button { Content = "SAVE CHANGES", Margin = new Thickness(0,12,0,0) };
            panel.Children.Add(save);
            save.Click += async (_, _) =>
            {
                if (IsMusicFileLoaded(path)) { MessageBox.Show(dialog, "This file is now loaded in a player. Load another song before saving.", "MP3 is in use"); return; }
                var newArtist = artist.Text.Trim(); var newTitle = title.Text.Trim(); var newAlbum = album.Text.Trim();
                save.IsEnabled = false;
                try
                {
                    await HazzKaraokeHoster.Core.Mp3Metadata.SaveAsync(path, newArtist, newTitle, newAlbum);
                    foreach (var row in DeckAPlaylist.Items.OfType<MusicQueueItem>().Concat(DeckBPlaylist.Items.OfType<MusicQueueItem>()).Where(x => string.Equals(x.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        var fallback = MusicQueueItem.FromPath(path); row.Artist = fallback.Artist; row.Title = fallback.Title;
                        await RefreshMusicTagsSafeAsync(row);
                    }
                    await _libraryImporter.IndexFileAsync(path, HazzKaraokeHoster.Core.Models.LibraryImportMode.Music);
                    dialog.Close();
                }
                catch (Exception ex) { MessageBox.Show(dialog, "Could not complete tag update: " + ex.Message, "MP3 tag update"); save.IsEnabled = true; }
            };
            dialog.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not read MP3 tags"); }
    }
}
