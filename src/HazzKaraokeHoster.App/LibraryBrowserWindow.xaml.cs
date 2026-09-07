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
        Loaded += async (_, _) => await ReloadAsync();
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
            ? "Karaoke: drag a track onto a singer in the main window, or select a singer there and use ADD TO SELECTED SINGER."
            : "Music: use ADD TO DECK 1 / DECK 2, or drag a track directly onto either music playlist in the main window.";
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
            var page = await _library.BrowseAsync(_mediaKind, FilterBox.Text, SelectedSort(), DescendingCheck.IsChecked == true, _offset, PageSize, token);
            if (token.IsCancellationRequested) return;
            _currentPage = page;
            _offset = page.Offset;
            LibraryGrid.ItemsSource = page.Items;
            PageBox.Text = page.PageNumber.ToString();
            PageStatusText.Text = $"of {page.PageCount:N0} • {page.TotalCount:N0} tracks";
            StatusText.Text = page.TotalCount == 0
                ? $"No {_mediaKind.ToLowerInvariant()} tracks match this filter."
                : $"Showing {page.Offset + 1:N0}–{page.Offset + page.Items.Count:N0} of {page.TotalCount:N0}. Only {PageSize:N0} rows are held in memory.";
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
        DragDrop.DoDragDrop(LibraryGrid, new DataObject(typeof(SongRecord), song), DragDropEffects.Copy);
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
