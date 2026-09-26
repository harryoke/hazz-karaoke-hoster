using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HazzKaraokeHoster.App;

public sealed class AudienceTextStyle
{
    public string Text { get; set; } = string.Empty;
    public string Font { get; set; } = "Segoe UI";
    public double Size { get; set; } = 32;
    public string Colour { get; set; } = "#FFFFFFFF";

    // Panel appearance is read from the canonical entry for each overlay
    // (Call-up heading, Now singing heading, Announcement, Queue status, Venue header).
    public string BackgroundColour { get; set; } = "#FF000000";
    public double BackgroundOpacity { get; set; } = 0.82;
    public string BorderColour { get; set; } = "#55FFFFFF";
    public double BorderThickness { get; set; } = 1;
    public double CornerRadius { get; set; } = 9;
    public string Position { get; set; } = "Default";

    public static Dictionary<string, AudienceTextStyle> Defaults() => new()
    {
        ["Call-up heading"] = new() { Text = "NEXT SINGER", Size = 28, Colour = "#FFFFD34D", BackgroundOpacity = 0.88, BorderColour = "#FFFFD34D", BorderThickness = 2, CornerRadius = 14, Position = "Center" },
        ["Call-up singer"] = new() { Text = "{singer}", Size = 72 },
        ["Call-up song"] = new() { Text = "{song}", Size = 30 },
        ["Call-up message"] = new() { Text = "Please make your way to the microphone", Size = 22 },
        ["Now singing heading"] = new() { Text = "NOW SINGING", Size = 22, Colour = "#FFFFD34D", BackgroundOpacity = 0.78, BorderColour = "#55FFFFFF", BorderThickness = 1, CornerRadius = 9, Position = "Top Left" },
        ["Now singing singer"] = new() { Text = "{singer}", Size = 46 },
        ["Now singing song"] = new() { Text = "{song}", Size = 25 },
        ["Announcement"] = new() { Text = "{message}", Size = 54, BackgroundOpacity = 0.88, BorderColour = "#FFFFD34D", BorderThickness = 2, CornerRadius = 12, Position = "Center" },
        ["Queue status"] = new() { Text = "{queue}", Size = 20, BackgroundOpacity = 0.71, BorderColour = "#44FFFFFF", BorderThickness = 1, CornerRadius = 7, Position = "Top Left" },
        ["Venue header"] = new() { Text = "{venue}", Size = 28, Colour = "#FFFFD34D", BackgroundOpacity = 0.71, BorderColour = "#44FFFFFF", BorderThickness = 1, CornerRadius = 7, Position = "Top Center" }
    };

    public static Dictionary<string, AudienceTextStyle> Normalize(Dictionary<string, AudienceTextStyle>? saved)
    {
        var result = Defaults();
        foreach (var key in result.Keys.ToArray())
        {
            if (saved is null || !saved.TryGetValue(key, out var style) || style is null) continue;
            var fallback = result[key];
            style.Text ??= fallback.Text;
            if (style.Text.Length > 500) style.Text = style.Text[..500];
            if (string.IsNullOrWhiteSpace(style.Font) || style.Font.Length > 128) style.Font = "Segoe UI";
            try { _ = new FontFamily(style.Font); } catch { style.Font = "Segoe UI"; }
            style.Size = double.IsFinite(style.Size) ? Math.Clamp(style.Size, 12, 160) : fallback.Size;
            style.Colour = NormalizeColour(style.Colour, fallback.Colour);
            style.BackgroundColour = NormalizeColour(style.BackgroundColour, fallback.BackgroundColour);
            style.BorderColour = NormalizeColour(style.BorderColour, fallback.BorderColour);
            style.BackgroundOpacity = double.IsFinite(style.BackgroundOpacity) ? Math.Clamp(style.BackgroundOpacity, 0, 1) : fallback.BackgroundOpacity;
            style.BorderThickness = double.IsFinite(style.BorderThickness) ? Math.Clamp(style.BorderThickness, 0, 12) : fallback.BorderThickness;
            style.CornerRadius = double.IsFinite(style.CornerRadius) ? Math.Clamp(style.CornerRadius, 0, 40) : fallback.CornerRadius;
            style.Position = style.Position switch
            {
                "Default" or "Top Left" or "Top Center" or "Top Right" or "Center Left" or "Center" or "Center Right" or "Bottom Left" or "Bottom Center" or "Bottom Right" => style.Position,
                _ => fallback.Position
            };
            result[key] = style;
        }
        return result;
    }

    private static string NormalizeColour(string? value, string fallback)
    {
        try
        {
            _ = (Color)ColorConverter.ConvertFromString(value ?? string.Empty);
            return value!;
        }
        catch { return fallback; }
    }

    internal void Apply(TextBlock target, params (string Token, string Value)[] values)
    {
        var text = Text;
        foreach (var (token, value) in values) text = text.Replace("{" + token + "}", value);
        target.Text = text;
        target.FontFamily = new FontFamily(Font);
        target.FontSize = Size;
        target.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Colour));
        ApplyPanelAppearanceIfCanonical(target);
    }

    private void ApplyPanelAppearanceIfCanonical(TextBlock target)
    {
        if (target.Name is not ("SingerCallUpHeadingText" or "NowSingingHeadingText" or "AnnouncementText" or "QueueStatusText" or "VenueHeaderText"))
            return;

        var panel = FindAncestorBorder(target);
        if (panel is null) return;

        var background = (Color)ColorConverter.ConvertFromString(BackgroundColour);
        background.A = (byte)Math.Clamp((int)Math.Round(255 * BackgroundOpacity), 0, 255);
        panel.Background = new SolidColorBrush(background);
        panel.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(BorderColour));
        panel.BorderThickness = new Thickness(BorderThickness);
        panel.CornerRadius = new CornerRadius(CornerRadius);

        if (Position == "Default") return;
        (panel.HorizontalAlignment, panel.VerticalAlignment) = Position switch
        {
            "Top Left" => (HorizontalAlignment.Left, VerticalAlignment.Top),
            "Top Center" => (HorizontalAlignment.Center, VerticalAlignment.Top),
            "Top Right" => (HorizontalAlignment.Right, VerticalAlignment.Top),
            "Center Left" => (HorizontalAlignment.Left, VerticalAlignment.Center),
            "Center Right" => (HorizontalAlignment.Right, VerticalAlignment.Center),
            "Bottom Left" => (HorizontalAlignment.Left, VerticalAlignment.Bottom),
            "Bottom Center" => (HorizontalAlignment.Center, VerticalAlignment.Bottom),
            "Bottom Right" => (HorizontalAlignment.Right, VerticalAlignment.Bottom),
            _ => (HorizontalAlignment.Center, VerticalAlignment.Center)
        };
        panel.Margin = target.Name switch
        {
            "SingerCallUpHeadingText" => new Thickness(60),
            "AnnouncementText" => new Thickness(50),
            "QueueStatusText" or "VenueHeaderText" => new Thickness(30, 66, 30, 30),
            _ => new Thickness(30)
        };
    }

    private static Border? FindAncestorBorder(DependencyObject child)
    {
        for (DependencyObject? current = child; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is Border border) return border;
        return null;
    }
}
