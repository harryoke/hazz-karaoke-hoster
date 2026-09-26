using System.Windows.Controls;
using System.Windows.Media;

namespace HazzKaraokeHoster.App;

public sealed class AudienceTextStyle
{
    public string Text { get; set; } = string.Empty;
    public string Font { get; set; } = "Segoe UI";
    public double Size { get; set; } = 32;
    public string Colour { get; set; } = "#FFFFFFFF";

    public static Dictionary<string, AudienceTextStyle> Defaults() => new()
    {
        ["Call-up heading"] = new() { Text = "NEXT SINGER", Size = 28, Colour = "#FFFFD34D" },
        ["Call-up singer"] = new() { Text = "{singer}", Size = 72 },
        ["Call-up song"] = new() { Text = "{song}", Size = 30 },
        ["Call-up message"] = new() { Text = "Please make your way to the microphone", Size = 22 },
        ["Now singing heading"] = new() { Text = "NOW SINGING", Size = 22, Colour = "#FFFFD34D" },
        ["Now singing singer"] = new() { Text = "{singer}", Size = 46 },
        ["Now singing song"] = new() { Text = "{song}", Size = 25 },
        ["Announcement"] = new() { Text = "{message}", Size = 54 },
        ["Queue status"] = new() { Text = "{queue}", Size = 20 },
        ["Venue header"] = new() { Text = "{venue}", Size = 28, Colour = "#FFFFD34D" }
    };

    public static Dictionary<string, AudienceTextStyle> Normalize(Dictionary<string, AudienceTextStyle>? saved)
    {
        var result = Defaults();
        foreach (var key in result.Keys.ToArray())
        {
            if (saved is null || !saved.TryGetValue(key, out var style) || style is null) continue;
            style.Text ??= result[key].Text;
            if (style.Text.Length > 500) style.Text = style.Text[..500];
            if (string.IsNullOrWhiteSpace(style.Font) || style.Font.Length > 128) style.Font = "Segoe UI";
            try { _ = new FontFamily(style.Font); } catch { style.Font = "Segoe UI"; }
            style.Size = double.IsFinite(style.Size) ? Math.Clamp(style.Size, 12, 160) : result[key].Size;
            try { _ = (Color)ColorConverter.ConvertFromString(style.Colour); }
            catch { style.Colour = "#FFFFFFFF"; }
            result[key] = style;
        }
        return result;
    }

    internal void Apply(TextBlock target, params (string Token, string Value)[] values)
    {
        var text = Text;
        foreach (var (token, value) in values) text = text.Replace("{" + token + "}", value);
        target.Text = text;
        target.FontFamily = new FontFamily(Font);
        target.FontSize = Size;
        target.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Colour));
    }
}
