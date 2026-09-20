using System.Windows;
namespace HazzKaraokeHoster.App;
public partial class MainWindow
{
    private Dictionary<string, double> _savedAvSync = new(StringComparer.OrdinalIgnoreCase);
    private double SavedSync(string? path, double fallback)
        => path is not null && _savedAvSync.TryGetValue(path, out var value) && double.IsFinite(value) ? Math.Clamp(value, -10, 10) : fallback;
    private async void SaveAvSync_Click(object sender, RoutedEventArgs e)
    {
        if (_karaokePackage is null) { SearchStatus.Text = "Load a karaoke song before saving sync."; return; }
        var path = _karaokePackage.SourcePath;
        var offset = _cdgTiming.OffsetSeconds;
        var key = _keyChange;
        var recordId = string.Equals(_currentKaraokeRecord?.FilePath, path, StringComparison.OrdinalIgnoreCase) ? _currentKaraokeRecord?.Id : null;
        _savedAvSync[path] = offset;
        if (_activeSingerSong is not null && string.Equals(_activeSingerSong.FilePath, path, StringComparison.OrdinalIgnoreCase)) _activeSingerSong.CdgSyncSeconds = offset;
        if (!TrySaveDisplayPreferences()) return;
        try
        {
            if (recordId is long id) await _library.SaveTrackPreferencesAsync(id, key, offset, _lifetime.Token);
            SearchStatus.Text = "Track sync default saved. Existing queued singer preferences remain independent.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SearchStatus.Text = "File sync saved; library preference could not be updated: " + ex.Message; }
    }
}
