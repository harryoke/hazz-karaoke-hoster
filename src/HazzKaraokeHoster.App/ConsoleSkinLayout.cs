using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HazzKaraokeHoster.App;

// Layouts use the original control instances. No player, list, command or event
// handler is recreated when changing skins, including during playback.
internal sealed class ConsoleSkinLayout
{
    private readonly Window _window;
    private readonly Grid _workspace;
    private readonly Grid _a, _b, _karaoke;
    private readonly List<Action> _restore = new();
    private readonly Dictionary<string, object> _brushes = new();
    private readonly Brush _background;
    private readonly FontFamily _font;

    internal static string Normalize(string? name) => name is "Midnight" or "Copper" or "Daylight" ? name : "Classic";

    internal ConsoleSkinLayout(Window window)
    {
        _window = window;
        _workspace = Find<Grid>("DeckWorkspaceGrid");
        _a = Find<Grid>("MusicDeckAPanel"); _b = Find<Grid>("MusicDeckBPanel");
        _karaoke = Find<Grid>("KaraokeWorkspace");
        _background = window.Background; _font = window.FontFamily;
        foreach (var grid in new[] { _workspace, _a, _b })
        {
            var originalRows = grid.RowDefinitions.ToArray();
            _restore.Add(() => { grid.RowDefinitions.Clear(); foreach (var row in originalRows) grid.RowDefinitions.Add(row); });
            foreach (var row in grid.RowDefinitions)
            {
                var height = row.Height; var min = row.MinHeight;
                _restore.Add(() => { row.Height = height; row.MinHeight = min; });
            }
            foreach (var column in grid.ColumnDefinitions)
            {
                var width = column.Width; var min = column.MinWidth;
                _restore.Add(() => { column.Width = width; column.MinWidth = min; });
            }
            foreach (UIElement child in grid.Children)
            {
                int row = Grid.GetRow(child), col = Grid.GetColumn(child), rs = Grid.GetRowSpan(child), cs = Grid.GetColumnSpan(child);
                _restore.Add(() => Place(child, row, col, rs, cs));
                if (child is GridSplitter splitter)
                {
                    var direction = splitter.ResizeDirection;
                    var width = splitter.Width; var height = splitter.Height;
                    _restore.Add(() => { splitter.ResizeDirection = direction; splitter.Width = width; splitter.Height = height; });
                }
            }
        }
        foreach (var key in window.Resources.Keys.OfType<string>())
            if (window.Resources[key] is Brush) _brushes[key] = window.Resources[key];
        // Construction can occur while restoring a karaoke-only venue.
        _restore.Add(() => { Place(_a, 0, 0); Place(_karaoke, 0, 2); Place(_b, 0, 4); });
    }

    private T Find<T>(string name) where T : class => (T)_window.FindName(name);
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static void Place(UIElement element, int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        Grid.SetRow(element, row); Grid.SetColumn(element, column);
        Grid.SetRowSpan(element, rowSpan); Grid.SetColumnSpan(element, columnSpan);
    }

    internal void Apply(string skin, bool karaokeOnly, bool singleDeck)
    {
        skin = Normalize(skin);
        _workspace.RowDefinitions.Clear(); _a.ColumnDefinitions.Clear(); _b.ColumnDefinitions.Clear();
        foreach (var restore in _restore) restore();
        foreach (var pair in _brushes) _window.Resources[pair.Key] = pair.Value;
        _window.Background = _background; _window.FontFamily = _font;
        foreach (var splitter in _workspace.Children.OfType<GridSplitter>()) splitter.Visibility = karaokeOnly ? Visibility.Collapsed : Visibility.Visible;
        foreach (var splitter in _a.Children.OfType<GridSplitter>()) splitter.Visibility = Visibility.Visible;
        foreach (var splitter in _b.Children.OfType<GridSplitter>()) splitter.Visibility = singleDeck ? Visibility.Collapsed : Visibility.Visible;
        if (skin != "Classic")
        {
            bool light = skin == "Daylight", copper = skin == "Copper";
            _window.Background = Brush(light ? "#DCE5ED" : copper ? "#211813" : "#071323");
            _window.FontFamily = new FontFamily(light ? "Segoe UI" : copper ? "Bahnschrift" : "Segoe UI");
            _window.Resources["PanelBrush"] = Brush(light ? "#F8FAFC" : copper ? "#2B231F" : "#10243A");
            _window.Resources["Panel2Brush"] = Brush(light ? "#EDF2F7" : copper ? "#201A17" : "#0B1B30");
            _window.Resources["BorderBrush"] = Brush(light ? "#899BAB" : copper ? "#B58960" : "#387B9A");
            _window.Resources["DeckABrush"] = Brush(light ? "#1E6389" : copper ? "#805030" : "#135A7B");
            _window.Resources["DeckBBrush"] = Brush(light ? "#236B53" : copper ? "#5C6539" : "#236E6D");
            _window.Resources["KaraokeBrush"] = Brush(light ? "#634C8B" : copper ? "#6E4631" : "#454E92");
            _window.Resources["PlaylistBrush"] = Brush(light ? "#4A6077" : copper ? "#74552E" : "#24415E");
            if (light) ApplyDaylightBrushes();
        }
        if (skin == "Midnight" && !karaokeOnly)
        {
            _workspace.ColumnDefinitions[0].Width = new GridLength(1.12, GridUnitType.Star);
            _workspace.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            _workspace.ColumnDefinitions[3].Width = new GridLength(0);
            _workspace.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
            _workspace.RowDefinitions.Add(new RowDefinition());
            _workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            _workspace.RowDefinitions.Add(new RowDefinition());
            Place(_karaoke, 0, 0, 3);
            Place(_a, 0, 2, 1, 3); Place(_b, 2, 2, 1, 3);
            Place(Find<GridSplitter>("MainLeftSplitter"), 0, 1, 3);
            Find<GridSplitter>("MainRightSplitter").Visibility = Visibility.Collapsed;
            HorizontalDeck(_a); HorizontalDeck(_b);
        }
        else if (skin == "Copper" && !karaokeOnly)
        {
            Place(_a, 0, 0); Place(_b, 0, 2); Place(_karaoke, 0, 4);
            _workspace.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            _workspace.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            _workspace.ColumnDefinitions[4].Width = new GridLength(1.3, GridUnitType.Star);
            ReverseDeck(_a); ReverseDeck(_b);
        }
        else if (skin == "Daylight" && !karaokeOnly)
        {
            Place(_karaoke, 0, 0); Place(_a, 0, 2); Place(_b, 0, 4);
            _workspace.ColumnDefinitions[0].Width = new GridLength(1.35, GridUnitType.Star);
            _workspace.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            _workspace.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
        }
        var focusHost = Find<Grid>("KaraokeFocusSingerHost");
        Place(focusHost, Grid.GetRow(_b), Grid.GetColumn(_b), Grid.GetRowSpan(_b), Grid.GetColumnSpan(_b));
        if (karaokeOnly) Place(_karaoke, 0, 0, 1, 5);
        // Mode-specific minima must survive resetting layout geometry.
        if (singleDeck) { Find<RowDefinition>("DeckBPlayerRow").MinHeight = 0; Find<RowDefinition>("DeckBSplitterRow").Height = new GridLength(0); }
        Find<Grid>("HostViewport").Background = _window.Background;
    }

    private static void HorizontalDeck(Grid deck)
    {
        deck.ColumnDefinitions.Add(new ColumnDefinition());
        deck.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        deck.ColumnDefinitions.Add(new ColumnDefinition());
        // Keep the named player row Auto so the normal viewport clamp still works.
        // Both sides span the rows, so player height never clips the playlist.
        foreach (UIElement child in deck.Children)
        {
            int row = Grid.GetRow(child);
            Place(child, 0, row == 2 ? 2 : row == 1 ? 1 : 0, 3);
            if (child is GridSplitter splitter)
            {
                splitter.Width = 8; splitter.Height = double.NaN;
                splitter.ResizeDirection = GridResizeDirection.Columns;
            }
        }
    }

    private static void ReverseDeck(Grid deck)
    {
        // Named Auto player row stays at index zero: reverse the actual row
        // definitions so existing sizing code still targets the player itself.
        var player = deck.RowDefinitions[0]; var list = deck.RowDefinitions[2];
        deck.RowDefinitions.Remove(list); deck.RowDefinitions.Remove(player);
        deck.RowDefinitions.Insert(0, list); deck.RowDefinitions.Add(player);
        foreach (UIElement child in deck.Children)
            Grid.SetRow(child, Grid.GetRow(child) == 0 ? 2 : Grid.GetRow(child) == 2 ? 0 : 1);
    }

    private void ApplyDaylightBrushes()
    {
        foreach (var key in _brushes.Keys.Where(k => k.StartsWith("SkinSurface_")))
            _window.Resources[key] = Brush("#F0F4F8");
        foreach (var key in _brushes.Keys.Where(k => k.StartsWith("SkinInk_")))
            _window.Resources[key] = Brush("#172D40");
        foreach (var key in _brushes.Keys.Where(k => k.StartsWith("SkinEdge_")))
            _window.Resources[key] = Brush("#8A9DAE");
        foreach (var key in new[] { "SkinSurface_315B76", "SkinSurface_294A60", "SkinSurface_356B8F", "SkinSurface_285577" })
            _window.Resources[key] = Brush("#BEDAF1");
        _window.Resources["SkinSurface_17202A"] = Brush("#E5EDF4");
        _window.Resources["SkinSurface_8B2635"] = Brush("#F0C5CB");
        _window.Resources["SkinSurface_F3C94B"] = Brush("#F3C94B");
        foreach (var key in new[] { "SkinInk_FFFFD700", "SkinInk_FFD34D", "SkinSurface_020605" })
            if (_brushes.TryGetValue(key, out var original)) _window.Resources[key] = original;
    }
}
