using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private void SearchGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row is null) return;
        if (!row.IsSelected)
        {
            if (SearchGrid.SelectionMode == DataGridSelectionMode.Single)
            {
                SearchGrid.SelectedItem = row.Item;
                SearchGrid.CurrentItem = row.Item;
            }
            else
            {
                SearchGrid.SelectedItems.Clear();
                row.IsSelected = true;
            }
        }
        row.Focus();
    }

    private HazzKaraokeHoster.Core.Models.SongRecord[] GetContextSelectedSearchSongs()
    {
        var selected = SearchGrid.SelectedItems.OfType<HazzKaraokeHoster.Core.Models.SongRecord>().ToArray();
        if (selected.Length == 0 && SearchGrid.SelectedItem is HazzKaraokeHoster.Core.Models.SongRecord current)
            return new[] { current };
        return selected;
    }

    private void SearchContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        var selected = GetContextSelectedSearchSongs();
        var favourite = menu.Items.OfType<MenuItem>().FirstOrDefault(x => string.Equals(x.Tag?.ToString(), "Favourite", StringComparison.Ordinal));
        var remove = menu.Items.OfType<MenuItem>().FirstOrDefault(x => string.Equals(x.Tag?.ToString(), "RemoveDatabase", StringComparison.Ordinal));
        var removeMissing = menu.Items.OfType<MenuItem>().FirstOrDefault(x => string.Equals(x.Tag?.ToString(), "RemoveMissing", StringComparison.Ordinal));

        if (favourite is not null)
        {
            favourite.IsEnabled = selected.Length > 0;
            favourite.Header = selected.Length > 0 && selected.All(x => MusicFavourites.Default.Contains(x.FilePath))
                ? "UNMARK FAVOURITE"
                : "MARK AS FAVOURITE";
        }

        if (remove is not null)
        {
            remove.IsEnabled = selected.Length > 0;
            var oneMissing = selected.Length == 1 && !IndexedSongFileExists(selected[0]);
            remove.Header = oneMissing
                ? "REMOVE MISSING ENTRY FROM DATABASE"
                : selected.Length > 1
                    ? $"REMOVE {selected.Length:N0} SELECTED ENTRIES FROM DATABASE"
                    : "REMOVE FROM LIBRARY DATABASE";
        }

        if (removeMissing is not null)
        {
            var visible = GetVisibleSearchSongs();
            var missingCount = visible.Count(song => !IndexedSongFileExists(song));
            removeMissing.IsEnabled = missingCount > 0;
            removeMissing.Header = missingCount > 0
                ? $"REMOVE {missingCount:N0} MISSING RESULT(S) FROM DATABASE"
                : "NO MISSING RESULTS TO REMOVE";
        }
    }

    private async void SearchFavourite_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetContextSelectedSearchSongs();
        if (selected.Length == 0) return;
        var paths = selected.Select(x => x.FilePath).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mark = !selected.All(x => MusicFavourites.Default.Contains(x.FilePath));
        try
        {
            await Task.Run(() => MusicFavourites.Default.Set(paths, mark));
            SearchGrid.Items.Refresh();
            SearchStatus.Text = mark ? $"Marked {paths.Count:N0} search result(s) as favourite" : $"Removed {paths.Count:N0} search result(s) from favourites";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save favourites", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SearchRemoveLibrary_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetContextSelectedSearchSongs();
        if (selected.Length == 0) return;

        var label = selected.Length == 1
            ? $"{selected[0].Artist} - {selected[0].Title}"
            : $"{selected.Length:N0} selected library entries";
        var answer = MessageBox.Show(this,
            $"Remove {label} from the Hazz Karaoke Hoster library database?\n\n" +
            "This removes only the indexed database record. No ZIP, CDG, MP3 or video file will be deleted from disk.",
            "Remove From Library Database", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        await RemoveSongsFromLibraryDatabaseAsync(selected, "Removed from library database");
    }

    private async void SearchRemoveMissing_Click(object sender, RoutedEventArgs e)
    {
        var missing = GetVisibleSearchSongs().Where(song => !IndexedSongFileExists(song)).ToArray();
        if (missing.Length == 0)
        {
            SearchStatus.Text = "No missing files in the current search results";
            return;
        }

        var answer = MessageBox.Show(this,
            $"Remove {missing.Length:N0} missing search result(s) from the Hazz Karaoke Hoster library database?\n\n" +
            "Only stale database records are removed. No file on disk is deleted.",
            "Remove Missing Library Entries", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        await RemoveSongsFromLibraryDatabaseAsync(missing, "Removed missing library entries");
    }

    private async Task<bool> EnsureIndexedSongAvailableAsync(HazzKaraokeHoster.Core.Models.SongRecord song)
    {
        if (IndexedSongFileExists(song)) return true;

        BrokenMediaRegistry.Mark(song.FilePath, "Indexed karaoke file no longer exists");
        var answer = MessageBox.Show(this,
            "The karaoke file no longer exists at its indexed location:\n\n" + song.FilePath +
            "\n\nRemove this stale entry from the Hazz Karaoke Hoster library database now?" +
            "\n\nChoosing Yes removes only the database record. It does not delete any file from disk.",
            "Missing Karaoke File", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
            await RemoveSongsFromLibraryDatabaseAsync(new[] { song }, "Removed missing karaoke entry");

        return false;
    }

    private async Task RemoveSongsFromLibraryDatabaseAsync(
        IReadOnlyCollection<HazzKaraokeHoster.Core.Models.SongRecord> songs,
        string statusPrefix)
    {
        var valid = songs.Where(song => song.Id > 0).GroupBy(song => song.Id).Select(group => group.First()).ToArray();
        if (valid.Length == 0) return;

        try
        {
            var removed = await _library.DeleteSongsAsync(valid.Select(song => song.Id), _lifetime.Token);
            _searchCts?.Cancel(); // An in-flight duration refresh must not restore deleted results.
            RemoveSongsFromVisibleSearch(valid.Select(song => song.Id));
            SearchStatus.Text = $"{statusPrefix}: {removed:N0}";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            App.WriteDiagnostic("REMOVE LIBRARY ENTRY", ex.ToString());
            MessageBox.Show(this, ex.Message, "Could Not Remove Library Entry", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoveSongsFromVisibleSearch(IEnumerable<long> songIds)
    {
        var ids = songIds.Where(id => id > 0).ToHashSet();
        if (ids.Count == 0) return;
        var visible = GetVisibleSearchSongs();
        if (visible.Count == 0) return;
        SearchGrid.ItemsSource = visible.Where(song => !ids.Contains(song.Id)).ToArray();
    }

    private IReadOnlyList<HazzKaraokeHoster.Core.Models.SongRecord> GetVisibleSearchSongs()
        => SearchGrid.ItemsSource is IEnumerable<HazzKaraokeHoster.Core.Models.SongRecord> rows
            ? rows.ToArray()
            : Array.Empty<HazzKaraokeHoster.Core.Models.SongRecord>();

    private static bool IndexedSongFileExists(HazzKaraokeHoster.Core.Models.SongRecord song)
        => !string.IsNullOrWhiteSpace(song.FilePath) && File.Exists(song.FilePath);

}
