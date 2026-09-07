using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HazzKaraokeHoster.Core.Models;

public sealed class SingerSongEntry : INotifyPropertyChanged
{
    private string _songTitle = string.Empty;
    private string _artist = string.Empty;
    private string _filePath = string.Empty;
    private int _keyChange;
    private double _cdgSyncSeconds;

    public Guid Id { get; init; } = Guid.NewGuid();
    public long? SongId { get; set; }

    public string SongTitle
    {
        get => _songTitle;
        set { if (_songTitle == value) return; _songTitle = value; OnPropertyChanged(); }
    }

    public string Artist
    {
        get => _artist;
        set { if (_artist == value) return; _artist = value; OnPropertyChanged(); }
    }

    public string FilePath
    {
        get => _filePath;
        set { if (_filePath == value) return; _filePath = value; OnPropertyChanged(); }
    }

    public int KeyChange
    {
        get => _keyChange;
        set
        {
            var clamped = Math.Clamp(value, -6, 6);
            if (_keyChange == clamped) return;
            _keyChange = clamped;
            OnPropertyChanged();
        }
    }

    public double CdgSyncSeconds
    {
        get => _cdgSyncSeconds;
        set
        {
            var snapped = Math.Clamp(Math.Round(value * 4.0) / 4.0, -10.0, 10.0);
            if (Math.Abs(_cdgSyncSeconds - snapped) < 0.0001) return;
            _cdgSyncSeconds = snapped;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
