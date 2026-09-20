using System.Text.Json;
using System.Text.Json.Serialization;

namespace HazzKaraokeHoster.App;

internal sealed class UiLayoutSettings
{
    public Dictionary<string, HazzKaraokeHoster.Core.Models.TextStrokeSettings> AudienceTextStrokes { get; set; } = new();
    public string KamikazeFolderPath { get; set; } = string.Empty;
    public string ConsoleSkin { get; set; } = "Classic";
    public double HostTextScale { get; set; } = 1;
    public bool UseLibVlcAudienceVideo { get; set; }
    public bool SmoothCdgPicture { get; set; } = true;
    public string KaraokeMusicAction { get; set; } = "Next";

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
    public bool KaraokeFocusMode { get; set; } = false;

    // Preserve settings added by newer/test builds (for example skin selection)
    // even when this build does not yet have a strongly typed property for them.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

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
    public bool AudienceShowSingerPhotos { get; set; } = true;
    public double AudienceSingerPhotoSize { get; set; } = 64;
    public string AudienceSingerPhotoFit { get; set; } = "Fit";
    public string AudienceNextSingerFontFamily { get; set; } = "Segoe UI";
    public double AudienceNextSingerFontSize { get; set; } = 48;
    public double AudienceNextHeadingFontSize { get; set; } = 36;
    public string AudienceNextSingerPosition { get; set; } = "BottomCenter";
    public bool AudienceScrollerRotationEnabled { get; set; } = true;
    public bool AudienceScrollerMessageEnabled { get; set; } = true;
    public bool AudienceSecondScrollerEnabled { get; set; } = false;
    public string AudienceSecondScrollerText { get; set; } = "";
    public string AudienceSecondScrollerFontFamily { get; set; } = "Segoe UI";
    public double AudienceSecondScrollerFontSize { get; set; } = 30;
    public double AudienceSecondScrollerSpeed { get; set; } = 110;
    public double AudienceSecondScrollerInset { get; set; } = 0;
    public string AudienceSecondScrollerColor { get; set; } = "#FFFFD34D";
    public string AudienceKaraokeSizing { get; set; } = "Fit";
    public bool AudienceScrollerEnabled { get; set; } = true;
    public string AudienceScrollerText { get; set; } = "WELCOME TO KARAOKE WITH HAZZ • PLEASE HAVE YOUR NEXT SONG READY •";
    public string AudienceScrollerFontFamily { get; set; } = "Segoe UI";
    public double AudienceScrollerFontSize { get; set; } = 30;
    public double AudienceScrollerPixelsPerSecond { get; set; } = 110;
    public string AudienceScrollerPosition { get; set; } = "Bottom";
    public double AudienceScrollerEdgeInset { get; set; }

    public string AudienceKamikazeText { get; set; } = "KAMIKAZE KARAOKE!";
    public string AudienceKamikazeFontFamily { get; set; } = "Segoe UI Black";
    public double AudienceKamikazeFontSize { get; set; } = 84;
    public string AudienceKamikazeColor { get; set; } = "#FFFFD34D";
}



internal static class UiLayoutSettingsStore
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hazz Karaoke Hoster");
    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "ui-layout.json");
    private static readonly string PreSessionBackupPath = Path.Combine(SettingsFolder, "ui-layout.pre-session.json");
    private static bool _sessionBackupCreated;

    private static bool _canSave = true;
    private static readonly object Gate = new();

    public static UiLayoutSettings Load()
    {
        lock (Gate)
        {
            try
            {
                var settings = LoadFrom(SettingsPath);
                _canSave = true;
                // A small, one-time recovery point, never rotated away by
                // repeated saves while trying different skins.
                try
                {
                    if (File.Exists(SettingsPath) && !File.Exists(SettingsPath + ".before-skin-settings"))
                        File.Copy(SettingsPath, SettingsPath + ".before-skin-settings");
                }
                catch { /* A backup failure must not discard successfully loaded settings. */ }
                return settings;
            }
            catch
            {
                // A read error must not turn into a destructive defaults save.
                _canSave = false;
                return new UiLayoutSettings();
            }
        }
    }

    internal static UiLayoutSettings LoadFrom(string path)
    {
        var candidates = new[] { path, path + ".previous", Path.ChangeExtension(path, ".backup.json"), Path.ChangeExtension(path, ".pre-session.json") };
        if (!candidates.Any(File.Exists)) return new UiLayoutSettings();
        foreach (var candidate in candidates)
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                var settings = JsonSerializer.Deserialize<UiLayoutSettings>(File.ReadAllText(candidate));
                if (settings != null) return settings;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (JsonException) { }
        }
        throw new IOException("The interface settings and recovery copy could not be read. Existing files have been preserved.");
    }

    public static void SaveConsoleSkin(string skin)
    {
        lock (Gate)
        {
            if (!_canSave) return;
            try { PreserveSessionBackup(); SaveSkinTo(SettingsPath, skin); }
            catch { /* Keep the existing settings if the file is unavailable. */ }
        }
    }

    internal static void SaveSkinTo(string path, string skin)
    {
        // Preserve every field, including options written by a newer build.
        var document = JsonSerializer.SerializeToNode(LoadFrom(path))!.AsObject();
        document["ConsoleSkin"] = skin;
        WriteAtomic(path, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void Save(UiLayoutSettings settings)
    {
        lock (Gate)
        {
            if (!_canSave) return;
            try { PreserveSessionBackup(); SaveTo(SettingsPath, settings); }
            catch { /* Do not interrupt playback when storage is unavailable. */ }
        }
    }

    private static void PreserveSessionBackup()
    {
        if (_sessionBackupCreated) return;
        // LoadFrom already refuses unreadable settings. Back up the recovered
        // model rather than copying a damaged primary over the session copy.
        var settings = LoadFrom(SettingsPath);
        Directory.CreateDirectory(SettingsFolder);
        File.WriteAllText(PreSessionBackupPath, JsonSerializer.Serialize(settings));
        _sessionBackupCreated = true;
    }

    internal static void SaveTo(string path, UiLayoutSettings settings)
        => WriteAtomic(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));

    private static void WriteAtomic(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, json);
            if (File.Exists(path))
            {
                // Do not replace a good recovery copy with malformed JSON.
                bool valid;
                try { using var original = JsonDocument.Parse(File.ReadAllText(path)); valid = original.RootElement.ValueKind == JsonValueKind.Object; }
                catch (JsonException) { valid = false; }
                File.Replace(temp, path, valid ? path + ".previous" : path + ".unreadable", true);
            }
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
