using System.Windows;
using System.Windows.Controls;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private string _consoleSkin = "Classic";
    private ConsoleSkinLayout? _consoleSkinLayout;
    private double _classicLeftWeight = 0.93, _classicCenterWeight = 1.24, _classicRightWeight = 0.93;

    private void ConsoleSkin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        if (_consoleSkin == "Classic")
        {
            RememberClassicWidths();
            _consoleSkinLayout = new ConsoleSkinLayout(this);
        }
        _consoleSkin = ConsoleSkinLayout.Normalize(item.Tag?.ToString());
        ApplyConsoleSkinLayout();
        ClampFixedRowsToViewport();
        // A console-only choice must never serialize audience controls. They
        // may be in a closed popup or in the middle of a venue restore.
        if (_mainLayoutReady && !_restoringMainLayout)
            UiLayoutSettingsStore.SaveConsoleSkin(_consoleSkin);
    }

    private void RememberClassicWidths()
    {
        if (_consoleSkin != "Classic") return;
        var total = MainLeftColumn.ActualWidth + MainCenterColumn.ActualWidth + MainRightColumn.ActualWidth;
        if (total <= 0) return;
        _classicLeftWeight = System.Math.Max(0.05, MainLeftColumn.ActualWidth / total);
        _classicCenterWeight = System.Math.Max(0.05, MainCenterColumn.ActualWidth / total);
        _classicRightWeight = System.Math.Max(0.05, MainRightColumn.ActualWidth / total);
    }

    private void ApplyConsoleSkinLayout()
    {
        if (DeckWorkspaceGrid == null) return;
        _consoleSkinLayout ??= new ConsoleSkinLayout(this);
        _consoleSkinLayout.Apply(_consoleSkin, _karaokeOnlyMode, _singleDeckMode);
        foreach (var child in ConsoleSkinMenu.Items)
            if (child is MenuItem item) item.IsChecked = (string)item.Tag == _consoleSkin;
        PositionSkinSearch();
    }

    private void PositionSkinSearch()
    {
        if (SearchResultsOverlay == null) return;
        var target = IsMusicSearchMode || _karaokeOnlyMode ? KaraokeWorkspace : MusicDeckAPanel;
        Grid.SetColumn(SearchResultsOverlay, Grid.GetColumn(target));
        Grid.SetRow(SearchResultsOverlay, Grid.GetRow(target));
        Grid.SetColumnSpan(SearchResultsOverlay, Grid.GetColumnSpan(target));
        Grid.SetRowSpan(SearchResultsOverlay, Grid.GetRowSpan(target));
    }
}
