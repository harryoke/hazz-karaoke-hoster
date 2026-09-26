using System.Text.Json;

namespace HazzKaraokeHoster.App;

internal sealed class AudienceOverlayTextStyle
{
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 28;
    public string TextColor { get; set; } = "#FFFFFFFF";
}

internal sealed class AudienceEnhancementSettings
{
    public double SlideshowSeconds { get; set; } = 60;
    public string SlideshowTransition { get; set; } = "Fade";
    public int ComingUpCount { get; set; } = 4;
    public bool ShowNowSinging { get; set; } = true;
    public double NowSingingSeconds { get; set; } = 7;

    // Test-only broadcast-style audience additions.
    public bool ShowSingerCallUp { get; set; } = true;
    public double SingerCallUpSeconds { get; set; } = 8;
    public bool ShowQueueStatus { get; set; } = true;
    public bool ShowVenueHeader { get; set; } = false;
    public string VenueTitle { get; set; } = string.Empty;
    public string OverlayTransition { get; set; } = "Slide";
    public double AnnouncementSeconds { get; set; } = 8;

    // Every new overlay is user-editable. Colours use #AARRGGBB / #RRGGBB.
    public string SingerCallUpHeadingText { get; set; } = "NEXT SINGER";
    public string SingerCallUpPromptText { get; set; } = "Please make your way to the microphone";
    public AudienceOverlayTextStyle SingerCallUpHeadingStyle { get; set; } = new() { FontFamily = "Segoe UI", FontSize = 28, TextColor = "#FFFFD34D" };
    public AudienceOverlayTextStyle SingerCallUpNameStyle { get; set; } = new() { FontFamily = "Segoe UI Black", FontSize = 72, TextColor = "#FFFFFFFF" };
    public AudienceOverlayTextStyle SingerCallUpSongStyle { get; set; } = new() { FontFamily = "Segoe UI", FontSize = 30, TextColor = "#FFD8E2EF" };
    public AudienceOverlayTextStyle SingerCallUpPromptStyle { get; set; } = new() { FontFamily = "Segoe UI", FontSize = 22, TextColor = "#FFB8C7D5" };
    public string SingerCallUpBackgroundColor { get; set; } = "#E0000000";

    public string NowSingingHeadingText { get; set; } = "NOW SINGING";
    public AudienceOverlayTextStyle NowSingingHeadingStyle { get; set; } = new() { FontFamily = "Segoe UI", FontSize = 22, TextColor = "#FFFFD34D" };
    public AudienceOverlayTextStyle NowSingingNameStyle { get; set; } = new() { FontFamily = "Segoe UI", FontSize = 46, TextColor = "#FFFFFFFF" };
    public AudienceOverlayTextStyle NowSingingSongStyle { get; set; } = new() { FontFamily = "Segoe UI", FontSize = 25, TextColor = "#FFD8E2EF" };
    public string NowSingingBackgroundColor { get; set; } = "#C8000000";

    // Supported placeholders: {active}, {singerWord}, {hold}, {holdPart}, {playable}, {time}, {timePart}.
    public string QueueStatusTemplate { get; set; } = "{active} {singerWord}{holdPart}{timePart}";
    public AudienceOverlayTextStyle QueueStatusStyle { get; set; } = new() { FontFamily = "Segoe UI", FontSize = 20, TextColor = "#FFFFFFFF" };
    public string QueueStatusBackgroundColor { get; set; } = "#B5000000";

    public AudienceOverlayTextStyle VenueHeaderStyle { get; set; } = new() { FontFamily = "Segoe UI", FontSize = 28, TextColor = "#FFFFD34D" };
    public string VenueHeaderBackgroundColor { get; set; } = "#B5000000";

    public AudienceOverlayTextStyle AnnouncementStyle { get; set; } = new() { FontFamily = "Segoe UI", FontSize = 54, TextColor = "#FFFFFFFF" };
    public string AnnouncementBackgroundColor { get; set; } = "#E0000000";
}

internal static class AudienceEnhancementSettingsStore
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hazz Karaoke Hoster");
    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "audience-enhancements-test.json");
    private static readonly object Gate = new();

    public static AudienceEnhancementSettings Load()
    {
        lock (Gate)
        {
            try
            {
                if (!File.Exists(SettingsPath)) return Normalize(new AudienceEnhancementSettings());
                return Normalize(JsonSerializer.Deserialize<AudienceEnhancementSettings>(File.ReadAllText(SettingsPath))
                    ?? new AudienceEnhancementSettings());
            }
            catch
            {
                return Normalize(new AudienceEnhancementSettings());
            }
        }
    }

    public static void Save(AudienceEnhancementSettings settings)
    {
        lock (Gate)
        {
            try
            {
                settings = Normalize(settings);
                Directory.CreateDirectory(SettingsFolder);
                var temp = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, SettingsPath, true);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                App.WriteDiagnostic("AUDIENCE ENHANCEMENT SETTINGS", ex.Message);
            }
        }
    }

    internal static AudienceEnhancementSettings Normalize(AudienceEnhancementSettings settings)
    {
        settings.SlideshowSeconds = double.IsFinite(settings.SlideshowSeconds)
            ? Math.Clamp(settings.SlideshowSeconds, 20, 60) : 60;
        settings.SlideshowTransition = settings.SlideshowTransition switch
        {
            "Cut" or "Fade" or "Slide Left" or "Slide Right" or "Zoom" or "Random" => settings.SlideshowTransition,
            _ => "Fade"
        };
        settings.ComingUpCount = Math.Clamp(settings.ComingUpCount, 1, 4);
        settings.NowSingingSeconds = double.IsFinite(settings.NowSingingSeconds)
            ? Math.Clamp(settings.NowSingingSeconds, 3, 15) : 7;
        settings.SingerCallUpSeconds = double.IsFinite(settings.SingerCallUpSeconds)
            ? Math.Clamp(settings.SingerCallUpSeconds, 3, 15) : 8;
        settings.OverlayTransition = settings.OverlayTransition switch
        {
            "Cut" or "Fade" or "Slide" or "Zoom" or "Pop" or "Random" => settings.OverlayTransition,
            _ => "Slide"
        };
        settings.AnnouncementSeconds = double.IsFinite(settings.AnnouncementSeconds)
            ? Math.Clamp(settings.AnnouncementSeconds, 5, 30) : 8;

        settings.VenueTitle = NormalizeText(settings.VenueTitle, string.Empty, 120);
        settings.SingerCallUpHeadingText = NormalizeText(settings.SingerCallUpHeadingText, "NEXT SINGER", 80);
        settings.SingerCallUpPromptText = NormalizeText(settings.SingerCallUpPromptText, "Please make your way to the microphone", 160);
        settings.NowSingingHeadingText = NormalizeText(settings.NowSingingHeadingText, "NOW SINGING", 80);
        settings.QueueStatusTemplate = NormalizeText(settings.QueueStatusTemplate, "{active} {singerWord}{holdPart}{timePart}", 240);

        settings.SingerCallUpHeadingStyle = NormalizeStyle(settings.SingerCallUpHeadingStyle, "Segoe UI", 28, "#FFFFD34D", 12, 96);
        settings.SingerCallUpNameStyle = NormalizeStyle(settings.SingerCallUpNameStyle, "Segoe UI Black", 72, "#FFFFFFFF", 16, 140);
        settings.SingerCallUpSongStyle = NormalizeStyle(settings.SingerCallUpSongStyle, "Segoe UI", 30, "#FFD8E2EF", 12, 96);
        settings.SingerCallUpPromptStyle = NormalizeStyle(settings.SingerCallUpPromptStyle, "Segoe UI", 22, "#FFB8C7D5", 10, 72);
        settings.NowSingingHeadingStyle = NormalizeStyle(settings.NowSingingHeadingStyle, "Segoe UI", 22, "#FFFFD34D", 10, 72);
        settings.NowSingingNameStyle = NormalizeStyle(settings.NowSingingNameStyle, "Segoe UI", 46, "#FFFFFFFF", 12, 110);
        settings.NowSingingSongStyle = NormalizeStyle(settings.NowSingingSongStyle, "Segoe UI", 25, "#FFD8E2EF", 10, 80);
        settings.QueueStatusStyle = NormalizeStyle(settings.QueueStatusStyle, "Segoe UI", 20, "#FFFFFFFF", 10, 64);
        settings.VenueHeaderStyle = NormalizeStyle(settings.VenueHeaderStyle, "Segoe UI", 28, "#FFFFD34D", 10, 80);
        settings.AnnouncementStyle = NormalizeStyle(settings.AnnouncementStyle, "Segoe UI", 54, "#FFFFFFFF", 14, 120);

        settings.SingerCallUpBackgroundColor = NormalizeColor(settings.SingerCallUpBackgroundColor, "#E0000000");
        settings.NowSingingBackgroundColor = NormalizeColor(settings.NowSingingBackgroundColor, "#C8000000");
        settings.QueueStatusBackgroundColor = NormalizeColor(settings.QueueStatusBackgroundColor, "#B5000000");
        settings.VenueHeaderBackgroundColor = NormalizeColor(settings.VenueHeaderBackgroundColor, "#B5000000");
        settings.AnnouncementBackgroundColor = NormalizeColor(settings.AnnouncementBackgroundColor, "#E0000000");
        return settings;
    }

    private static AudienceOverlayTextStyle NormalizeStyle(AudienceOverlayTextStyle? style, string font, double size, string color, double minSize, double maxSize)
    {
        style ??= new AudienceOverlayTextStyle();
        style.FontFamily = NormalizeText(style.FontFamily, font, 120);
        style.FontSize = double.IsFinite(style.FontSize) ? Math.Clamp(style.FontSize, minSize, maxSize) : size;
        style.TextColor = NormalizeColor(style.TextColor, color);
        return style;
    }

    private static string NormalizeText(string? value, string fallback, int maxLength)
    {
        value = value?.Trim() ?? string.Empty;
        if (value.Length == 0) value = fallback;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        value = value?.Trim() ?? string.Empty;
        if (value.Length == 0) return fallback;
        try
        {
            _ = System.Windows.Media.ColorConverter.ConvertFromString(value);
            return value;
        }
        catch
        {
            return fallback;
        }
    }
}
