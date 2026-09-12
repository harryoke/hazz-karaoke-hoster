using System.Text.Json;

namespace HazzKaraokeHoster.App;

internal sealed class UiLayoutSettings
{
    public double HostTextScale { get; set; } = 1;
    public bool UseLibVlcAudienceVideo { get; set; }
    public bool SmoothCdgPicture { get; set; } = true;
    public bool AutomaticRotation { get; set; }
    public string NewcomerPlacement { get; set; } = "End of current round";
    public int NewcomerSpacing { get; set; } = 2;
    public string RotationPrimary { get; set; } = "Fewest turns";
    public string RotationSecondary { get; set; } = "Longest waiting";
    public bool RotationAvoidConsecutive { get; set; }
    public double DefaultDeck1Volume { get; set; } = 0.85;
    public double DefaultDeck2Volume { get; set; } = 0.85;
    public bool DefaultAutoCrossfade { get; set; } = true;
    public double DefaultCrossfadeSeconds { get; set; } = 4;
    public bool MusicVideoShowLogo { get; set; } = false;
    public bool MusicVideoShowScroller { get; set; } = false;
    public bool MusicVideoShowSingers { get; set; } = false;
    public bool MusicVideoShowKamikaze { get; set; } = false;

    public double LeftColumnWeight { get; set; } = 0.93;
    public double CenterColumnWeight { get; set; } = 1.24;
    public double RightColumnWeight { get; set; } = 0.93;
    public double DeckAPlayerHeight { get; set; } = 205;
    public double DeckBPlayerHeight { get; set; } = 205;
    public double KaraokeDeckHeight { get; set; } = 250;
    public double KaraokePreviewHeight { get; set; } = 120;
    public bool PreviewVisible { get; set; } = true;
    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 840;
    public bool WindowMaximized { get; set; } = false;
    public bool KaraokeOnlyMode { get; set; } = false;
    public bool SingleDeckMode { get; set; } = false;

    // Audience artwork is stored with the host UI settings so it survives restarts.
    public bool AudienceBackgroundEnabled { get; set; } = false;
    public double AudienceBackgroundGifSpeed { get; set; } = 1;
    public string AudienceBackgroundStretchMode { get; set; } = "Fit";
    public string AudienceBackgroundFolderPath { get; set; } = string.Empty;
    public string AudienceBackgroundImagePath { get; set; } = string.Empty;
    public bool AudienceLogoEnabled { get; set; } = false;
    public string AudienceLogoImagePath { get; set; } = string.Empty;
    public string AudienceLogoPosition { get; set; } = "TopRight";
    public double AudienceLogoWidth { get; set; } = 180;


    public string AudienceNextHeadingColor { get; set; } = "#FFFFD34D";
    public string AudienceNextPositionColor { get; set; } = "#FFFFD34D";
    public string AudienceNextSingerColor { get; set; } = "#FFFFFFFF";
    public string AudienceNextSongColor { get; set; } = "#FFD8E2EF";
    public string AudienceRotationScrollerColor { get; set; } = "#FFFFFFFF";
    public string AudienceVenueScrollerColor { get; set; } = "#FFFFD34D";

    // Audience text/rotation settings. These are persisted alongside the artwork
    // settings so the complete singer display returns exactly as the host left it.
    public bool AudienceShowNextSinger { get; set; } = true;
    public bool AudienceShowNextSong { get; set; } = true;
    public string AudienceNextSingerFontFamily { get; set; } = "Segoe UI";
    public double AudienceNextSingerFontSize { get; set; } = 48;
    public string AudienceNextSingerPosition { get; set; } = "BottomCenter";
    public bool AudienceScrollerEnabled { get; set; } = true;
    public string AudienceScrollerText { get; set; } = "WELCOME TO KARAOKE WITH HAZZ • PLEASE HAVE YOUR NEXT SONG READY •";
    public string AudienceScrollerFontFamily { get; set; } = "Segoe UI";
    public double AudienceScrollerFontSize { get; set; } = 30;
    public double AudienceScrollerPixelsPerSecond { get; set; } = 110;
    public string AudienceScrollerPosition { get; set; } = "Bottom";

    public string AudienceKamikazeText { get; set; } = "KAMIKAZE KARAOKE!";
    public string AudienceKamikazeFontFamily { get; set; } = "Segoe UI Black";
    public double AudienceKamikazeFontSize { get; set; } = 84;
    public string AudienceKamikazeColor { get; set; } = "#FFFFD34D";
}



internal static class UiLayoutSettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hazz Karaoke Hoster", "ui-layout.json");

    public static UiLayoutSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new UiLayoutSettings();
            return JsonSerializer.Deserialize<UiLayoutSettings>(File.ReadAllText(SettingsPath)) ?? new UiLayoutSettings();
        }
        catch
        {
            return new UiLayoutSettings();
        }
    }

    public static void Save(UiLayoutSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Layout persistence must never be allowed to interfere with a live show or shutdown.
        }
    }
}
