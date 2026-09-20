using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

// Shares the real WPF scene, including GIF frames and scroller positions.
// A separate muted decoder mirrors native video, which VisualBrush cannot capture.
public sealed class AudiencePreview : Grid, IDisposable
{
    private readonly Grid _scene = new() { ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Rectangle _background = new();
    private readonly Rectangle _overlay = new();
    private AudienceVideoSurface? _video;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private AudienceWindow? _audience;
    private bool _playing, _disposed;
    public Func<AudienceWindow>? GetAudience { get; set; }
    public AudiencePreview()
    {
        Background = Brushes.Black;
        ClipToBounds = true;
        _scene.Children.Add(_background);
        _scene.Children.Add(_overlay);
        Children.Add(_scene);
        _timer.Tick += (_, _) => RefreshSafe();
        Loaded += (_, _) => { if (!_disposed && IsVisible) { _timer.Start(); RefreshSafe(); } };
        IsVisibleChanged += (_, _) => { if (IsVisible && !_disposed) _timer.Start(); else Suspend(); };
        Unloaded += (_, _) => Suspend();
    }
    private void Suspend()
    {
        _timer.Stop();
        _audience?.SetPreviewRunning(false);
        if (_video is not null)
        {
            _video.Overlay = null;
            _video.Close(); _scene.Children.Remove(_video); _video = null;
        }
        _playing = false;
        _background.Fill = null; _overlay.Fill = null; _audience = null;
    }
    private void RefreshSafe()
    {
        if (_disposed || !IsVisible || GetAudience is null) return;
        try { Refresh(); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Preview failure must never stop show playback or repeat on every tick.
            App.WriteDiagnostic("AUDIENCE PREVIEW", ex.ToString());
            Suspend();
        }
    }
    public void Refresh()
    {
        if (_disposed || GetAudience is null) return;
        var audience = GetAudience();
        if (_audience != audience)
        {
            _audience?.SetPreviewRunning(false);
            _audience = audience;
            _background.Fill = new VisualBrush(audience.PreviewScene) { Stretch = Stretch.Fill, AutoLayoutContent = false };
            _overlay.Fill = new VisualBrush(audience.PreviewOverlays) { Stretch = Stretch.Fill, AutoLayoutContent = false };
        }
        audience.SetPreviewRunning(true);
        var source = audience.PreviewScene;
        var ratio = source.ActualWidth / Math.Max(1, source.ActualHeight);
        var width = Math.Max(1, Math.Min(ActualWidth, ActualHeight * ratio));
        _scene.Width = width; _scene.Height = width / ratio;
        var playback = audience.PreviewVideo;
        _overlay.Visibility = playback.Surface?.Source is null ? Visibility.Collapsed : Visibility.Visible;
        if (playback.Surface?.Source is null)
        {
            if (_video is not null) { _video.Overlay = null; _video.Source = null; _video.Visibility = Visibility.Collapsed; }
            MoveOverlay(false); _playing = false; return;
        }
        if (_video is null)
        {
            _video = new AudienceVideoSurface();
            _video.BackendChanged += (_, _) => MoveOverlay(_video?.NativeActive == true);
            _scene.Children.Insert(1, _video);
        }
        _video.Visibility = Visibility.Visible;
        _video.UseLibVlc = playback.Surface.NativeActive;
        _video.StretchToFill = playback.Surface.StretchToFill;
        _video.Tempo = playback.Surface.Tempo;
        var changed = _video.Source != playback.Surface.Source;
        if (changed) _video.Source = playback.Surface.Source;
        if (changed || !playback.Playing || Math.Abs((_video.Position - playback.Surface.Position).TotalMilliseconds) > 300)
            _video.Position = playback.Surface.Position;
        if (changed || _playing != playback.Playing)
        {
            // Opening paused media still needs one Play to initialize its video output.
            _video.Play();
            if (!playback.Playing) _video.Pause();
            _playing = playback.Playing;
        }
        MoveOverlay(_video.NativeActive);
    }
    private void MoveOverlay(bool native)
    {
        if (native && _video is not null)
        {
            _scene.Children.Remove(_overlay);
            if (_video.Overlay != _overlay) _video.Overlay = _overlay;
        }
        else
        {
            if (_video is not null) _video.Overlay = null;
            if (!_scene.Children.Contains(_overlay)) _scene.Children.Add(_overlay);
        }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; Suspend(); }
}
