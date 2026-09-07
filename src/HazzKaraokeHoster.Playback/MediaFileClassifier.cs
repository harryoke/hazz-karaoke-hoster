namespace HazzKaraokeHoster.Playback;

public enum HazzMediaKind { Unknown, ZipKaraoke, CdgGraphics, Mp3Audio, Video, Audio }

public static class MediaFileClassifier
{
    private static readonly HashSet<string> Video = new(StringComparer.OrdinalIgnoreCase){".mp4",".mkv",".avi",".mov",".mpeg",".mpg",".wmv",".m4v",".vob",".ts",".m2ts",".webm",".divx"};
    private static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase){".mp3",".wav",".wma",".m4a",".aac",".flac",".ogg",".aif",".aiff"};
    public static HazzMediaKind Classify(string path)
    {
        var ext=Path.GetExtension(path);
        if (ext.Equals(".zip",StringComparison.OrdinalIgnoreCase)) return HazzMediaKind.ZipKaraoke;
        if (ext.Equals(".cdg",StringComparison.OrdinalIgnoreCase)) return HazzMediaKind.CdgGraphics;
        if (ext.Equals(".mp3",StringComparison.OrdinalIgnoreCase)) return HazzMediaKind.Mp3Audio;
        if (Video.Contains(ext)) return HazzMediaKind.Video;
        if (Audio.Contains(ext)) return HazzMediaKind.Audio;
        return HazzMediaKind.Unknown;
    }
}
