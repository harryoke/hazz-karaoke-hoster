using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private bool _v18ImprovementsInitialized;
    private LibraryHealthWindow? _libraryHealthWindow;

    private void InitializeV18Improvements()
    {
        if (_v18ImprovementsInitialized) return;
        _v18ImprovementsInitialized = true;

        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var alternative = FindButtonByContent(this, "FIND ALTERNATIVE");
                if (alternative is not null)
                {
                    alternative.Click -= FindAlternative_Click;
                    alternative.Click += FindAlternativeAdvanced_Click;
                    alternative.ToolTip = "Show every indexed karaoke version with manufacturer, disc, length and format; replace the loaded version without changing singer, key, sync or rotation.";

                    if (alternative.Parent is Panel parent && FindButtonByContent(parent, "LIBRARY HEALTH") is null)
                    {
                        var health = new Button
                        {
                            Content = "LIBRARY HEALTH",
                            ToolTip = "Scan the karaoke library for missing files, bad ZIPs, broken MP3+CDG pairs, duplicates and missing metadata/durations."
                        };
                        health.SetResourceReference(FrameworkElement.StyleProperty, "SpecialButtonStyle");
                        health.Click += LibraryHealth_Click;
                        var index = parent.Children.IndexOf(alternative);
                        parent.Children.Insert(Math.Min(index + 1, parent.Children.Count), health);
                    }
                }
            }
            catch (Exception ex)
            {
                App.WriteDiagnostic("V1.8 IMPROVEMENTS INIT", ex.ToString());
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static Button? FindButtonByContent(DependencyObject root, string text)
    {
        if (root is Button button && string.Equals(button.Content?.ToString(), text, StringComparison.OrdinalIgnoreCase))
            return button;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindButtonByContent(VisualTreeHelper.GetChild(root, i), text);
            if (found is not null) return found;
        }
        return null;
    }

    private async void FindAlternativeAdvanced_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var artist = (_activeSingerSong?.Artist ?? _currentKaraokeRecord?.Artist ?? string.Empty).Trim();
            var title = (_activeSingerSong?.SongTitle ?? _currentKaraokeRecord?.Title ?? string.Empty).Trim();
            var currentPath = _activeSingerSong?.FilePath ?? _currentKaraokeRecord?.FilePath ?? _karaokePackage?.SourcePath;
            if (title.Length == 0)
            {
                SearchStatus.Text = "Load a singer/track first, then use FIND ALTERNATIVE.";
                return;
            }

            SearchStatus.Text = "Finding all karaoke versions…";
            var current = _currentKaraokeRecord;
            if (current is null && !string.IsNullOrWhiteSpace(currentPath))
                current = await _library.FindByFilePathAsync(currentPath, _lifetime.Token);

            var alternatives = await _library.FindAlternativesAsync(artist, title, null, 500, _lifetime.Token);
            var versions = alternatives.ToList();
            if (current is not null && versions.All(x => !PathEqualsV18(x.FilePath, current.FilePath)))
                versions.Insert(0, current);

            if (versions.Count == 0)
            {
                SearchStatus.Text = $"No other indexed version found for {artist} — {title}.".Trim(' ', '—');
                return;
            }

            var singerName = _activeSinger?.SingerName ?? string.Empty;
            var preferredPath = SingerVersionPreferenceStore.GetPreferredPath(singerName, artist, title);
            var chooser = new AlternativeVersionsWindow(this, singerName, artist, title, current, versions, preferredPath);
            var accepted = chooser.ShowDialog() == true;
            if (chooser.ForgetPreference)
                SingerVersionPreferenceStore.ClearPreferredPath(singerName, artist, title);
            if (!accepted || chooser.SelectedRecord is not SongRecord candidate) return;

            if (chooser.RememberSelection)
                SingerVersionPreferenceStore.SetPreferredPath(singerName, artist, title, candidate.FilePath);

            if (!string.IsNullOrWhiteSpace(currentPath) && PathEqualsV18(candidate.FilePath, currentPath))
            {
                SearchStatus.Text = "That karaoke version is already loaded.";
                return;
            }

            var singerKey = _keyChange;
            var singerSync = _cdgTiming.OffsetSeconds;
            var singerSong = _activeSingerSong;
            var loaded = await LoadKaraokeAsync(candidate.FilePath, preserveAlternativeCycle: false);
            if (!loaded) return;

            _currentKaraokeRecord = candidate;
            _keyChange = singerKey;
            KeyText.Text = $"{_keyChange:+0;-0;0}";
            _pitchAudio.SetSemitones(_keyChange);
            _cdgTiming.Set(singerSync);

            if (singerSong is not null)
            {
                singerSong.SongId = candidate.Id;
                singerSong.FilePath = candidate.FilePath;
                singerSong.KeyChange = singerKey;
                singerSong.CdgSyncSeconds = singerSync;
                if (candidate.DurationSeconds is double duration && duration > 0)
                    singerSong.DurationSeconds = duration;
                singerSong.RefreshSongSummaryIfAvailable();
            }

            _kamikazeBannerRequested = false;
            _audience?.HideKamikazeBanner();
            UpdateAudienceNext();
            MarkLiveShowStateDirty();
            SearchStatus.Text = $"Alternative loaded: {candidate.Manufacturer} {candidate.DiscId} • {candidate.Format} • {candidate.DurationText}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            App.WriteDiagnostic("FIND ALTERNATIVE ADVANCED", ex.ToString());
            SearchStatus.Text = "Find Alternative failed: " + ex.Message;
        }
    }

    private void LibraryHealth_Click(object sender, RoutedEventArgs e)
    {
        if (_libraryHealthWindow is { IsVisible: true })
        {
            _libraryHealthWindow.Activate();
            return;
        }
        _libraryHealthWindow = new LibraryHealthWindow(this, _db, _library);
        _libraryHealthWindow.Closed += (_, _) => _libraryHealthWindow = null;
        _libraryHealthWindow.Show();
    }

    private static bool PathEqualsV18(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
    }
}

internal static class SingerSongEntryV18Extensions
{
    // Song summary properties are notification-driven in SingerSongEntry/SingerQueueEntry.
    // This deliberately remains a no-op hook so the alternative loader can be kept isolated
    // from queue internals while still documenting the update point.
    public static void RefreshSongSummaryIfAvailable(this HazzKaraokeHoster.Core.Models.SingerSongEntry song) { }
}
