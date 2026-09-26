using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;
public partial class MainWindow
{
    private bool _choosingAlternative;
    private async Task LoadQueuedChoiceAsync(SingerQueueEntry singer, SingerSongEntry song)
    {
        if (_karaokePlaying || _karaokePaused || _karaokePresentationActive || _callingNextSinger) return;
        _callingNextSinger = true;
        var sync = song.CdgSyncSeconds; var key = song.KeyChange;
        var previousSong = _activeSingerSong;
        _activeSingerSong = null;
        try
        {
            if (!await LoadKaraokeAsync(song.FilePath)) { _activeSingerSong = previousSong; return; }
            if (!_queue.Contains(singer) || !singer.Songs.Contains(song)) { _activeSingerSong = null; return; }
            ClearStartedSingerSongReference();
            _activeSinger = singer; _activeSingerSong = song; _activeSingerSongCheckedOut = false;
            song.CdgSyncSeconds = sync; song.KeyChange = key;
            _keyChange = key; KeyText.Text = $"{key:+0;-0;0}"; _pitchAudio.SetSemitones(key); _cdgTiming.Set(sync);
            SetKaraokeTempo(_pitchAudio.IsLoaded ? LoadTempo(song.FilePath, TempoSinger) : 1);
            KaraokeNowText.Text = $"{singer.SingerName} — {song.SongTitle} — {song.Artist}";
            QueueList.SelectedItem = singer;
            UpdateMusicAutomationStatus("Singer's chosen song loaded. Press PLAY when ready.");
        }
        finally { _callingNextSinger = false; }
    }

    private async void FindAlternative_Click(object sender, RoutedEventArgs e)
    {
        if (_choosingAlternative) return;
        if (_karaokePlaying || _karaokePaused || _karaokePresentationActive)
        { UpdateMusicAutomationStatus("Stop karaoke before replacing its version."); return; }
        var queued = _activeSingerSong;
        var current = _currentKaraokeRecord;
        var artist = queued?.Artist ?? current?.Artist ?? "";
        var title = queued?.SongTitle ?? current?.Title ?? "";
        var path = _karaokePackage?.SourcePath ?? queued?.FilePath ?? current?.FilePath;
        if (string.IsNullOrWhiteSpace(title)) { UpdateMusicAutomationStatus("Load an indexed karaoke song first."); return; }
        _choosingAlternative = true;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var window = new Window { Owner = this, Title = "Find Alternative — compare versions", Width = Math.Min(940,SystemParameters.WorkArea.Width-30), Height = Math.Min(570,SystemParameters.WorkArea.Height-40), MinWidth = 600, MinHeight = 350, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new DockPanel { Margin = new Thickness(14) }; window.Content = panel;
        var status = new TextBlock { Text = $"CURRENT: {artist} — {title}\n{current?.Manufacturer}  {current?.DiscId}  {current?.DurationText}\n{path}\nSearching…", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) };
        DockPanel.SetDock(status, Dock.Top); panel.Children.Add(status);
        var replace = new Button { Content = "REPLACE LOADED / QUEUED VERSION (DO NOT PLAY)", IsEnabled = false, MinHeight = 36 };
        DockPanel.SetDock(replace, Dock.Bottom); panel.Children.Add(replace);
        var grid = new DataGrid { EnableRowVirtualization = true, EnableColumnVirtualization = true, AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single };
        foreach (var col in new[] { ("Artist","Artist"), ("Title","Title"), ("Manufacturer","Manufacturer"), ("Disc","DiscId"), ("Duration","DurationText"), ("File","FilePath") })
            grid.Columns.Add(new DataGridTextColumn { Header = col.Item1, Binding = new Binding(col.Item2), Width = new DataGridLength(1,DataGridLengthUnitType.Star), MinWidth = 85 });
        panel.Children.Add(grid);
        window.Closing += (_, args) => { if (_callingNextSinger) args.Cancel = true; };
        window.Closed += (_,_) => cts.Cancel();
        window.Loaded += async (_,_) =>
        {
            try
            {
                var matches = await Task.Run(() => _library.FindAlternativesAsync(artist, title, path, 300, cts.Token), cts.Token);
                if (cts.IsCancellationRequested) return;
                grid.ItemsSource = matches;
                status.Text = $"CURRENT: {artist} — {title}\n{current?.Manufacturer}  {current?.DiscId}  {current?.DurationText}\n{path}\n{matches.Count} suggestions. Check the recording: reordered names and swapped artist/title are included. Key, sync and rotation are retained.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        grid.SelectionChanged += (_,_) => replace.IsEnabled = grid.SelectedItem is SongRecord;
        replace.Click += async (_,_) =>
        {
            if (grid.SelectedItem is not SongRecord candidate || _karaokePlaying || _karaokePaused || _karaokePresentationActive) return;
            replace.IsEnabled = false;
            var key = queued?.KeyChange ?? _keyChange; var sync = queued?.CdgSyncSeconds ?? _cdgTiming.OffsetSeconds;
            _callingNextSinger = true;
            try
            {
                if (!File.Exists(candidate.FilePath)) { status.Text = "That file is missing. Choose another or use Library Health Centre."; return; }
                _activeSingerSong = null;
                if (!await LoadKaraokeAsync(candidate.FilePath)) return;
                _activeSingerSong = queued;
                _currentKaraokeRecord = candidate;
                if (queued is not null)
                {
                    queued.ReplaceRecording(candidate);
                    queued.KeyChange = key; queued.CdgSyncSeconds = sync;
                }
                _keyChange = key; KeyText.Text = $"{key:+0;-0;0}"; _pitchAudio.SetSemitones(key); _cdgTiming.Set(sync);
                KaraokeNowText.Text = $"{_activeSinger?.SingerName} — {candidate.Artist} — {candidate.Title}";
                SetKaraokeTempo(_pitchAudio.IsLoaded ? LoadTempo(candidate.FilePath, TempoSinger) : 1);
                MarkLiveShowStateDirty(); UpdateAudienceNext(); _callingNextSinger = false; window.Close();
            }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { _activeSingerSong = queued; _callingNextSinger = false; replace.IsEnabled = true; }
        };
        try { window.ShowDialog(); } finally { _choosingAlternative = false; }
    }
}
