using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class AudienceWindow
{
    private readonly DispatcherTimer _slideshowTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private CancellationTokenSource? _slideshowCts;
    private string _slideshowFolder = string.Empty;
    private string _lastSlide = string.Empty;
    private bool _slideLoading;
    private bool _slideshowHooked;
    private string _backgroundVideoPath = string.Empty;
    private string _backgroundGifPath = string.Empty;
    private static bool IsBackgroundGif(string path) => string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);

    private double _backgroundGifSpeed = 1;
    private CancellationTokenSource? _gifLoadCts;
    private MemoryStream? _gifStream;
    private const long MaximumGifBytes = 128L * 1024 * 1024;

    private async void SetBackgroundGif(string path)
    {
        if (string.Equals(path, _backgroundGifPath, StringComparison.OrdinalIgnoreCase)) return;
        _gifLoadCts?.Cancel();
        XamlAnimatedGif.AnimationBehavior.SetSourceStream(SingerBackgroundGif, null);
        _gifStream?.Dispose();
        _gifStream = null;
        SingerBackgroundGif.Source = null;
        _backgroundGifPath = path;
        if (path.Length == 0) return;
        using var cts = new CancellationTokenSource();
        _gifLoadCts = cts;
        var speed = _backgroundGifSpeed;
        try
        {
            var fileLength = new FileInfo(path).Length;
            if (fileLength > MaximumGifBytes)
                throw new IOException("GIF is larger than the 128 MB live-show safety limit.");
            var bytes = await File.ReadAllBytesAsync(path, cts.Token);
            await Task.Run(() => GifPlaybackTiming.ScaleDelays(bytes, speed), cts.Token);
            if (cts.IsCancellationRequested) return;
            _gifStream = new MemoryStream(bytes, writable: false);
            XamlAnimatedGif.AnimationBehavior.SetRepeatBehavior(SingerBackgroundGif, System.Windows.Media.Animation.RepeatBehavior.Forever);
            XamlAnimatedGif.AnimationBehavior.SetSourceStream(SingerBackgroundGif, _gifStream);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            App.WriteDiagnostic("AUDIENCE GIF", $"{path}: {ex.Message}");
            // Leave a blank background for an unreadable GIF; the slideshow still advances.
        }
        finally { if (ReferenceEquals(_gifLoadCts, cts)) _gifLoadCts = null; }
    }
    private static bool IsBackgroundVideo(string path) => new[] { ".mp4", ".m4v", ".wmv", ".avi", ".mov", ".mkv", ".webm", ".mpg", ".mpeg" }
        .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private void SetBackgroundVideo(string path)
    {
        if (_backgroundVideoPath == path) return;
        SingerBackgroundVideo.Close();
        SingerBackgroundVideo.Source = null;
        _backgroundVideoPath = path;
        if (path.Length > 0)
        {
            SingerBackgroundVideo.IsMuted = true;
            SingerBackgroundVideo.Volume = 0;
            SingerBackgroundVideo.Source = new Uri(path, UriKind.Absolute);
            SingerBackgroundVideo.Play();
        }
    }

    private void BackgroundVideo_Ended(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_backgroundVideoPath.Length == 0) return;
        SingerBackgroundVideo.Position = TimeSpan.Zero;
        SingerBackgroundVideo.Play();
    }

    private void BackgroundVideo_Failed(object sender, System.Windows.ExceptionRoutedEventArgs e)
    {
        // Keep the one-minute slot, then advance; never interrupt the show with a media dialog.
        SetBackgroundVideo(string.Empty);
        SetBackgroundGif(string.Empty);
        UpdateOverlayLayerVisibility();
    }

    private void StopBackgroundSlideshow()
    {
        _slideshowTimer.Stop();
        _slideshowCts?.Cancel();
        _slideshowCts?.Dispose();
        _slideshowCts = null;
        _slideshowFolder = string.Empty;
        _lastSlide = string.Empty;
        SetBackgroundVideo(string.Empty);
        SetBackgroundGif(string.Empty);
    }

    private void ConfigureBackgroundSlideshow(AudienceOverlaySettings settings)
    {
        var speed = double.IsFinite(settings.BackgroundGifSpeed) ? Math.Clamp(settings.BackgroundGifSpeed, 0.25, 4) : 1;
        if (_backgroundGifSpeed != speed)
        {
            _backgroundGifSpeed = speed;
            var gifPath = _backgroundGifPath;
            SetBackgroundGif(string.Empty);
            if (gifPath.Length > 0) SetBackgroundGif(gifPath);
        }
        var folder = settings.BackgroundImageEnabled ? settings.BackgroundFolderPath : string.Empty;
        if (string.IsNullOrWhiteSpace(folder))
        {
            var gif = settings.BackgroundImageEnabled && IsBackgroundGif(settings.BackgroundImagePath)
                ? settings.BackgroundImagePath : string.Empty;
            if (_slideshowFolder.Length == 0 && gif.Length > 0 && gif == _backgroundGifPath) return;
            if (gif.Length > 0)
            {
                StopBackgroundSlideshow();
                _backgroundImagePath = "\0";
                SingerBackgroundImage.Source = null;
                SetBackgroundGif(gif);
                return;
            }
            var video = settings.BackgroundImageEnabled && IsBackgroundVideo(settings.BackgroundImagePath)
                ? settings.BackgroundImagePath : string.Empty;
            if (_slideshowFolder.Length == 0 && video.Length > 0 && video == _backgroundVideoPath) return;
            if (_slideshowFolder.Length > 0) _backgroundImagePath = "\0"; // Force replacement of the slideshow bitmap.
            StopBackgroundSlideshow();
            SetBackgroundVideo(video);
            if (video.Length > 0) SingerBackgroundImage.Source = null;
            SetCachedImage(SingerBackgroundImage, video.Length > 0 ? string.Empty : settings.BackgroundImagePath, ref _backgroundImagePath);
            return;
        }
        if (string.Equals(folder, _slideshowFolder, StringComparison.OrdinalIgnoreCase)) return;
        StopBackgroundSlideshow();
        _slideshowFolder = folder;
        _backgroundImagePath = string.Empty;
        SingerBackgroundImage.Source = null;
        _slideshowCts = new CancellationTokenSource();
        if (!_slideshowHooked)
        {
            _slideshowTimer.Tick += async (_, _) =>
            {
                try { await AdvanceBackgroundSlideAsync(); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { App.WriteDiagnostic("AUDIENCE SLIDESHOW", ex.ToString()); }
            };
            _slideshowHooked = true;
        }
        _ = AdvanceBackgroundSlideAsync();
        _slideshowTimer.Start();
    }

    private async Task AdvanceBackgroundSlideAsync()
    {
        if (_slideLoading || _slideshowCts is null) return;
        _slideLoading = true;
        var token = _slideshowCts.Token;
        var folder = _slideshowFolder;
        var previous = _lastSlide;
        try
        {
            var slide = await Task.Run(() =>
            {
                var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp" };
                var files = Directory.EnumerateFiles(folder).Where(p => extensions.Contains(Path.GetExtension(p)) || IsBackgroundVideo(p))
                    .Take(10_000).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
                var start = Array.FindIndex(files, p => string.Equals(p, previous, StringComparison.OrdinalIgnoreCase)) + 1;
                for (var offset = 0; offset < files.Length; offset++)
                {
                    token.ThrowIfCancellationRequested();
                    var path = files[(start + offset) % files.Length];
                    if (IsBackgroundVideo(path)) return (Path: path, Image: (BitmapSource?)null);
                    try
                    {
                        using var stream = File.OpenRead(path);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 3840;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return (Path: path, Image: (BitmapSource?)bitmap);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.IO.FileFormatException or ArgumentException)
                    { /* Skip unreadable images and try the next file. */ }
                }
                return (Path: string.Empty, Image: (BitmapSource?)null);
            }, token);
            if (token.IsCancellationRequested) return;
            _lastSlide = slide.Path;
            SetBackgroundVideo(IsBackgroundVideo(slide.Path) ? slide.Path : string.Empty);
            SetBackgroundGif(IsBackgroundGif(slide.Path) ? slide.Path : string.Empty);
            SingerBackgroundImage.Source = IsBackgroundGif(slide.Path) ? null : slide.Image;
            // Give each successfully loaded item a full minute, including a looping video.
            _slideshowTimer.Stop();
            _slideshowTimer.Start();
            UpdateOverlayLayerVisibility();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A disconnected/deleted folder is retried at the next interval.
            if (!token.IsCancellationRequested)
            {
                SingerBackgroundImage.Source = null;
                SetBackgroundVideo(string.Empty);
                SetBackgroundGif(string.Empty);
                UpdateOverlayLayerVisibility();
            }
            App.WriteDiagnostic("AUDIENCE SLIDESHOW", $"{folder}: {ex.Message}");
        }
        finally
        {
            _slideLoading = false;
            if (token.IsCancellationRequested && _slideshowCts is not null)
                _ = AdvanceBackgroundSlideAsync();
        }
    }
}
