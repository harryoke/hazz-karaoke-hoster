using System.Windows;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private CdgPresentationSettings _cdgDefaults = new();
    private Dictionary<string, CdgPresentationSettings> _cdgSongPresentation = new(StringComparer.OrdinalIgnoreCase);
    private bool _loadingCdgPresentation, _usingSongPresentation;

    private CdgPresentationSettings CaptureCdgPresentation() => new()
    {
        Enabled = TransparentCdgCheck.IsChecked == true,
        BackgroundColour = CdgColourCombo.SelectedIndex - 1,
        BackgroundOpacity = CdgBackgroundOpacitySlider.Value,
        LyricsOpacity = CdgLyricsOpacitySlider.Value
    };
    private void SetCdgPresentationControls(CdgPresentationSettings options)
    {
        _loadingCdgPresentation = true;
        try
        {
            TransparentCdgCheck.IsChecked = options.Enabled;
            CdgColourCombo.SelectedIndex = Math.Clamp(options.BackgroundColour, -1, 15) + 1;
            CdgBackgroundOpacitySlider.Value = double.IsFinite(options.BackgroundOpacity) ? Math.Clamp(options.BackgroundOpacity, 0, 1) : 0.6;
            CdgLyricsOpacitySlider.Value = double.IsFinite(options.LyricsOpacity) ? Math.Clamp(options.LyricsOpacity, 0, 1) : 1;
        }
        finally { _loadingCdgPresentation = false; }
    }
    private void RestoreCdgPresentation(UiLayoutSettings settings)
    {
        _cdgDefaults = settings.CdgPresentation ?? new();
        _cdgSongPresentation = new(settings.CdgSongPresentation ?? new(), StringComparer.OrdinalIgnoreCase);
        LoadCdgPresentationForSong(_karaokePackage?.SourcePath);
    }
    private void LoadCdgPresentationForSong(string? path)
    {
        _usingSongPresentation = path is not null && _cdgSongPresentation.ContainsKey(path);
        SetCdgPresentationControls(_usingSongPresentation ? _cdgSongPresentation[path!] : _cdgDefaults);
        _lastRenderedCdgVersion = -1;
        if (IsLoaded && !_restoringMainLayout) ApplyOverlaySettings();
    }
    private void CdgPresentationChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _restoringMainLayout || _loadingCdgPresentation) return;
        if (!_usingSongPresentation) _cdgDefaults = CaptureCdgPresentation();
        RefreshCdgPresentation();
    }
    private void RefreshCdgPresentation()
    {
        RenderCdgAtCurrentPosition(force: true);
        ApplyOverlaySettings();
    }
    private void RestoreCdgOriginal_Click(object sender, RoutedEventArgs e)
    {
        SetCdgPresentationControls(new());
        if (!_usingSongPresentation) _cdgDefaults = CaptureCdgPresentation();
        RefreshCdgPresentation();
    }
    private void SaveCdgPresentation_Click(object sender, RoutedEventArgs e)
    {
        if (_karaokePackage?.Kind != HazzKaraokeHoster.Playback.KaraokePackageKind.CdgPair)
        { SearchStatus.Text = "Load a CD+G song before saving its display settings."; return; }
        _cdgSongPresentation[_karaokePackage.SourcePath] = CaptureCdgPresentation();
        _usingSongPresentation = true;
        if (!TrySaveDisplayPreferences()) return;
        SearchStatus.Text = "CD+G display settings saved for this song. Original media files unchanged.";
    }
    private bool TrySaveDisplayPreferences()
    {
        try { if (SaveMainLayout()) return true; }
        catch (Exception ex) { App.WriteDiagnostic("DISPLAY PREFERENCES", ex.ToString()); }
        SearchStatus.Text = "Display changes are active, but could not be saved. Check storage and retry Save.";
        return false;
    }
    private void ClearCdgPresentation_Click(object sender, RoutedEventArgs e)
    {
        if (_karaokePackage is not null) _cdgSongPresentation.Remove(_karaokePackage.SourcePath);
        _usingSongPresentation = false;
        SetCdgPresentationControls(_cdgDefaults);
        RefreshCdgPresentation();
        if (!TrySaveDisplayPreferences()) return;
        SearchStatus.Text = "This song now uses the default CD+G display settings.";
    }
}
