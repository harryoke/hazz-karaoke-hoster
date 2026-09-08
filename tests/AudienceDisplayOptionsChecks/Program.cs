using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HazzKaraokeHoster.App;
using HazzKaraokeHoster.Core.Models;

internal static class Check
{
    [STAThread]
    private static void Main()
    {
        var application = new Application();

        var audience = new AudienceWindow { Opacity = 0, ShowActivated = false, ShowInTaskbar = false };
        audience.Show();
        var image = (Image)audience.FindName("SingerBackgroundImage");
        var gif = (Image)audience.FindName("SingerBackgroundGif");
        var video = (MediaElement)audience.FindName("SingerBackgroundVideo");
        var musicVideo = (MediaElement)audience.FindName("MusicVideoMedia");
        var scroller = (Border)audience.FindName("ScrollerPanel");
        var next = (Border)audience.FindName("NextSingerPanel");

        foreach (var pair in new[]
        {
            (Name: "Fit", Stretch: Stretch.Uniform),
            (Name: "FillCrop", Stretch: Stretch.UniformToFill),
            (Name: "Stretch", Stretch: Stretch.Fill),
            (Name: "Center", Stretch: Stretch.None)
        })
        {
            audience.Apply(new AudienceOverlaySettings { BackgroundStretchMode = pair.Name });
            Require(image.Stretch == pair.Stretch && gif.Stretch == pair.Stretch && video.Stretch == pair.Stretch,
                $"Background mode {pair.Name} did not reach every background media layer.");
        }

        audience.Apply(new AudienceOverlaySettings
        {
            ScrollerPosition = "Top",
            NextSingerPosition = OverlayPosition.TopCenter
        });
        Require(scroller.VerticalAlignment == VerticalAlignment.Top && next.Margin.Top == 74 && next.Margin.Bottom == 30,
            "Top scroller placement or top Next Singer clearance is incorrect.");

        audience.Apply(new AudienceOverlaySettings
        {
            ScrollerPosition = "Bottom",
            NextSingerPosition = OverlayPosition.BottomCenter
        });
        Require(scroller.VerticalAlignment == VerticalAlignment.Bottom && next.Margin.Bottom == 74 && next.Margin.Top == 30,
            "Bottom scroller placement or bottom Next Singer clearance is incorrect.");
        Require(musicVideo.IsMuted && musicVideo.Volume == 0 && musicVideo.Stretch == Stretch.Uniform,
            "VJ music-video output is not silent and screen-fitted.");
        foreach (var method in new[] { "ShowMusicVideo", "PauseMusicVideo", "ResumeMusicVideo", "SyncMusicVideo", "ClearMusicVideo" })
            Require(typeof(AudienceWindow).GetMethod(method) is not null, $"Audience VJ method is missing: {method}");
        audience.Close();

        var main = new MainWindow();
        main.Measure(new Size(1440, 840));
        main.Arrange(new Rect(0, 0, 1440, 840));
        var positionCombo = (ComboBox)main.FindName("PositionCombo");
        positionCombo.ApplyTemplate();
        var comboChrome = (Border)positionCombo.Template.FindName("ComboChrome", positionCombo);
        Require(IsDark(comboChrome.Background) && IsLight(positionCombo.Foreground),
            "Closed dropdown does not have high-contrast light text on dark steel.");
        var option = (ComboBoxItem)positionCombo.Items[0];
        option.ApplyTemplate();
        var itemChrome = (Border)option.Template.FindName("ItemChrome", option);
        Require(IsDark(itemChrome.Background) && IsLight(option.Foreground),
            "Opened dropdown option does not have high-contrast light text on a dark row.");
        var singerCombo = (ComboBox)main.FindName("SingerNameBox");
        singerCombo.ApplyTemplate();
        var editor = (TextBox)singerCombo.Template.FindName("PART_EditableTextBox", singerCombo);
        Require(IsDark(editor.Background) && IsLight(editor.Foreground) && editor.CaretBrush is not null,
            "Editable singer dropdown is not high contrast.");

        var buttons = Walk(main).OfType<Button>().ToArray();
        var play = buttons.First(x => Equals(x.Content, "▶ PLAY"));
        var pause = buttons.First(x => Equals(x.Content, "Ⅱ PAUSE"));
        var stop = buttons.First(x => Equals(x.Content, "■ STOP"));
        var load = buttons.First(x => Equals(x.Content, "LOAD NEXT SINGER"));
        Require(Dominant(play.Background, 'g') && Dominant(stop.Background, 'r') && Dominant(pause.Background, 'y') && Dominant(load.Background, 'b'),
            "Play, Stop, Pause and Load Next Singer do not have their semantic colours.");
        Require(new[] { BrushText(play.Background), BrushText(pause.Background), BrushText(stop.Background), BrushText(load.Background) }.Distinct().Count() == 4,
            "Primary function buttons still share the same colour.");
        play.Tag = "Active";
        play.ApplyTemplate();
        var illumination = (Border)play.Template.FindName("Illumination", play);
        Require(illumination.Opacity >= 0.24, "Active button does not brighten.");

        var kind = typeof(MainWindow).GetField("_searchMediaKind", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var update = typeof(MainWindow).GetMethod("UpdateSearchModeUi", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var overlay = (Border)main.FindName("SearchResultsOverlay");
        kind.SetValue(main, "Music");
        update.Invoke(main, null);
        Require(Grid.GetColumn(overlay) == 2, "Music search still covers Deck 1.");
        kind.SetValue(main, "Karaoke");
        update.Invoke(main, null);
        Require(Grid.GetColumn(overlay) == 0, "Karaoke search does not leave the singer rotation available.");

        var singleDeck = typeof(MainWindow).GetMethod("SetSingleDeckMode", BindingFlags.Instance | BindingFlags.NonPublic)!;
        singleDeck.Invoke(main, new object[] { true, false, false });
        var deck2Player = (Border)main.FindName("DeckBPlayerControlsPanel");
        var sideControls = (Border)main.FindName("DeckBSideListControlsPanel");
        var deck2Footer = (WrapPanel)main.FindName("DeckBPlaylistFooter");
        Require(deck2Player.Visibility == Visibility.Collapsed && sideControls.Visibility == Visibility.Visible && deck2Footer.Visibility == Visibility.Collapsed,
            "Single Deck mode did not replace the complete Deck 2 player with side-list controls.");
        var sideButtons = Walk(sideControls).OfType<Button>().Select(button => button.Content?.ToString()).ToHashSet();
        foreach (var label in new[] { "ADD FILES", "LOAD LIST…", "SAVE LIST", "SELECT ALL", "▲ MOVE UP", "▼ MOVE DOWN", "SEND TO DECK 1 →", "SHUFFLE LIST", "REMOVE SELECTED", "CLEAR LIST" })
            Require(sideButtons.Contains(label), $"Side-list control is missing: {label}");
        singleDeck.Invoke(main, new object[] { false, false, false });
        Require(deck2Player.Visibility == Visibility.Visible && sideControls.Visibility == Visibility.Collapsed && deck2Footer.Visibility == Visibility.Visible,
            "Normal mode did not restore the Deck 2 player.");

        Require(main.FindName("DeckAProgress") is Slider && main.FindName("DeckBProgress") is Slider,
            "Music decks do not expose draggable seek sliders.");
        Require(((Border)main.FindName("KaraokeDeckDropTarget")).AllowDrop,
            "The Karaoke Deck is not a drop target.");
        var deckType = typeof(MainWindow).GetNestedType("MusicDeckId", BindingFlags.NonPublic)!;
        var deck1 = Enum.Parse(deckType, "Deck1");
        var setPaused = typeof(MainWindow).GetMethod("SetDeckPaused", BindingFlags.Instance | BindingFlags.NonPublic)!;
        setPaused.Invoke(main, new[] { deck1, (object)true });
        Require(Equals(((Button)main.FindName("DeckAPauseButton")).Content, "▶ RESUME"), "Pause does not switch to Resume.");
        setPaused.Invoke(main, new[] { deck1, (object)false });
        Require(Equals(((Button)main.FindName("DeckAPauseButton")).Content, "Ⅱ PAUSE"), "Resume does not restore the Pause label.");
        var handoffSeconds = (double)typeof(MainWindow).GetField("KaraokeStopHandoffSeconds", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        Require(Math.Abs(handoffSeconds - 1.5) < 0.001, "Karaoke Fade Stop handoff is not fixed at 1.5 seconds.");
        Require(typeof(MainWindow).GetField("_audienceMusicVideoDeck", BindingFlags.Instance | BindingFlags.NonPublic) is not null &&
                typeof(MainWindow).GetMethod("ApplyCurrentMusicVideoToAudience", BindingFlags.Instance | BindingFlags.NonPublic) is not null,
            "Music-deck VJ routing is not wired to the audience display.");

        Console.WriteLine("PASS: colours, dropdowns, audience layout, VJ video routing, search placement, dedicated side-list controls, seek sliders, Karaoke Deck drop target, pause/resume state and 1.5-second handoff.");
        application.Shutdown();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool IsDark(Brush brush)
        => brush is SolidColorBrush solid && (solid.Color.R + solid.Color.G + solid.Color.B) < 240;

    private static bool IsLight(Brush brush)
        => brush is SolidColorBrush solid && (solid.Color.R + solid.Color.G + solid.Color.B) > 600;

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Walk(child)) yield return descendant;
    }

    private static string BrushText(Brush brush) => brush.ToString();

    private static bool Dominant(Brush brush, char colour)
    {
        if (brush is not SolidColorBrush solid) return false;
        var c = solid.Color;
        return colour switch
        {
            'r' => c.R > c.G && c.R > c.B,
            'g' => c.G > c.R && c.G > c.B,
            'b' => c.B > c.R && c.B > c.G,
            'y' => c.R > c.B && c.G > c.B,
            _ => false
        };
    }
}
