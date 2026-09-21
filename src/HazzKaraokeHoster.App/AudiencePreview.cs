using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

// Samples the real WPF scene into bounded snapshots, including GIF frames and scrollers.
// A separate muted decoder mirrors native video, which VisualBrush cannot capture.
public sealed class AudiencePreview : Grid, IDisposable
{
    private readonly Grid _scene = new() { ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Rectangle _background = new();
    private readonly Rectangle _overlay = new();
    private AudienceVideoSurface? _video;
    private RenderTargetBitmap? _sceneFrame, _overlayFrame;
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
        _sceneFrame = _overlayFrame = null;
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
            _background.Fill = new ImageBrush { Stretch = Stretch.Fill };
            _overlay.Fill = new ImageBrush { Stretch = Stretch.Fill };
        }
        audience.SetPreviewRunning(true);
        var source = audience.PreviewScene;
        // Automatic VisualBrush bounds include off-screen scrolling content.
        // Always sample the display rectangle, not the extent of moving text.
        if (!double.IsFinite(source.ActualWidth) || !double.IsFinite(source.ActualHeight) ||
            source.ActualWidth <= 0 || source.ActualHeight <= 0) return;
        var bounds = new Rect(0, 0, source.ActualWidth, source.ActualHeight);

        var ratio = source.ActualWidth / source.ActualHeight;
        var width = Math.Max(1, Math.Min(ActualWidth, ActualHeight * ratio));
        _scene.Width = width; _scene.Height = width / ratio;
        // Do not attach a live brush of another Window to the host render tree.
        // Snapshot at a bounded size; the host GPU only receives a bitmap.
        var pixelWidth = Math.Clamp((int)Math.Ceiling(width), 1, 960);
        var pixelHeight = Math.Clamp((int)Math.Ceiling(pixelWidth / ratio), 1, 540);
        var playback = audience.PreviewVideo;
        var backgroundVideo = playback.Surface?.Source is null ? audience.PreviewBackgroundVideo : null;
        var mediaSource = playback.Surface?.Source ?? backgroundVideo?.Source;
        if (mediaSource is null)
        {
            Capture(source, bounds, pixelWidth, pixelHeight, ref _sceneFrame);
            ((ImageBrush)_background.Fill).ImageSource = _sceneFrame;
        }
        else
        {
            ((ImageBrush)_background.Fill).ImageSource = null;
            Capture(audience.PreviewOverlays, bounds, pixelWidth, pixelHeight, ref _overlayFrame,
                backgroundVideo is null ? null : audience.PreviewCdg);
            ((ImageBrush)_overlay.Fill).ImageSource = _overlayFrame;
        }
        _overlay.Visibility = mediaSource is null ? Visibility.Collapsed : Visibility.Visible;
        if (mediaSource is null)
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
        _video.UseLibVlc = playback.Surface?.NativeActive ?? false;
        _video.Opacity = backgroundVideo?.Opacity ?? 1;
        _video.StretchToFill = playback.Surface?.StretchToFill ?? (backgroundVideo?.Stretch == Stretch.Fill);
        _video.Tempo = playback.Surface?.Tempo ?? 1;
        var position = playback.Surface?.Position ?? backgroundVideo!.Position;
        var playing = backgroundVideo is not null || playback.Playing;
        var changed = _video.Source != mediaSource;
        if (changed) _video.Source = mediaSource;
        if (changed || !playing || Math.Abs((_video.Position - position).TotalMilliseconds) > 300)
            _video.Position = position;
        if (changed || _playing != playing)
        {
            // Opening paused media still needs one Play to initialize its video output.
            _video.Play();
            if (!playing) _video.Pause();
            _playing = playing;
        }
        MoveOverlay(_video.NativeActive);
    }
    private static void Capture(FrameworkElement source, Rect bounds, int width, int height, ref RenderTargetBitmap? frame, FrameworkElement? underneath = null)
    {
        if (frame is null || frame.PixelWidth != width || frame.PixelHeight != height)
            frame = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        else frame.Clear();
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            if (underneath is { Visibility: Visibility.Visible })
                dc.DrawRectangle(new VisualBrush(underneath) { AutoLayoutContent = false, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds, Stretch = Stretch.Fill }, null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new VisualBrush(source) { AutoLayoutContent = false,
                ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds, Stretch = Stretch.Fill },
                null, new Rect(0, 0, width, height));
        }
        frame.Render(drawing);
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
