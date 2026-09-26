using System.Text.Json;

namespace HazzKaraokeHoster.App;

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
    public Dictionary<string, AudienceTextStyle> TextStyles { get; set; } = AudienceTextStyle.Defaults();
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
                if (!File.Exists(SettingsPath)) return new AudienceEnhancementSettings();
                return Normalize(JsonSerializer.Deserialize<AudienceEnhancementSettings>(File.ReadAllText(SettingsPath))
                    ?? new AudienceEnhancementSettings());
            }
            catch
            {
                return new AudienceEnhancementSettings();
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

    private static AudienceEnhancementSettings Normalize(AudienceEnhancementSettings settings)
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
        settings.VenueTitle = (settings.VenueTitle ?? string.Empty).Trim();
        if (settings.VenueTitle.Length > 120) settings.VenueTitle = settings.VenueTitle[..120];
        settings.TextStyles = AudienceTextStyle.Normalize(settings.TextStyles);
        return settings;
    }
}
