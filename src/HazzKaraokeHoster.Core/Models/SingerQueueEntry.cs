using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HazzKaraokeHoster.Core.Models;

public sealed class SingerQueueEntry : INotifyPropertyChanged
{
    private string _singerName = string.Empty;
    private bool _isHeld;

    public SingerQueueEntry()
    {
        Songs.CollectionChanged += Songs_CollectionChanged;
    }

    public Guid Id { get; init; } = Guid.NewGuid();
    public long? SingerId { get; init; }

    public string SingerName
    {
        get => _singerName;
        set { if (_singerName == value) return; _singerName = value; OnPropertyChanged(); }
    }

    public ObservableCollection<SingerSongEntry> Songs { get; } = new();

    public bool IsHeld
    {
        get => _isHeld;
        set
        {
            if (_isHeld == value) return;
            _isHeld = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public string StatusText => IsHeld ? "HOLD" : string.Empty;

    public SingerSongEntry? NextSong => Songs.FirstOrDefault();
    public string NextSongTitle => NextSong?.SongTitle ?? string.Empty;
    public string NextArtist => NextSong?.Artist ?? string.Empty;
    public int NextKey => NextSong?.KeyChange ?? 0;
    public double NextSync => NextSong?.CdgSyncSeconds ?? 0;
    public int SongCount => Songs.Count;
    public string SongCountText => Songs.Count == 1 ? "1 song" : $"{Songs.Count} songs";

    public void RefreshSongSummary()
    {
        OnPropertyChanged(nameof(NextSong));
        OnPropertyChanged(nameof(NextSongTitle));
        OnPropertyChanged(nameof(NextArtist));
        OnPropertyChanged(nameof(NextKey));
        OnPropertyChanged(nameof(NextSync));
        OnPropertyChanged(nameof(SongCount));
        OnPropertyChanged(nameof(SongCountText));
    }

    private void Songs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (SingerSongEntry song in e.OldItems)
                song.PropertyChanged -= Song_PropertyChanged;
        if (e.NewItems is not null)
            foreach (SingerSongEntry song in e.NewItems)
                song.PropertyChanged += Song_PropertyChanged;
        RefreshSongSummary();
    }

    private void Song_PropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshSongSummary();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
