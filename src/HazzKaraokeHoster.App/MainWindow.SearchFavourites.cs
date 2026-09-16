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
            SearchGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }
        row.Focus();
    }

    private void SearchContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        var action = menu.Items.OfType<MenuItem>().FirstOrDefault();
        var selected = SearchGrid.SelectedItems.OfType<HazzKaraokeHoster.Core.Models.SongRecord>().ToArray();
        if (action is null) return;
        action.IsEnabled = selected.Length > 0;
        action.Header = selected.Length > 0 && selected.All(x => MusicFavourites.Default.Contains(x.FilePath))
            ? "UNMARK FAVOURITE"
            : "MARK AS FAVOURITE";
    }

    private async void SearchFavourite_Click(object sender, RoutedEventArgs e)
    {
        var selected = SearchGrid.SelectedItems.OfType<HazzKaraokeHoster.Core.Models.SongRecord>().ToArray();
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
}
