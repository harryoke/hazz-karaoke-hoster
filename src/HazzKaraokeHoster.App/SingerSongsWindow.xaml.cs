using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;

namespace HazzKaraokeHoster.App;

public partial class SingerSongsWindow : Window
{
    private readonly SingerQueueEntry _singer;
    private readonly ISingerRepository _singers;
    private readonly ILibraryRepository _library;
    private Point _dragStart;
    private SingerSongEntry? _dragSong;
    private Point _historyDragStart;
    private SingerHistoryEntry? _dragHistory;
    private CancellationTokenSource? _historySearchCts;
    private readonly Func<long?, string, string, Task<bool>>? _confirmDuplicateRequest;

    public SingerSongsWindow(SingerQueueEntry singer, ISingerRepository singers, ILibraryRepository library,
        Func<long?, string, string, Task<bool>>? confirmDuplicateRequest = null)
    {
        InitializeComponent();
        _singer = singer;
        _singers = singers;
        _library = library;
        _confirmDuplicateRequest = confirmDuplicateRequest;
        SingerTitle.Text = singer.SingerName;
        SongGrid.ItemsSource = singer.Songs;
        Loaded += async (_, _) => await LoadHistoryAsync();
    }

    public long? SingerId => _singer.SingerId;

    private SingerSongEntry? SelectedSong => SongGrid.SelectedItem as SingerSongEntry;

    public Task RefreshHistoryAsync() => LoadHistoryAsync();

    private async Task LoadHistoryAsync()
    {
        if (_singer.SingerId is not long singerId)
        {
            HistoryGrid.ItemsSource = null;
            return;
        }
        try
        {
            var query = HistorySearchBox?.Text?.Trim() ?? string.Empty;
            HistoryGrid.ItemsSource = query.Length == 0
                ? await _singers.GetHistoryAsync(singerId, 5000)
                : await _singers.SearchHistoryAsync(singerId, query, 5000);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not load history: " + ex.Message;
        }
    }

    private void SongUp_Click(object sender, RoutedEventArgs e)
    {
        var i = SongGrid.SelectedIndex;
        if (i <= 0) return;
        _singer.Songs.Move(i, i - 1);
        SongGrid.SelectedIndex = i - 1;
    }

    private void SongDown_Click(object sender, RoutedEventArgs e)
    {
        var i = SongGrid.SelectedIndex;
        if (i < 0 || i >= _singer.Songs.Count - 1) return;
        _singer.Songs.Move(i, i + 1);
        SongGrid.SelectedIndex = i + 1;
    }

    private async void KeyDown_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSong is not { } song) return;
        song.KeyChange--;
        await SaveTrackDefaultsAsync(song);
    }

    private async void KeyUp_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSong is not { } song) return;
        song.KeyChange++;
        await SaveTrackDefaultsAsync(song);
    }

    private async void KeyReset_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSong is not { } song) return;
        song.KeyChange = 0;
        await SaveTrackDefaultsAsync(song);
    }

    private async void SyncEarlier_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSong is not { } song) return;
        song.CdgSyncSeconds -= 0.25;
        await SaveTrackDefaultsAsync(song);
    }

    private async void SyncLater_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSong is not { } song) return;
        song.CdgSyncSeconds += 0.25;
        await SaveTrackDefaultsAsync(song);
    }

    private async void SyncReset_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSong is not { } song) return;
        song.CdgSyncSeconds = 0;
        await SaveTrackDefaultsAsync(song);
    }

    private async Task SaveTrackDefaultsAsync(SingerSongEntry song)
    {
        _singer.RefreshSongSummary();
        if (song.SongId is not long songId) return;
        try
        {
            await _library.SaveTrackPreferencesAsync(songId, song.KeyChange, song.CdgSyncSeconds);
            StatusText.Text = $"Saved {song.SongTitle}: key {song.KeyChange:+0;-0;0}, sync {song.CdgSyncSeconds:+0.00;-0.00;0.00}s";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not save track defaults: " + ex.Message;
        }
    }

    private void RemoveSong_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSong is not { } song) return;
        _singer.Songs.Remove(song);
    }

    private void SongGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(SongGrid);
        _dragSong = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as SingerSongEntry;
    }

    private void SongGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSong is null) return;
        var p = e.GetPosition(SongGrid);
        if (Math.Abs(p.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var song = _dragSong;
        DragDrop.DoDragDrop(SongGrid, new DataObject(typeof(SingerSongEntry), song), DragDropEffects.Move);
        _dragSong = null;
    }

    private void SongGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(SingerHistoryEntry)))
            e.Effects = DragDropEffects.Copy;
        else if (e.Data.GetDataPresent(typeof(SingerSongEntry)))
            e.Effects = DragDropEffects.Move;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void SongGrid_Drop(object sender, DragEventArgs e)
    {
        var hit = SongGrid.InputHitTest(e.GetPosition(SongGrid)) as DependencyObject;
        var row = FindVisualParent<DataGridRow>(hit);
        var target = row?.Item as SingerSongEntry;
        var index = _singer.Songs.Count;
        if (target is not null)
        {
            index = _singer.Songs.IndexOf(target);
            if (row is not null && e.GetPosition(row).Y > row.ActualHeight / 2.0) index++;
        }
        index = Math.Clamp(index, 0, _singer.Songs.Count);

        if (e.Data.GetData(typeof(SingerSongEntry)) is SingerSongEntry dragged && _singer.Songs.Contains(dragged))
        {
            if (ReferenceEquals(target, dragged)) return;
            var oldIndex = _singer.Songs.IndexOf(dragged);
            _singer.Songs.Remove(dragged);
            if (oldIndex >= 0 && oldIndex < index) index--;
            index = Math.Clamp(index, 0, _singer.Songs.Count);
            _singer.Songs.Insert(index, dragged);
            SongGrid.SelectedItem = dragged;
            SongGrid.ScrollIntoView(dragged);
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(typeof(SingerHistoryEntry)) is SingerHistoryEntry history)
        {
            await RequeueHistoryAsync(history, index);
            e.Handled = true;
        }
    }

    private void HistoryGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _historyDragStart = e.GetPosition(HistoryGrid);
        _dragHistory = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as SingerHistoryEntry;
    }

    private void HistoryGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragHistory is null) return;
        var p = e.GetPosition(HistoryGrid);
        if (Math.Abs(p.X - _historyDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _historyDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var history = _dragHistory;
        DragDrop.DoDragDrop(HistoryGrid, new DataObject(typeof(SingerHistoryEntry), history), DragDropEffects.Copy);
        _dragHistory = null;
    }

    private async void HistoryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryGrid.SelectedItem is SingerHistoryEntry history)
            await RequeueHistoryAsync(history, _singer.Songs.Count);
    }

    private async void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _historySearchCts?.Cancel();
        _historySearchCts?.Dispose();
        _historySearchCts = new CancellationTokenSource();
        var token = _historySearchCts.Token;
        try
        {
            await Task.Delay(140, token);
            await LoadHistoryAsync();
            if (!token.IsCancellationRequested)
            {
                var count = HistoryGrid.Items.Count;
                StatusText.Text = string.IsNullOrWhiteSpace(HistorySearchBox.Text)
                    ? "Singer history loaded."
                    : $"History filter: {count:N0} matching song{(count == 1 ? string.Empty : "s")}.";
            }
        }
        catch (OperationCanceledException) { }
    }

    private void HistorySearchClear_Click(object sender, RoutedEventArgs e)
    {
        HistorySearchBox.Clear();
        HistorySearchBox.Focus();
    }

    private async Task RequeueHistoryAsync(SingerHistoryEntry history, int insertIndex)
    {
        try
        {
            SongRecord? song = null;
            if (!string.IsNullOrWhiteSpace(history.FilePath))
                song = await _library.FindByFilePathAsync(history.FilePath);

            if (song is null && (!string.IsNullOrWhiteSpace(history.Title) || !string.IsNullOrWhiteSpace(history.Artist)))
            {
                var matches = await _library.FindAlternativesAsync(history.Artist, history.Title, null, 50);
                song = matches.FirstOrDefault(x =>
                    string.Equals(x.Title, history.Title, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(history.Artist) || string.Equals(x.Artist, history.Artist, StringComparison.OrdinalIgnoreCase)))
                    ?? matches.FirstOrDefault();
            }

            var path = song?.FilePath ?? history.FilePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                StatusText.Text = $"Could not re-queue '{history.Title}' — no matching karaoke file is currently indexed.";
                return;
            }

            var duplicateSongId = song?.Id ?? history.SongId;
            var duplicateArtist = string.IsNullOrWhiteSpace(history.Artist) ? song?.Artist ?? string.Empty : history.Artist;
            var duplicateTitle = string.IsNullOrWhiteSpace(history.Title) ? song?.Title ?? string.Empty : history.Title;
            if (_confirmDuplicateRequest is not null &&
                !await _confirmDuplicateRequest(duplicateSongId, duplicateArtist, duplicateTitle))
            {
                StatusText.Text = "Re-queue cancelled after duplicate-song warning.";
                return;
            }

            var entry = new SingerSongEntry
            {
                SongId = song?.Id ?? history.SongId,
                SongTitle = string.IsNullOrWhiteSpace(history.Title) ? song?.Title ?? string.Empty : history.Title,
                Artist = string.IsNullOrWhiteSpace(history.Artist) ? song?.Artist ?? string.Empty : history.Artist,
                FilePath = path,
                KeyChange = history.KeyChange,
                CdgSyncSeconds = history.CdgSyncSeconds
            };
            insertIndex = Math.Clamp(insertIndex, 0, _singer.Songs.Count);
            _singer.Songs.Insert(insertIndex, entry);
            SongGrid.SelectedItem = entry;
            SongGrid.ScrollIntoView(entry);
            StatusText.Text = $"Re-queued from history: {entry.Artist} — {entry.SongTitle}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not re-queue history song: " + ex.Message;
        }
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

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
