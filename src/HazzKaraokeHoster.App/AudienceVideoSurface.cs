using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

// Audience-only decoder. Audio and the private preview remain on
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
    private bool _mediaOpenedRaised;
    private int _generation;
    // All decoder calls are serialized off the dispatcher, including clock queries.
    // LibVLC can wait for codecs, devices or its render thread in any of these calls.
    private readonly SerialMediaCommands _commands = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private TimeSpan? _duration;
    private TimeSpan _position;
    private bool _nativeRequested, _pollPending;
    private int _nativeGeneration;
    private bool _stretchToFill;
    public bool StretchToFill
    {
        get => _stretchToFill;
        set { _stretchToFill = value; ApplySizing(); }
    }
    private void ApplySizing()
    {
        _windows.Stretch = _stretchToFill ? Stretch.Fill : Stretch.Uniform;
        if (!NativeActive) return;
        var aspect = _stretchToFill && ActualWidth > 0 && ActualHeight > 0
            ? $"{(int)Math.Round(ActualWidth)}:{(int)Math.Round(ActualHeight)}" : null;
        NativeCommand(() => { if (_player is not null) _player.AspectRatio = aspect; });
    }
    public bool UseLibVlc { get; set; }
    private double _tempo = 1;
    public double Tempo
    {
        get => _tempo;
        set
        {
            var tempo = double.IsFinite(value) ? Math.Clamp(value, 0.75, 1.25) : 1;
            if (_tempo == tempo) return;
            _tempo = tempo;
            _windows.SpeedRatio = _tempo;
            var rate = (float)_tempo;
            if (NativeActive) NativeCommand(() =>
            {
                if (_player is not null && _player.IsPlaying && _player.SetRate(rate) == -1)
                    throw new InvalidOperationException("LibVLC refused the selected tempo.");
            });
        }
    }
    public static bool WindowsRequested => Environment.GetCommandLineArgs().Contains("--windows-video", StringComparer.OrdinalIgnoreCase);
    public bool NativeActive => _nativeRequested && !_fallback && !_closed;
    public event EventHandler? MediaOpened;
    public event EventHandler? MediaEnded;
    public event EventHandler? BackendChanged;
    public event EventHandler? MediaFailed;
    public object? Overlay { get => _overlayHost.Content; set => _overlayHost.Content = value; }

    public AudienceVideoSurface()
    {
        // VideoView transfers Content to its foreground window and resets its
        // own Content to null. Keep a stable host so clearing really detaches.
        _clock.Tick += (_, _) => PollClock();
        SizeChanged += (_, _) => ApplySizing();
        _view.Content = _overlayHost;
        Children.Add(_windows); Children.Add(_view);
        _windows.MediaOpened += (_, _) =>
        {
            _windows.Position = _requestedPosition;
            _windows.SpeedRatio = _tempo;
            RaiseMediaOpenedOnce();
        };
        _windows.MediaEnded += (_, _) => MediaEnded?.Invoke(this, EventArgs.Empty);
        _windows.MediaFailed += (_, e) => { App.WriteDiagnostic("WINDOWS VIDEO", e.ErrorException.ToString()); MediaFailed?.Invoke(this, EventArgs.Empty); };
    }
    public Uri? Source
    {
        get => _source;
        set
        {
            if (_source == value) return;
            Stop(); _generation++; _source = value; _requestedPosition = TimeSpan.Zero; _mediaOpenedRaised = false;
            _windows.Source = null;
            if (value is null) return;
            // Apply a preference at a source boundary, never during a song.
            var useNative = UseLibVlc && !WindowsRequested;
            if (!useNative || _fallback) ReleaseNative();
            _fallback = false;
            _nativeRequested = useNative;
            _duration = null;
            _position = TimeSpan.Zero;
            if (useNative) EnsureBackend();
            if (!NativeActive) _windows.Source = value;
            BackendChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public TimeSpan? Duration => NativeActive ? _duration
        : _windows.NaturalDuration.HasTimeSpan ? _windows.NaturalDuration.TimeSpan : null;
    public TimeSpan Position
    {
        get => NativeActive ? _position : _windows.Position;
        set
        {
            _requestedPosition = value < TimeSpan.Zero ? TimeSpan.Zero : value;
            _position = _requestedPosition;
            var position = _requestedPosition;
            if (NativeActive) NativeCommand(() =>
            {
                if (_player?.IsSeekable == true) _player.Time = (long)position.TotalMilliseconds;
            });
            else _windows.Position = position;
        }
    }
    private void EnsureBackend()
    {
        var generation = _generation;
        _commands.Enqueue(async () =>
        {
            if (_closed || generation != _generation) return;
            try
            {
                if (_player is null)
                {
                    LibVLCSharp.Shared.Core.Initialize(Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64"));
                    _vlc = new LibVLC("--no-audio", "--no-video-title-show", "--no-snapshot-preview");
                    _player = new LibVLCSharp.Shared.MediaPlayer(_vlc) { Mute = true, EnableHardwareDecoding = true };
                    _player.Playing += OnPlaying;
                    _player.EndReached += OnEnded;
                    _player.EncounteredError += OnError;
                }
                // Attach the HWND on its owning dispatcher before starting the decoder.
                if (_closed || Dispatcher.HasShutdownStarted) return;
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_closed || generation != _generation) return;
                    _view.MediaPlayer = _player;
                    _windows.Visibility = Visibility.Collapsed; _view.Visibility = Visibility.Visible;
                    _clock.Start();
                    ApplySizing();
                    BackendChanged?.Invoke(this, EventArgs.Empty);
                });
            }
            catch (Exception ex) { Post(generation, () => UseWindows(ex.Message)); }
        });
    }
    private void NativeCommand(Action action)
    {
        var generation = _generation;
        _commands.Enqueue(() =>
        {
            if (_closed || generation != _generation || !NativeActive) return;
            try { action(); }
            catch (Exception ex) { Post(generation, () => UseWindows(ex.Message)); }
        });
    }
    private void Post(int generation, Action action)
    {
        if (_closed || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(new Action(() => { if (!_closed && generation == _generation) action(); }));
    }
    private void PollClock()
    {
        if (!NativeActive || !_started || _pollPending) return;
        _pollPending = true;
        var generation = _generation;
        _commands.Enqueue(() =>
        {
            try
            {
                if (generation != _generation || _closed || _player is null) return;
                var time = _player.Time;
                var length = _player.Length;
                Post(generation, () =>
                {
                    if (time >= 0) _position = TimeSpan.FromMilliseconds(time);
                    if (length > 0) _duration = TimeSpan.FromMilliseconds(length);
                });
            }
            catch (Exception ex) { Post(generation, () => UseWindows(ex.Message)); }
            finally
            {
                if (!Dispatcher.HasShutdownStarted)
                    Dispatcher.BeginInvoke(new Action(() => _pollPending = false));
            }
        });
    }
    private void OnPlaying(object? sender, EventArgs e)
    {
        var generation = _nativeGeneration;
        Post(generation, () =>
        {
            if (!NativeActive) return;
            var first = !_started;
            _starting = false; _started = true;
            var position = _requestedPosition;
            var rate = (float)_tempo;
            var paused = !_playing;
            NativeCommand(() =>
            {
                if (_player is null) return;
                if (first && position > TimeSpan.Zero) _player.Time = (long)position.TotalMilliseconds;
                if (_player.SetRate(rate) == -1) throw new InvalidOperationException("LibVLC refused the selected tempo.");
                _player.SetPause(paused);
            });
            ApplySizing();
            RaiseMediaOpenedOnce();
        });
    }
    private void RaiseMediaOpenedOnce()
    {
        if (_mediaOpenedRaised) return;
        _mediaOpenedRaised = true;
        MediaOpened?.Invoke(this, EventArgs.Empty);
    }
    private void OnEnded(object? sender, EventArgs e) => Post(_nativeGeneration, () =>
    {
        _playing = false; _started = false; _starting = false;
        _requestedPosition = _position = TimeSpan.Zero;
        MediaEnded?.Invoke(this, EventArgs.Empty);
    });
    private void OnError(object? sender, EventArgs e) => Post(_nativeGeneration, () => UseWindows("LibVLC could not open/play " + _source));
    private void UseWindows(string reason)
    {
        var resume = _playing; var position = Position;
        _generation++; _fallback = true;
        ReleaseNative(); _started = false; _starting = false;
        _requestedPosition = position;
        _view.Visibility = Visibility.Collapsed; _windows.Visibility = Visibility.Visible;
        _windows.Source = _source; _windows.Position = position;
        _windows.SpeedRatio = _tempo;
        App.WriteDiagnostic("LIBVLC FALLBACK", reason);
        BackendChanged?.Invoke(this, EventArgs.Empty);
        if (resume) _windows.Play();
    }
    public void Play()
    {
        if (_closed || _source is null) return;
        _playing = true;
        if (NativeActive) _clock.Start();
        if (!NativeActive) { _windows.Play(); return; }
        if (_started) { NativeCommand(() => _player?.SetPause(false)); return; }
        if (_starting) return;
        EnsureBackend();
        _starting = true;
        var source = _source;
        var generation = _generation;
        NativeCommand(() =>
        {
            if (_player is null || _vlc is null) return;
            _nativeGeneration = generation;
            using var media = new Media(_vlc, source);
            media.AddOption(":no-audio");
            if (!_player.Play(media)) throw new InvalidOperationException("LibVLC Play returned false.");
        });
    }
    public void Pause()
    {
        _playing = false;
        if (NativeActive) NativeCommand(() => _player?.SetPause(true)); else _windows.Pause();
    }
    public void Stop()
    {
        _generation++; _playing = false; _started = false; _starting = false;
        _requestedPosition = _position = TimeSpan.Zero;
        _clock.Stop();
        // Stop is ordered before subsequent initialization/play, never skipped as stale.
        _commands.Enqueue(() => { try { _player?.Stop(); } catch (Exception ex) { App.WriteDiagnostic("VIDEO STOP", ex.Message); } });
        _windows.Stop();
    }
    public void Close()
    {
        if (_closed) return;
        _closed = true; _generation++;
        _clock.Stop();
        ReleaseNative();
        _windows.Close();
    }
    private void ReleaseNative()
    {
        _clock.Stop();
        _commands.Enqueue(async () =>
        {
            var player = _player; var vlc = _vlc;
            _player = null; _vlc = null;
            if (player is not null)
            {
                player.Playing -= OnPlaying; player.EndReached -= OnEnded; player.EncounteredError -= OnError;
                try { player.Stop(); } catch (Exception ex) { App.WriteDiagnostic("VIDEO STOP", ex.Message); }
            }
            // VideoView's setter calls into the player. Only detach after native
            // work finishes, otherwise even assigning null can block the dispatcher.
            if (!Dispatcher.HasShutdownStarted)
                await Dispatcher.InvokeAsync(() =>
                {
                    _view.MediaPlayer = null;
                    if (_closed) _view.Dispose();
                });
            try { player?.Dispose(); } finally { vlc?.Dispose(); }
        });
        _view.Visibility = Visibility.Collapsed;
        _windows.Visibility = Visibility.Visible;
    }
}
