using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HazzKaraokeHoster.Core.Models;

public sealed class SingerQueueEntry : INotifyPropertyChanged
{
    private string _singerName = string.Empty;
    private bool _isHeld;
    private int? _rotationPosition;
    private int _rotationTotal;
    private bool _isNextSinger;

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

    public int? RotationPosition => _rotationPosition;
    public int RotationTotal => _rotationTotal;
    public bool IsNextSinger => _isNextSinger;
    public string RotationPositionText
        => IsHeld ? "HOLD"
            : RotationPosition is int position && RotationTotal > 0 ? $"{position} / {RotationTotal}"
            : "—";
    public string RotationBadgeText => IsNextSinger ? $"NEXT  {RotationPositionText}" : RotationPositionText;

    public void SetRotationStanding(int? position, int total, bool isNext)
    {
        var changedPosition = _rotationPosition != position;
        var changedTotal = _rotationTotal != total;
        var changedNext = _isNextSinger != isNext;
        if (!changedPosition && !changedTotal && !changedNext) return;

        _rotationPosition = position;
        _rotationTotal = Math.Max(0, total);
        _isNextSinger = isNext;

        if (changedPosition) OnPropertyChanged(nameof(RotationPosition));
        if (changedTotal) OnPropertyChanged(nameof(RotationTotal));
        if (changedNext) OnPropertyChanged(nameof(IsNextSinger));
        OnPropertyChanged(nameof(RotationPositionText));
        OnPropertyChanged(nameof(RotationBadgeText));
    }

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
        OnPropertyChanged(nameof(RotationPositionText));
        OnPropertyChanged(nameof(RotationBadgeText));
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
