using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Diagnostics;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private string? _cuePath;
    private bool _cueReady;
    private DateTime _cueStarted;
    private int _cueGeneration;
    private bool _healthCheckRunning;
    private readonly DispatcherTimer _confidenceTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string ConfidenceBackupDirectory => Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!, "AutomaticBackups");

    private void InitializeConfidence()
    {
        LoadSoundRoutes();
        foreach (var media in new[] { DeckAMedia, DeckBMedia, StandbyMusicMedia })
        {
            media.MediaOpened += (sender, _) =>
            {
                if (ReferenceEquals(sender, StandbyMusicMedia))
                {
                    if (_cuePath is null || StandbyMusicMedia.Source?.LocalPath != _cuePath) return;
                    StandbyMusicMedia.Pause();
                    StandbyMusicMedia.Position = TimeSpan.Zero;
                    _cueReady = true;
                    NextCueStatus.Text = "Next track READY: " + Path.GetFileName(_cuePath);
                }
                else UpdateOpenedMusicMetadata(ReferenceEquals(sender, DeckAMedia) ? MusicDeckId.Deck1 : MusicDeckId.Deck2);
            };
            media.MediaEnded += (sender, _) =>
            {
                if (!ReferenceEquals(sender, StandbyMusicMedia))
                    HandleMusicDeckEnded(ReferenceEquals(sender, DeckAMedia) ? MusicDeckId.Deck1 : MusicDeckId.Deck2);
            };
            media.MediaFailed += (sender, e) =>
            {
                if (ReferenceEquals(sender, StandbyMusicMedia)) FailCue(e.ErrorException?.Message ?? "Cannot decode file");
                else HandleMusicDeckFailed(ReferenceEquals(sender, DeckAMedia) ? MusicDeckId.Deck1 : MusicDeckId.Deck2, e.ErrorException);
            };
        }
        _confidenceTimer.Tick += (_, _) => RunLiveTimerSafe("NEXT TRACK CHECK", UpdateNextCue);
        Loaded += async (_, _) => { _confidenceTimer.Start(); await AutomaticConfidenceBackupAsync(); };
        Closed += (_, _) => { _confidenceTimer.Stop(); _cueGeneration++; StandbyMusicMedia.Close(); };
    }

    private void FailCue(string reason)
    {
        _cueReady = false;
        _cueStarted = DateTime.MaxValue;
        StandbyMusicMedia.Close();
        BrokenMediaRegistry.Mark(_cuePath, reason);
        NextCueStatus.Text = "NEXT TRACK PROBLEM: " + Path.GetFileName(_cuePath) + " — " + reason;
    }

    private void UpdateNextCue()
    {
        if (_crossfadeActive) return;
        MusicQueueItem? item = null;
        if (_activeMusicDeck != MusicDeckId.None && !_quickSearchMusicActive && (_singleDeckMode || AutoCrossfadeCheck?.IsChecked == true))
        {
            var nextDeck = !_singleDeckMode && HasTracks(Opposite(_activeMusicDeck)) ? Opposite(_activeMusicDeck) : _activeMusicDeck;
            StandbyMusicMedia.OutputDeviceId = nextDeck == MusicDeckId.Deck1 ? _soundRoutes.Deck1 : _soundRoutes.Deck2;
            item = GetScheduledItem(nextDeck);
            if (ReferenceEquals(item, CurrentMusicItemFor(nextDeck))) item = null;
        }
        var path = item?.FilePath;
        if (string.Equals(path, _cuePath, StringComparison.OrdinalIgnoreCase))
        {
            if (path is not null && !_cueReady && _cueStarted != DateTime.MaxValue && DateTime.UtcNow - _cueStarted > TimeSpan.FromSeconds(15))
            {
                _cueStarted = DateTime.MaxValue;
                _cueGeneration++;
                StandbyMusicMedia.Close();
                NextCueStatus.Text = "Next track check timed out — unverified: " + Path.GetFileName(path);
            }
            return;
        }
        _cuePath = path;
        _cueReady = false;
        StandbyMusicMedia.Close();
        var generation = ++_cueGeneration;
        if (path is null) { NextCueStatus.Text = "Next track: idle"; return; }
        _cueStarted = DateTime.UtcNow;
        NextCueStatus.Text = "Checking next track: " + Path.GetFileName(path);
        _ = OpenCueAsync(path, generation);
    }

    private async Task OpenCueAsync(string path, int generation)
    {
        try
        {
            await Task.Run(() => { using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); if (file.Length == 0) throw new IOException("Empty file"); });
            if (generation != _cueGeneration) return;
            StandbyMusicMedia.IsMuted = true;
            StandbyMusicMedia.Volume = 0;
            StandbyMusicMedia.Source = new Uri(path, UriKind.Absolute);
            StandbyMusicMedia.Play();
        }
        catch (Exception ex) { if (generation == _cueGeneration) { _cueStarted = DateTime.MaxValue; FailCue(ex.Message); } }
    }

    private bool TakeReadyCue(MusicDeckId deck, string path)
    {
        if (!_cueReady || !string.Equals(path, _cuePath, StringComparison.OrdinalIgnoreCase)) return false;
        var previous = MediaFor(deck);
        previous.Stop();
        if (deck == MusicDeckId.Deck1) DeckAMedia = StandbyMusicMedia;
        else DeckBMedia = StandbyMusicMedia;
        StandbyMusicMedia = previous;
        StandbyMusicMedia.Close();
        var ready = MediaFor(deck);
        ready.Position = TimeSpan.Zero;
        ready.IsMuted = false;
        _cueGeneration++;
        _cuePath = null;
        _cueReady = false;
        return true;
    }

    private async Task AutomaticConfidenceBackupAsync()
    {
        try
        {
            await HazzKaraokeHoster.Data.CompressedBackup.CreateAsync(_db, ConfidenceBackupDirectory, _lifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { App.WriteDiagnostic("AUTOMATIC BACKUP", ex.ToString()); NextCueStatus.Text = "Automatic backup failed — use Backup Database before the show"; }
    }

    private void OpenConfidenceBackups_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(ConfidenceBackupDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", ConfidenceBackupDirectory) { UseShellExecute = true });
    }

    private async void PreShowHealthCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_healthCheckRunning) return;
        _healthCheckRunning = true;
        var paths = DeckAPlaylist.Items.Cast<MusicQueueItem>().Concat(DeckBPlaylist.Items.Cast<MusicQueueItem>()).Select(x => x.FilePath)
            .Concat(_queue.SelectMany(x => x.Songs).Select(x => x.FilePath)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        try
        {
            var problems = await Task.Run(() => paths.Select(path =>
            {
                try { using var f = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); return f.Length == 0 ? (path, "Empty file") : (path, ""); }
                catch (Exception ex) { return (path, ex.Message); }
            }).Where(x => x.Item2.Length > 0).ToArray(), _lifetime.Token);
            foreach (var (path, reason) in problems) BrokenMediaRegistry.Mark(path, reason);
            var screens = System.Windows.Forms.Screen.AllScreens.Length;
            MessageBox.Show(this, $"Checked {paths.Length} queued file paths. Problems: {problems.Length}.\nConnected displays: {screens}.\n\n" +
                string.Join("\n", problems.Take(20).Select(x => Path.GetFileName(x.path) + ": " + x.Item2)) +
                "\n\nThis checks file access, not the entire recording. Next-track cueing checks Windows decoder opening. Confirm sound with a short speaker test before the show.", "Pre-show Health Check");
        }
        catch (Exception ex) { App.WriteDiagnostic("PRE-SHOW CHECK", ex.ToString()); }
        finally { _healthCheckRunning = false; }
    }
}
