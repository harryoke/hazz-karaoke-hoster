using System.ComponentModel;
using System.Runtime.CompilerServices;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public sealed class MusicQueueItem : INotifyPropertyChanged
{
    private int _number;
    private bool _isNowPlaying;
    private bool _isPlayedThisSession;
    private TimeSpan? _duration;
    private bool _isFavourite;
    public bool IsFavourite
    {
        get => _isFavourite;
        set { _isFavourite = value; OnPropertyChanged(); OnPropertyChanged(nameof(NumberText)); }
    }

    public long? SongId { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public async Task RefreshTagsAsync()
    {
        var version = ++_tagReadVersion;
        var tags = await HazzKaraokeHoster.Core.Mp3Metadata.ReadAsync(FilePath, Artist, Title);
        if (version != _tagReadVersion) return;
        Artist = tags.Artist;
        Title = tags.Title;
        OnPropertyChanged(nameof(Artist)); OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(DisplayArtist)); OnPropertyChanged(nameof(DisplayTitle));
    }
    private int _tagReadVersion;

    public int Number
    {
        get => _number;
        set { if (_number == value) return; _number = value; OnPropertyChanged(); OnPropertyChanged(nameof(NumberText)); }
    }

    public bool IsNowPlaying
    {
        get => _isNowPlaying;
        set { if (_isNowPlaying == value) return; _isNowPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(NumberText)); }
    }

    public bool IsPlayedThisSession
    {
        get => _isPlayedThisSession;
        set { if (_isPlayedThisSession == value) return; _isPlayedThisSession = value; OnPropertyChanged(); }
    }

    public TimeSpan? Duration
    {
        get => _duration;
        set { if (_duration == value) return; _duration = value; OnPropertyChanged(); OnPropertyChanged(nameof(DurationText)); }
    }

    public string NumberText => IsNowPlaying ? $"{Number} ▶" : Number.ToString();
    public string DisplayArtist => string.IsNullOrWhiteSpace(Artist) ? "Unknown Artist" : Artist;
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Path.GetFileNameWithoutExtension(FilePath) : Title;
    public string DurationText => Duration is TimeSpan d && d > TimeSpan.Zero
        ? (d.TotalHours >= 1 ? $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}" : $"{(int)d.TotalMinutes:00}:{d.Seconds:00}")
        : "--:--";

    public static MusicQueueItem FromSong(SongRecord song) => new()
    {
        SongId = song.Id,
        FilePath = song.FilePath,
        Artist = song.Artist,
        Title = song.Title
    };

    public static MusicQueueItem FromPath(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path).Trim();
        var artist = string.Empty;
        var title = stem;
        var marker = stem.IndexOf(" - ", StringComparison.Ordinal);
        if (marker > 0 && marker < stem.Length - 3)
        {
            artist = stem[..marker].Trim();
            title = stem[(marker + 3)..].Trim();
        }
        return new MusicQueueItem { FilePath = path, Artist = artist, Title = title };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
