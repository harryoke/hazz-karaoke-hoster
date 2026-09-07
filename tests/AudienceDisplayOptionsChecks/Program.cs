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

        Console.WriteLine("PASS: semantic button colours/active lighting, high-contrast dropdowns, four background fit modes, scroller clearance, and search drop targets.");
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
