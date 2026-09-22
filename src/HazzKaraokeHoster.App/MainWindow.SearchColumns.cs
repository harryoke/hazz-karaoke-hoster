using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private readonly SearchColumnSettingsFile _searchColumnSettings = SearchColumnSettingsStore.Load();
    private string? _searchColumnsAppliedMode;
    private bool _applyingSearchColumnLayout;
    private Button? _searchColumnsButton;

    private sealed record SearchColumnDescriptor(string Key, string Label, DataGridColumn Column, DataGridLength DefaultWidth);

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        // Keep this feature self-contained so it can be carried forward without
        // disturbing the established v1.74 search/playback code paths.
        Loaded += SearchColumns_MainWindowLoaded;
        Closed += (_, _) => SaveSearchColumnLayout();
    }

    private void SearchColumns_MainWindowLoaded(object sender, RoutedEventArgs e)
    {
        InstallSearchColumnsButton();
        SearchGrid.ColumnReordered += SearchGrid_ColumnReordered;
        SearchGrid.PreviewMouseLeftButtonUp += SearchGrid_PreviewMouseLeftButtonUp;
        SearchGrid.LayoutUpdated += SearchGrid_SearchColumnsLayoutUpdated;

        // The existing XAML mode handlers run first. Re-apply the saved layout
        // afterwards because v1.74's UpdateSearchModeUi still applies its legacy
        // Length-column default while changing modes.
        SearchKaraokeButton.Click += SearchModeButton_SearchColumnsChanged;
        SearchMusicButton.Click += SearchModeButton_SearchColumnsChanged;
        SearchMusicVideoButton.Click += SearchModeButton_SearchColumnsChanged;
        SearchResultsOverlay.IsVisibleChanged += SearchResultsOverlay_SearchColumnsVisibleChanged;

        ApplySearchColumnLayout(_searchMediaKind, force: true);
    }

    private void InstallSearchColumnsButton()
    {
        if (_searchColumnsButton is not null) return;
        if (SearchResultsTitle.Parent is not DockPanel header) return;

        var closeButton = header.Children
            .OfType<Button>()
            .FirstOrDefault(x => string.Equals(x.Content?.ToString(), "CLOSE", StringComparison.OrdinalIgnoreCase));
        if (closeButton is null) return;

        header.Children.Remove(closeButton);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(buttons, Dock.Right);

        _searchColumnsButton = new Button
        {
            Content = "COLUMNS",
            Margin = new Thickness(0, 0, 6, 0),
            ToolTip = "Choose, resize and reorder search-result columns for this search mode."
        };
        if (TryFindResource("LoadButtonStyle") is Style style) _searchColumnsButton.Style = style;
        _searchColumnsButton.Click += SearchColumns_Click;
        buttons.Children.Add(_searchColumnsButton);
        buttons.Children.Add(closeButton);
        header.Children.Add(buttons);
    }

    private void SearchModeButton_SearchColumnsChanged(object sender, RoutedEventArgs e)
        => Dispatcher.BeginInvoke(new Action(EnsureSearchColumnsForCurrentMode), DispatcherPriority.Background);

    private void SearchResultsOverlay_SearchColumnsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (SearchResultsOverlay.IsVisible) EnsureSearchColumnsForCurrentMode();
    }

    private void SearchGrid_SearchColumnsLayoutUpdated(object? sender, EventArgs e)
        => EnsureSearchColumnsForCurrentMode();

    private void EnsureSearchColumnsForCurrentMode()
    {
        if (!string.Equals(_searchColumnsAppliedMode, _searchMediaKind, StringComparison.OrdinalIgnoreCase))
            ApplySearchColumnLayout(_searchMediaKind);
    }

    private IReadOnlyList<SearchColumnDescriptor> SearchColumnDefinitions()
    {
        DataGridColumn Find(string header)
            => SearchGrid.Columns.First(x => string.Equals(x.Header?.ToString(), header, StringComparison.OrdinalIgnoreCase));

        return new SearchColumnDescriptor[]
        {
            new("Favourite", "Favourite ★", Find("★"), new DataGridLength(34, DataGridLengthUnitType.Pixel)),
            new("Artist", "Artist", Find("Artist"), new DataGridLength(1.7, DataGridLengthUnitType.Star)),
            new("Title", "Title", Find("Title"), new DataGridLength(1.9, DataGridLengthUnitType.Star)),
            new("Maker", "Maker", Find("Maker"), new DataGridLength(1, DataGridLengthUnitType.Star)),
            new("Disc", "Disc", Find("Disc"), new DataGridLength(1, DataGridLengthUnitType.Star)),
            new("Length", "Length", Find("Length"), new DataGridLength(72, DataGridLengthUnitType.Pixel)),
            new("Format", "Format", Find("Format"), new DataGridLength(80, DataGridLengthUnitType.Pixel))
        };
    }

    private List<SearchColumnState> DefaultSearchColumnLayout(string mode)
    {
        var isKaraoke = string.Equals(mode, "Karaoke", StringComparison.OrdinalIgnoreCase);
        return SearchColumnDefinitions().Select((definition, index) => new SearchColumnState
        {
            Key = definition.Key,
            Visible = definition.Key != "Length" || isKaraoke,
            DisplayIndex = index,
            Width = definition.DefaultWidth.Value,
            WidthUnit = definition.DefaultWidth.UnitType.ToString()
        }).ToList();
    }

    private List<SearchColumnState> EffectiveSearchColumnLayout(string mode)
    {
        var defaults = DefaultSearchColumnLayout(mode);
        if (!_searchColumnSettings.Modes.TryGetValue(mode, out var saved) || saved.Count == 0)
            return defaults;

        var savedByKey = saved
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var merged = new List<SearchColumnState>();
        foreach (var state in saved.OrderBy(x => x.DisplayIndex))
        {
            var fallback = defaults.FirstOrDefault(x => x.Key.Equals(state.Key, StringComparison.OrdinalIgnoreCase));
            if (fallback is null || merged.Any(x => x.Key.Equals(fallback.Key, StringComparison.OrdinalIgnoreCase))) continue;
            merged.Add(new SearchColumnState
            {
                Key = fallback.Key,
                Visible = state.Visible,
                DisplayIndex = state.DisplayIndex,
                Width = double.IsFinite(state.Width) && state.Width > 0 ? state.Width : fallback.Width,
                WidthUnit = string.IsNullOrWhiteSpace(state.WidthUnit) ? fallback.WidthUnit : state.WidthUnit
            });
        }

        foreach (var fallback in defaults)
        {
            if (savedByKey.ContainsKey(fallback.Key) || merged.Any(x => x.Key.Equals(fallback.Key, StringComparison.OrdinalIgnoreCase))) continue;
            merged.Add(fallback);
        }

        for (var i = 0; i < merged.Count; i++) merged[i].DisplayIndex = i;
        if (merged.All(x => !x.Visible))
        {
            var artist = merged.FirstOrDefault(x => x.Key == "Artist") ?? merged.First();
            artist.Visible = true;
        }
        return merged;
    }

    private void ApplySearchColumnLayout(string mode, bool force = false)
    {
        if (!force && string.Equals(_searchColumnsAppliedMode, mode, StringComparison.OrdinalIgnoreCase)) return;

        if (_searchColumnsAppliedMode is not null && !string.Equals(_searchColumnsAppliedMode, mode, StringComparison.OrdinalIgnoreCase))
            SaveSearchColumnLayout(_searchColumnsAppliedMode);

        var definitions = SearchColumnDefinitions();
        var definitionByKey = definitions.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var states = EffectiveSearchColumnLayout(mode);
        _applyingSearchColumnLayout = true;
        try
        {
            foreach (var state in states)
            {
                if (!definitionByKey.TryGetValue(state.Key, out var definition)) continue;
                definition.Column.Visibility = state.Visible ? Visibility.Visible : Visibility.Collapsed;
                definition.Column.Width = ToDataGridLength(state, definition.DefaultWidth);
            }

            var ordered = states.OrderBy(x => x.DisplayIndex).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                if (definitionByKey.TryGetValue(ordered[index].Key, out var definition))
                    definition.Column.DisplayIndex = index;
            }
        }
        finally
        {
            _applyingSearchColumnLayout = false;
        }

        _searchColumnsAppliedMode = mode;
    }

    private static DataGridLength ToDataGridLength(SearchColumnState state, DataGridLength fallback)
    {
        var value = double.IsFinite(state.Width) && state.Width > 0 ? state.Width : fallback.Value;
        var unit = state.WidthUnit?.Trim().ToLowerInvariant() switch
        {
            "star" or "*" => DataGridLengthUnitType.Star,
            "auto" => DataGridLengthUnitType.Auto,
            "sizetocells" or "cells" => DataGridLengthUnitType.SizeToCells,
            "sizetoheader" or "header" => DataGridLengthUnitType.SizeToHeader,
            _ => DataGridLengthUnitType.Pixel
        };
        return new DataGridLength(value, unit);
    }

    private void SaveSearchColumnLayout() => SaveSearchColumnLayout(_searchColumnsAppliedMode ?? _searchMediaKind);

    private void SaveSearchColumnLayout(string mode)
    {
        if (_applyingSearchColumnLayout || string.IsNullOrWhiteSpace(mode) || SearchGrid is null) return;
        var definitions = SearchColumnDefinitions();
        if (definitions.Count == 0) return;

        var states = definitions
            .OrderBy(x => x.Column.DisplayIndex)
            .Select((definition, index) => new SearchColumnState
            {
                Key = definition.Key,
                Visible = definition.Column.Visibility == Visibility.Visible,
                DisplayIndex = index,
                Width = definition.Column.Width.Value > 0 && double.IsFinite(definition.Column.Width.Value)
                    ? definition.Column.Width.Value
                    : Math.Max(1, definition.Column.ActualWidth),
                WidthUnit = definition.Column.Width.UnitType.ToString()
            })
            .ToList();

        if (states.All(x => !x.Visible))
        {
            var artist = states.First(x => x.Key == "Artist");
            artist.Visible = true;
            definitions.First(x => x.Key == "Artist").Column.Visibility = Visibility.Visible;
        }

        _searchColumnSettings.Modes[mode] = states;
        SearchColumnSettingsStore.Save(_searchColumnSettings);
    }

    private void SearchColumns_Click(object sender, RoutedEventArgs e)
    {
        if (_searchColumnsButton is null) return;
        SaveSearchColumnLayout();
        var menu = new ContextMenu
        {
            PlacementTarget = _searchColumnsButton,
            Placement = PlacementMode.Bottom
        };

        foreach (var definition in SearchColumnDefinitions().OrderBy(x => x.Column.DisplayIndex))
        {
            var item = new MenuItem
            {
                Header = definition.Label,
                IsCheckable = true,
                IsChecked = definition.Column.Visibility == Visibility.Visible,
                Tag = definition.Key
            };
            item.Click += SearchColumnVisibility_Click;
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var reset = new MenuItem { Header = "Reset this search mode to defaults" };
        reset.Click += SearchColumnsReset_Click;
        menu.Items.Add(reset);
        _searchColumnsButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void SearchColumnVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string key) return;
        var definitions = SearchColumnDefinitions();
        var definition = definitions.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (definition is null) return;

        if (!item.IsChecked)
        {
            var visibleCount = definitions.Count(x => x.Column.Visibility == Visibility.Visible);
            if (visibleCount <= 1)
            {
                item.IsChecked = true;
                SearchStatus.Text = "At least one search-result column must stay visible.";
                return;
            }
        }

        definition.Column.Visibility = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        SaveSearchColumnLayout();
        SearchStatus.Text = "${_searchMediaKind} search columns saved.`;
    }

    private void SearchColumnsReset_Click(object sender, RoutedEventArgs e)
    {
        _searchColumnSettings.Modes[_searchMediaKind] = DefaultSearchColumnLayout(_searchMediaKind);
        SearchColumnSettingsStore.Save(_searchColumnSettings);
        ApplySearchColumnLayout(_searchMediaKind, force: true);
        SearchStatus.Text = "${_searchMediaKind} search columns reset to defaults.`;
    }

    private void SearchGrid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (_applyingSearchColumnLayout) return;
        Dispatcher.BeginInvoke(new Action(SaveSearchColumnLayout), DispatcherPriority.Background);
    }

    private void SearchGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_applyingSearchColumnLayout) return;
        Dispatcher.BeginInvoke(new Action(SaveSearchColumnLayout), DispatcherPriority.Background);
    }
}
