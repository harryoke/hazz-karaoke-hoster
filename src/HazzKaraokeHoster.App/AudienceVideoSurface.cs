using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

namespace HazzKaraokeHoster.App;

// Experimental audience-only decoder. Audio and the private preview remain on
// their existing engines so sound-device routing and pitch control are preserved.
public sealed class AudienceVideoSurface : Grid
{
    private readonly MediaElement _windows = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, IsMuted = true, Volume = 0, Stretch = Stretch.Uniform };
    private readonly VideoView _view = new() { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private readonly ContentControl _overlayHost = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private LibVLC? _vlc;
    private LibVLCSharp.Shared.MediaPlayer? _player;
    private Uri? _source;
    private TimeSpan _requestedPosition;
    private bool _started, _starting, _playing, _closed, _fallback;
    private int _generation;
    public static bool WindowsRequested => Environment.GetCommandLineArgs().Contains("--windows-video", StringComparer.OrdinalIgnoreCase);
    public bool NativeActive => _player is not null && !_fallback && !_closed;
    public event EventHandler? MediaEnded;
    public event EventHandler? BackendChanged;
    public event EventHandler? MediaFailed;
    public object? Overlay { get => _overlayHost.Content; set => _overlayHost.Content = value; }

    public AudienceVideoSurface()
    {
        // VideoView transfers Content to its foreground window and resets its
        // own Content to null. Keep a stable host so clearing really detaches.
        _view.Content = _overlayHost;
        Children.Add(_windows); Children.Add(_view);
        _windows.MediaOpened += (_, _) => _windows.Position = _requestedPosition;
        _windows.MediaEnded += (_, _) => MediaEnded?.Invoke(this, EventArgs.Empty);
        _windows.MediaFailed += (_, e) => { App.WriteDiagnostic("WINDOWS VIDEO", e.ErrorException.ToString()); MediaFailed?.Invoke(this, EventArgs.Empty); };
    }
    public Uri? Source
    {
        get => _source;
        set
        {
            if (_source == value) return;
            Stop(); _generation++; _source = value; _requestedPosition = TimeSpan.Zero;
            _windows.Source = null;
            if (value is null) return;
            EnsureBackend();
            if (!NativeActive) _windows.Source = value;
        }
    }
    public TimeSpan Position
    {
        get => NativeActive ? (_started && _player!.Time >= 0 ? TimeSpan.FromMilliseconds(_player.Time) : _requestedPosition) : _windows.Position;
        set
        {
            _requestedPosition = value < TimeSpan.Zero ? TimeSpan.Zero : value;
            if (NativeActive) { if (_started && _player!.IsSeekable) _player.Time = (long)_requestedPosition.TotalMilliseconds; }
            else _windows.Position = _requestedPosition;
        }
    }
    private void EnsureBackend()
    {
        if (_closed || _fallback || _player is not null || WindowsRequested) return;
        try
        {
            LibVLCSharp.Shared.Core.Initialize(Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64"));
            _vlc = new LibVLC("--no-audio", "--no-video-title-show", "--no-snapshot-preview");
            _player = new LibVLCSharp.Shared.MediaPlayer(_vlc) { Mute = true, EnableHardwareDecoding = true };
            _player.Playing += OnPlaying;
            _player.EndReached += OnEnded;
            _player.EncounteredError += OnError;
            _view.MediaPlayer = _player;
            _windows.Visibility = Visibility.Collapsed; _view.Visibility = Visibility.Visible;
            App.WriteDiagnostic("LIBVLC", "Audience decoder initialized; hardware decoding enabled; audio disabled.");
            BackendChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { UseWindows(ex.Message); }
    }
    private void Post(Action action)
    {
        var generation = _generation;
        if (_closed || Dispatcher.HasShutdownStarted) return;
        // Never synchronously invoke the UI from a LibVLC event: Stop/Dispose
        // may be waiting for that native event thread to finish.
        Dispatcher.BeginInvoke(new Action(() => { if (!_closed && generation == _generation) action(); }));
    }
    private void OnPlaying(object? sender, EventArgs e) => Post(() =>
    {
        if (!NativeActive) return;
        if (!_started && _requestedPosition > TimeSpan.Zero) _player!.Time = (long)_requestedPosition.TotalMilliseconds;
        _starting = false; _started = true;
        if (!_playing) _player!.SetPause(true);
    });
    private void OnEnded(object? sender, EventArgs e) => Post(() => { _playing = false; _started = false; _starting = false; _requestedPosition = TimeSpan.Zero; MediaEnded?.Invoke(this, EventArgs.Empty); });
    private void OnError(object? sender, EventArgs e) => Post(() => UseWindows("LibVLC could not open/play " + _source));
    private void UseWindows(string reason)
    {
        var resume = _playing; var position = Position;
        _generation++; _fallback = true;
        _player?.Stop(); _started = false; _starting = false;
        _requestedPosition = position;
        _view.Visibility = Visibility.Collapsed; _windows.Visibility = Visibility.Visible;
        _windows.Source = _source; _windows.Position = position;
        App.WriteDiagnostic("LIBVLC FALLBACK", reason);
        BackendChanged?.Invoke(this, EventArgs.Empty);
        if (resume) _windows.Play();
    }
    public void Play()
    {
        if (_closed || _source is null) return;
        _playing = true;
        if (!NativeActive) { _windows.Play(); return; }
        if (_started) { _player!.SetPause(false); return; }
        if (_starting) return;
        _starting = true;
        using var media = new Media(_vlc!, _source);
        media.AddOption(":no-audio");
        if (!_player!.Play(media)) UseWindows("LibVLC Play returned false.");
    }
    public void Pause()
    {
        _playing = false;
        if (NativeActive) _player!.SetPause(true); else _windows.Pause();
    }
    public void Stop()
    {
        _generation++; _playing = false; _started = false; _starting = false; _requestedPosition = TimeSpan.Zero;
        _player?.Stop(); _windows.Stop();
    }
    public void Close()
    {
        if (_closed) return;
        _closed = true; _generation++;
        _view.MediaPlayer = null;
        if (_player is not null)
        {
            _player.Playing -= OnPlaying; _player.EndReached -= OnEnded; _player.EncounteredError -= OnError;
            _player.Stop(); _player.Dispose(); _player = null;
        }
        _vlc?.Dispose(); _vlc = null; _view.Dispose(); _windows.Close();
    }
}
