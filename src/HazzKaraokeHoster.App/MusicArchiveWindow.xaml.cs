using System.Windows;
using System.Windows.Controls;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class MusicArchiveWindow : Window
{
    private readonly IMusicPlaylistRepository _repository;
    private readonly CancellationTokenSource _lifetime = new();
    private IReadOnlyList<MusicPlaylistItem> _currentItems = Array.Empty<MusicPlaylistItem>();
    private IReadOnlyList<MusicHistoryEntry> _historyItems = Array.Empty<MusicHistoryEntry>();
    private int? _preferredDeck;
    public event EventHandler<(int Deck, IReadOnlyList<string> Paths)>? LoadToDeckRequested;

    public MusicArchiveWindow(IMusicPlaylistRepository repository, int? preferredDeck = null)
    {
        InitializeComponent();
        _repository = repository;
        _preferredDeck = preferredDeck;
        Loaded += async (_, _) =>
        {
            SetPreferredDeck(_preferredDeck);
            await RefreshAsync();
        };
        Closed += (_, _) => _lifetime.Cancel();
    }

    public Task RefreshFromHostAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            PlaylistGrid.ItemsSource = await _repository.GetPlaylistsAsync(cancellationToken: _lifetime.Token);
            _historyItems = await _repository.GetMusicHistoryAsync(100000, _lifetime.Token);
            HistoryGrid.ItemsSource = _historyItems;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { StatusText.Text = "Could not load music archive: " + ex.Message; }
    }

    private async void PlaylistGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlaylistGrid.SelectedItem is not MusicPlaylistSummary playlist) { _currentItems = Array.Empty<MusicPlaylistItem>(); PlaylistItemsGrid.ItemsSource = null; return; }
        try
        {
            _currentItems = await _repository.GetPlaylistItemsAsync(playlist.Id, _lifetime.Token);
            PlaylistItemsGrid.ItemsSource = _currentItems;
            StatusText.Text = $"{playlist.Name} — {_currentItems.Count:N0} tracks";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    public void SetPreferredDeck(int? deck)
    {
        _preferredDeck = deck is 1 or 2 ? deck : null;
        if (StatusText is null) return;
        StatusText.Text = _preferredDeck is int d
            ? $"Opened from Deck {d}. Select a saved/imported playlist, or use FULL HISTORY, then load it into that deck."
            : "Hazz stores every played music track by date/time. Imported BPM Studio playlists/history are also preserved.";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void LoadDeck1_Click(object sender, RoutedEventArgs e) => LoadSelected(1);
    private void LoadDeck2_Click(object sender, RoutedEventArgs e) => LoadSelected(2);
    private void LoadFullHistoryDeck1_Click(object sender, RoutedEventArgs e) => LoadFullHistory(1);
    private void LoadFullHistoryDeck2_Click(object sender, RoutedEventArgs e) => LoadFullHistory(2);

    private void LoadFullHistory(int deck)
    {
        var paths = _historyItems.Select(x => x.FilePath).Where(File.Exists).ToArray();
        if (paths.Length == 0)
        {
            MessageBox.Show(this, "No playable files from Music History are currently available.", "Music History", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        LoadToDeckRequested?.Invoke(this, (deck, paths));
        StatusText.Text = $"Loaded full music history: {paths.Length:N0} playable track(s) to Deck {deck}.";
    }

    private void LoadSelected(int deck)
    {
        IReadOnlyList<string> paths;
        if (Tabs.SelectedIndex == 0)
        {
            paths = _currentItems.Select(x => x.FilePath).Where(File.Exists).ToArray();
        }
        else
        {
            paths = HistoryGrid.SelectedItems.Cast<MusicHistoryEntry>()
                .Select(x => x.FilePath)
                .Where(File.Exists)
                .ToArray();

            if (paths.Count == 0 && HistoryGrid.SelectedItem is MusicHistoryEntry selectedHistory)
            {
                if (File.Exists(selectedHistory.FilePath))
                    paths = new[] { selectedHistory.FilePath };
            }
        }
        if (paths.Count == 0)
        {
            MessageBox.Show(this, "No playable files are selected/found. If the BPM Studio tracks moved to another drive, rescan or remap the music library first.", "Music Archive", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        LoadToDeckRequested?.Invoke(this, (deck, paths));
        StatusText.Text = $"Added {paths.Count:N0} track(s) to Deck {deck}.";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
