namespace HazzKaraokeHoster.Core.Models;

public enum OverlayPosition { TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight }

public sealed class AudienceOverlaySettings
{
    public bool ShowNextSinger { get; set; } = true;
    public bool ShowNextSong { get; set; } = true;
    public string NextSingerFontFamily { get; set; } = "Segoe UI";
    public double NextSingerFontSize { get; set; } = 48;
    public OverlayPosition NextSingerPosition { get; set; } = OverlayPosition.BottomCenter;
    public bool ScrollerEnabled { get; set; } = true;
    public string ScrollerText { get; set; } = "WELCOME TO KARAOKE WITH HAZZ";
    public string ScrollerFontFamily { get; set; } = "Segoe UI";
    public double ScrollerFontSize { get; set; } = 30;
    public double ScrollerPixelsPerSecond { get; set; } = 110;
    public string ScrollerPosition { get; set; } = "Bottom";

    // Audience font colours. Stored as #AARRGGBB so they can be persisted safely.
    public string NextHeadingColor { get; set; } = "#FFFFD34D";
    public string NextPositionColor { get; set; } = "#FFFFD34D";
    public string NextSingerColor { get; set; } = "#FFFFFFFF";
    public string NextSongColor { get; set; } = "#FFD8E2EF";
    public string RotationScrollerColor { get; set; } = "#FFFFFFFF";
    public string VenueScrollerColor { get; set; } = "#FFFFD34D";

    // Idle / singer-view artwork. This is only shown when karaoke playback is not active.
    public bool BackgroundImageEnabled { get; set; } = false;
    public double BackgroundGifSpeed { get; set; } = 1;
    public string BackgroundStretchMode { get; set; } = "Fit";
    public string BackgroundFolderPath { get; set; } = string.Empty;
    public string BackgroundImagePath { get; set; } = string.Empty;

    // Persistent audience logo. When enabled it stays above CD+G and video output.
    public bool LogoEnabled { get; set; } = false;
    public string LogoImagePath { get; set; } = string.Empty;
    public OverlayPosition LogoPosition { get; set; } = OverlayPosition.TopRight;
    public double LogoWidth { get; set; } = 180;

    // Kamikaze Karaoke announcement shown between songs after a random song is assigned.
    public string KamikazeText { get; set; } = "KAMIKAZE KARAOKE!";
    public string KamikazeFontFamily { get; set; } = "Segoe UI Black";
    public double KamikazeFontSize { get; set; } = 84;
    public string KamikazeColor { get; set; } = "#FFFFD34D";
}


