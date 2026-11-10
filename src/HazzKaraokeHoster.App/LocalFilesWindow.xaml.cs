using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

public partial class LocalFilesWindow : Window
{
    private sealed class FolderState(string path) { public string Path { get; } = path; public bool Loaded, Loading; }
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _listing;
    private readonly DispatcherTimer _filterTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private string? _path;
    private Point _mouseDown;
    private bool _dragArmed, _closed;
    public LocalFilesWindow()
    {
        InitializeComponent();
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 30); Height = Math.Min(Height, SystemParameters.WorkArea.Height - 30);
        _filterTimer.Tick += (_, _) => { _filterTimer.Stop(); if (_path is not null) _ = OpenFolderAsync(_path); };
        Loaded += async (_, _) => await LoadDrivesAsync();
        Closed += (_, _) => { _closed = true; _filterTimer.Stop(); _lifetime.Cancel(); _listing?.Cancel(); };
    }
    private static TreeViewItem Node(string path, string? title = null)
    {
        var item = new TreeViewItem { Header = title ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), Tag = new FolderState(path), ToolTip = path };
        item.Items.Add(new TreeViewItem { Header = "Expand to browse…", IsEnabled = false });
        return item;
    }
    private async Task LoadDrivesAsync()
    {
        try
        {
            StatusText.Text = "Loading drive roots…";
            var drives = await Task.Run(LocalFileCatalog.Drives);
            if (_closed) return;
            FolderTree.Items.Clear();
            foreach (var drive in drives) FolderTree.Items.Add(Node(drive, drive));
            StatusText.Text = "Expand a drive to browse folders. Move this window beside the decks when dragging.";
        }
        catch (Exception ex) { if (!_closed) StatusText.Text = "Cannot list drives: " + ex.Message; }
    }
    private async void Folder_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem item || item.Tag is not FolderState state || state.Loaded || state.Loading) return;
        await LoadChildrenAsync(item, state);
    }
    private async Task LoadChildrenAsync(TreeViewItem item, FolderState state)
    {
        state.Loading = true;
        try
        {
            var token = _lifetime.Token;
            var folders = await Task.Run(() => LocalFileCatalog.Folders(state.Path, token), token).WaitAsync(token);
            if (_closed) return;
            item.Items.Clear(); foreach (var folder in folders) item.Items.Add(Node(folder)); state.Loaded = true;
            if (folders.Length == LocalFileCatalog.FolderLimit) item.Items.Add(new TreeViewItem { Header = "Folder limit reached — enter a path above", IsEnabled = false });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (!_closed) { StatusText.Text = "Cannot expand folder: " + ex.Message; item.Items.Clear(); item.Items.Add(new TreeViewItem { Header = "Unavailable — collapse/expand to retry", IsEnabled = false }); }
        }
        finally { state.Loading = false; }
    }
    private async void Folder_Selected(object sender, RoutedPropertyChangedEventArgs<object> e)
    { if (e.NewValue is TreeViewItem { Tag: FolderState state }) await OpenFolderAsync(state.Path); }
    internal async Task OpenFolderAsync(string path)
    {
        _listing?.Cancel(); var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _listing = request; var token = request.Token;
        try
        {
            string full = Path.GetFullPath(path.Trim()); string filter = FilterBox.Text;
            if (FolderTree.SelectedItem is TreeViewItem { Tag: FolderState selected } selectedItem &&
                !string.Equals(Path.TrimEndingDirectorySeparator(full), Path.TrimEndingDirectorySeparator(selected.Path), StringComparison.OrdinalIgnoreCase)) selectedItem.IsSelected = false;
            _path = full; AddressBox.Text = full; StatusText.Text = "Reading folder…"; FilesGrid.ItemsSource = null;
            var listing = await Task.Run(() => LocalFileCatalog.Files(full, filter, token), token).WaitAsync(token);
            if (_closed || !ReferenceEquals(_listing, request)) return;
            FilesGrid.ItemsSource = listing.Files;
            StatusText.Text = listing.Error ?? (listing.MatchingCount > listing.Files.Length
                ? $"Showing {listing.Files.Length:N0} of {listing.MatchingCount:N0} matching files. Type a filename filter to narrow this folder."
                : $"{listing.Files.Length:N0} supported file(s). Ctrl/Shift selects multiple; drag to a music playlist/player or the karaoke deck. Karaoke loads the first selected file.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
        { if (!_closed && ReferenceEquals(_listing, request)) StatusText.Text = "Cannot open folder: " + ex.Message; }
        finally { if (ReferenceEquals(_listing, request)) _listing = null; request.Dispose(); }
    }
    private void Filter_TextChanged(object sender, TextChangedEventArgs e) { if (_path is not null) { _filterTimer.Stop(); _filterTimer.Start(); } }
    private async void Go_Click(object sender, RoutedEventArgs e) => await OpenFolderAsync(AddressBox.Text);
    private async void Address_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; await OpenFolderAsync(AddressBox.Text); } }
    private async void Drives_Click(object sender, RoutedEventArgs e) => await LoadDrivesAsync();
    private async void Up_Click(object sender, RoutedEventArgs e)
    {
        try { if (_path is not null && Directory.GetParent(_path) is { } parent) await OpenFolderAsync(parent.FullName); }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { StatusText.Text = ex.Message; }
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (FolderTree.SelectedItem is TreeViewItem { Tag: FolderState state } item && !state.Loading) { state.Loaded = false; await LoadChildrenAsync(item, state); }
        if (_path is not null) await OpenFolderAsync(_path);
    }
    private void Files_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDown = e.GetPosition(FilesGrid);
        var row = ItemsControl.ContainerFromElement(FilesGrid, e.OriginalSource as DependencyObject) as DataGridRow;
        _dragArmed = row is not null;
        if (row?.IsSelected == true && FilesGrid.SelectedItems.Count > 1 && Keyboard.Modifiers == ModifierKeys.None) e.Handled = true;
    }
    private void Files_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragArmed || e.LeftButton != MouseButtonState.Pressed) return;
        var position = e.GetPosition(FilesGrid);
        if (Math.Abs(position.X - _mouseDown.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(position.Y - _mouseDown.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _dragArmed = false;
        var paths = FilesGrid.Items.OfType<LocalMediaFile>().Where(x => FilesGrid.SelectedItems.Contains(x)).Select(x => x.FullPath).ToArray();
        if (paths.Length == 0) return;
        if (paths.Length > LocalFileCatalog.DropLimit) { StatusText.Text = $"Select up to {LocalFileCatalog.DropLimit} files per drag."; return; }
        DragDrop.DoDragDrop(FilesGrid, new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Copy);
    }
}
