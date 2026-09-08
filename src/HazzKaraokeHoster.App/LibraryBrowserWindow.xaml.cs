using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace HazzKaraokeHoster.App;

public sealed record LibraryBrowserDeckRequest(int Deck, SongRecord Song);

public partial class LibraryBrowserWindow : Window
{
    private readonly ILibraryRepository _library;
    private readonly DispatcherTimer _filterTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private CancellationTokenSource? _loadCts;
    private string _mediaKind = "Karaoke";
    private int _offset;
    private const int PageSize = 500;
    private LibraryBrowsePage? _currentPage;
    private Point _dragStart;
    private SongRecord? _dragSong;
    private VirtualFolderNode? _selectedFolder;
    private bool _loadingFolders;
    private SongRecord[] _pendingFolderSongs = Array.Empty<SongRecord>();

    public event EventHandler<SongRecord>? AddToSingerRequested;
    public event EventHandler<LibraryBrowserDeckRequest>? AddToDeckRequested;

    public LibraryBrowserWindow(ILibraryRepository library)
    {
        InitializeComponent();
        _library = library;
        _filterTimer.Tick += async (_, _) =>
        {
            _filterTimer.Stop();
            _offset = 0;
            await ReloadAsync();
        };
        Loaded += async (_, _) =>
        {
            UpdateModeUi();
            await ReloadFoldersAsync();
            await ReloadAsync();
        };
        Closed += (_, _) =>
        {
            _filterTimer.Stop();
            _loadCts?.Cancel();
            _loadCts?.Dispose();
        };
    }

    private void KaraokeMode_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaKind == "Karaoke") return;
        _mediaKind = "Karaoke";
        _offset = 0;
        UpdateModeUi();
        _ = ReloadAsync();
    }

    private void MusicMode_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaKind == "Music") return;
        _mediaKind = "Music";
        _offset = 0;
        UpdateModeUi();
        _ = ReloadAsync();
    }

    private void UpdateModeUi()
    {
        KaraokeModeButton.Background = new SolidColorBrush(_mediaKind == "Karaoke" ? Color.FromRgb(0x39, 0x79, 0xA8) : Color.FromRgb(0x26, 0x31, 0x3D));
        MusicModeButton.Background = new SolidColorBrush(_mediaKind == "Music" ? Color.FromRgb(0x5E, 0x98, 0x5C) : Color.FromRgb(0x26, 0x31, 0x3D));
        var karaoke = _mediaKind == "Karaoke";
        AddSingerButton.Visibility = karaoke ? Visibility.Visible : Visibility.Collapsed;
        AddDeck1Button.Visibility = karaoke ? Visibility.Collapsed : Visibility.Visible;
        AddDeck2Button.Visibility = karaoke ? Visibility.Collapsed : Visibility.Visible;
        BrowserHint.Text = karaoke
            ? "Karaoke: drag onto a virtual folder or singer, or use the buttons below. Media files are never moved."
            : "Music: drag onto a virtual folder or music deck, or use the buttons below. Media files are never moved.";
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void SortChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _offset = 0;
        _ = ReloadAsync();
    }

    private void SortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _offset = 0;
        _ = ReloadAsync();
    }

    private string SelectedSort()
    {
        var text = (SortCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Artist";
        return text.Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private async Task ReloadAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;
        StatusText.Text = $"Loading {_mediaKind.ToLowerInvariant()} library...";
        try
        {
            var page = _selectedFolder is null
                ? await _library.BrowseAsync(_mediaKind, FilterBox.Text, SelectedSort(), DescendingCheck.IsChecked == true, _offset, PageSize, token)
                : await _library.BrowseVirtualFolderAsync(_selectedFolder.Id, _mediaKind, FilterBox.Text, SelectedSort(), DescendingCheck.IsChecked == true, _offset, PageSize, token);
            if (token.IsCancellationRequested) return;
            _currentPage = page;
            _offset = page.Offset;
            LibraryGrid.ItemsSource = page.Items;
            PageBox.Text = page.PageNumber.ToString();
            PageStatusText.Text = $"of {page.PageCount:N0} • {page.TotalCount:N0} tracks";
            var location = _selectedFolder is null ? "the complete library" : $"virtual folder '{_selectedFolder.Name}'";
            StatusText.Text = page.TotalCount == 0
                ? $"No {_mediaKind.ToLowerInvariant()} tracks match in {location}."
                : $"Showing {page.Offset + 1:N0}–{page.Offset + page.Items.Count:N0} of {page.TotalCount:N0} in {location}. Only {PageSize:N0} rows are held in memory.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusText.Text = "Library browser error: " + ex.Message;
        }
    }

    private void FirstPage_Click(object sender, RoutedEventArgs e) { _offset = 0; _ = ReloadAsync(); }
    private void PreviousPage_Click(object sender, RoutedEventArgs e) { _offset = Math.Max(0, _offset - PageSize); _ = ReloadAsync(); }
    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage?.HasNext != true) return;
        _offset += PageSize;
        _ = ReloadAsync();
    }
    private void LastPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage is null || _currentPage.TotalCount <= 0) return;
        _offset = (int)Math.Min(int.MaxValue, ((_currentPage.TotalCount - 1) / PageSize) * PageSize);
        _ = ReloadAsync();
    }
    private void GoPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage is null) return;
        if (!long.TryParse(PageBox.Text, out var page)) page = _currentPage.PageNumber;
        page = Math.Clamp(page, 1, _currentPage.PageCount);
        var wanted = (page - 1) * PageSize;
        _offset = (int)Math.Min(int.MaxValue, wanted);
        _ = ReloadAsync();
    }

    private void AddSinger_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaKind != "Karaoke" || LibraryGrid.SelectedItem is not SongRecord song) return;
        AddToSingerRequested?.Invoke(this, song);
    }

    private void AddDeck1_Click(object sender, RoutedEventArgs e) => RequestDeck(1);
    private void AddDeck2_Click(object sender, RoutedEventArgs e) => RequestDeck(2);
    private void RequestDeck(int deck)
    {
        if (_mediaKind != "Music" || LibraryGrid.SelectedItem is not SongRecord song) return;
        AddToDeckRequested?.Invoke(this, new LibraryBrowserDeckRequest(deck, song));
    }

    private void LibraryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LibraryGrid.SelectedItem is not SongRecord song) return;
        if (_mediaKind == "Karaoke") AddToSingerRequested?.Invoke(this, song);
        else AddToDeckRequested?.Invoke(this, new LibraryBrowserDeckRequest(1, song));
    }

    private void LibraryGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(LibraryGrid);
        _dragSong = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as SongRecord;
    }

    private void LibraryGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSong is null) return;
        var p = e.GetPosition(LibraryGrid);
        if (Math.Abs(p.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var song = _dragSong;
        var selected = LibraryGrid.SelectedItems.OfType<SongRecord>().ToArray();
        if (!selected.Contains(song)) selected = new[] { song };
        var data = new DataObject(typeof(SongRecord), song);
        data.SetData(typeof(SongRecord[]), selected);
        DragDrop.DoDragDrop(LibraryGrid, data, DragDropEffects.Copy);
        _dragSong = null;
    }

    private void LibraryGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta >= 0 || _currentPage?.HasNext != true) return;
        var scroll = FindVisualChild<ScrollViewer>(LibraryGrid);
        if (scroll is null || scroll.ScrollableHeight <= 0 || scroll.VerticalOffset < scroll.ScrollableHeight - 0.5) return;
        _offset += PageSize;
        _ = ReloadAsync();
        e.Handled = true;
    }

    private async Task ReloadFoldersAsync(long? selectFolderId = null)
    {
        var wanted = selectFolderId ?? _selectedFolder?.Id;
        _loadingFolders = true;
        try
        {
            var folders = await _library.GetVirtualFoldersAsync();
            var nodes = folders.ToDictionary(x => x.Id, x => new VirtualFolderNode
            {
                Id = x.Id,
                ParentId = x.ParentId,
                Name = x.Name,
                TrackCount = x.TrackCount,
                IsSelected = x.Id == wanted
            });
            var roots = new List<VirtualFolderNode>();
            foreach (var folder in folders)
            {
                var node = nodes[folder.Id];
                if (folder.ParentId is long parentId && nodes.TryGetValue(parentId, out var parent)) parent.Children.Add(node);
                else roots.Add(node);
            }
            FolderTree.ItemsSource = roots;
            _selectedFolder = wanted is long id && nodes.TryGetValue(id, out var selected) ? selected : null;
        }
        catch (Exception ex)
        {
            FolderHint.Text = "Could not load virtual folders: " + ex.Message;
        }
        finally { _loadingFolders = false; }
    }

    private async void AllLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFolder is not null) _selectedFolder.IsSelected = false;
        _selectedFolder = null;
        _offset = 0;
        await ReloadFoldersAsync();
        FolderHint.Text = "Showing all library tracks. Drag tracks onto a folder to categorise them.";
        await ReloadAsync();
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_loadingFolders || e.NewValue is not VirtualFolderNode folder) return;
        _pendingFolderSongs = LibraryGrid.SelectedItems.OfType<SongRecord>().ToArray();
        _selectedFolder = folder;
        _offset = 0;
        FolderHint.Text = $"Showing {folder.Name}. Drop tracks here to add links without moving files.";
        _ = ReloadAsync();
    }

    private async void NewFolder_Click(object sender, RoutedEventArgs e)
        => await CreateFolderAsync(null, "New Virtual Folder", "Folder name (for example: 80s, Rock or Jingles):");

    private async void NewSubfolder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFolder is null)
        {
            MessageBox.Show(this, "Select a parent folder first.", "New Subfolder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await CreateFolderAsync(_selectedFolder.Id, "New Virtual Subfolder", $"New folder inside {_selectedFolder.Name}:");
    }

    private async Task CreateFolderAsync(long? parentId, string title, string prompt)
    {
        var dialog = new VirtualFolderNameDialog(title, prompt) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var id = await _library.CreateVirtualFolderAsync(dialog.FolderName, parentId);
            await ReloadFoldersAsync(id);
            _offset = 0;
            await ReloadAsync();
            FolderHint.Text = $"Created virtual folder '{dialog.FolderName}'.";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFolder is null) return;
        var id = _selectedFolder.Id;
        var dialog = new VirtualFolderNameDialog("Rename Virtual Folder", "New folder name:", _selectedFolder.Name) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await _library.RenameVirtualFolderAsync(id, dialog.FolderName);
            await ReloadFoldersAsync(id);
            FolderHint.Text = $"Renamed folder to '{dialog.FolderName}'.";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Rename Virtual Folder", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void EmptyFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFolder is null) return;
        if (MessageBox.Show(this, $"Remove every track link directly inside '{_selectedFolder.Name}'?\n\nNo media files will be deleted.",
                "Empty Virtual Folder", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await _library.EmptyVirtualFolderAsync(_selectedFolder.Id);
        await ReloadFoldersAsync(_selectedFolder.Id);
        _offset = 0;
        await ReloadAsync();
    }

    private async void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFolder is null) return;
        var name = _selectedFolder.Name;
        if (MessageBox.Show(this, $"Delete virtual folder '{name}' and its subfolders?\n\nNo media files will be deleted.",
                "Delete Virtual Folder", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await _library.DeleteVirtualFolderAsync(_selectedFolder.Id);
        _selectedFolder = null;
        await ReloadFoldersAsync();
        _offset = 0;
        await ReloadAsync();
        FolderHint.Text = $"Deleted virtual folder '{name}'. The media files were not changed.";
    }

    private async void AddSelectedToFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = LibraryGrid.SelectedItems.OfType<SongRecord>().ToArray();
        if (selected.Length == 0 && LibraryGrid.SelectedItem is SongRecord one) selected = new[] { one };
        if (selected.Length == 0) selected = _pendingFolderSongs;
        if (selected.Length == 0)
        {
            MessageBox.Show(this, "Select one or more tracks first.", "Add to Virtual Folder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var folders = await _library.GetVirtualFoldersAsync();
        if (folders.Count == 0)
        {
            MessageBox.Show(this, "Create a virtual folder first, then select the tracks you want to add.", "Add to Virtual Folder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var picker = new VirtualFolderPickerDialog(folders, _selectedFolder?.Id, selected.Length) { Owner = this };
        if (picker.ShowDialog() != true || picker.SelectedFolderId is not long folderId) return;
        foreach (var song in selected) await _library.AddSongToVirtualFolderAsync(folderId, song.Id);
        _pendingFolderSongs = Array.Empty<SongRecord>();
        var folderName = folders.First(x => x.Id == folderId).Name;
        FolderHint.Text = $"Added {selected.Length:N0} track(s) to {folderName}.";
        await ReloadFoldersAsync(_selectedFolder?.Id);
        await ReloadAsync();
    }

    private async void RemoveFromFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFolder is null)
        {
            MessageBox.Show(this, "Open a virtual folder before removing track links.", "Remove from Folder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var selected = LibraryGrid.SelectedItems.OfType<SongRecord>().ToArray();
        if (selected.Length == 0 && LibraryGrid.SelectedItem is SongRecord one) selected = new[] { one };
        foreach (var song in selected) await _library.RemoveSongFromVirtualFolderAsync(_selectedFolder.Id, song.Id);
        FolderHint.Text = $"Removed {selected.Length:N0} track(s) from {_selectedFolder.Name}. Media files were not changed.";
        await ReloadFoldersAsync(_selectedFolder.Id);
        await ReloadAsync();
    }

    private void FolderTree_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = (e.Data.GetDataPresent(typeof(SongRecord)) || e.Data.GetDataPresent(typeof(SongRecord[]))) && FindFolderAt(e.OriginalSource as DependencyObject) is not null
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void FolderTree_Drop(object sender, DragEventArgs e)
    {
        if (FindFolderAt(e.OriginalSource as DependencyObject) is not VirtualFolderNode folder) return;
        var songs = e.Data.GetData(typeof(SongRecord[])) as SongRecord[];
        if (songs is null || songs.Length == 0)
            songs = e.Data.GetData(typeof(SongRecord)) is SongRecord one ? new[] { one } : Array.Empty<SongRecord>();
        if (songs.Length == 0) return;
        try
        {
            foreach (var song in songs) await _library.AddSongToVirtualFolderAsync(folder.Id, song.Id);
            FolderHint.Text = songs.Length == 1
                ? $"Added {songs[0].Artist} - {songs[0].Title} to {folder.Name}."
                : $"Added {songs.Length:N0} tracks to {folder.Name}.";
            await ReloadFoldersAsync(folder.Id);
            if (_selectedFolder?.Id == folder.Id) await ReloadAsync();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Add to Virtual Folder", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private static VirtualFolderNode? FindFolderAt(DependencyObject? source)
        => FindVisualParent<TreeViewItem>(source)?.DataContext as VirtualFolderNode;

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T wanted) return wanted;
            var nested = FindVisualChild<T>(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T wanted) return wanted;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}
