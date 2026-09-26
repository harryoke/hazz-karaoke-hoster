using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class AudienceWindow
{
    private readonly DispatcherTimer _slideshowTimer = new() { Interval = TimeSpan.FromSeconds(60) };
    private readonly Random _slideshowRandom = new();
    private CancellationTokenSource? _slideshowCts;
    private string _slideshowFolder = string.Empty;
    private string _lastSlide = string.Empty;
    private bool _slideLoading;
    private bool _slideshowHooked;
    private string _backgroundVideoPath = string.Empty;
    private string _backgroundGifPath = string.Empty;
    private double _slideshowSeconds = 60;
    private string _slideshowTransition = "Fade";
    private static bool IsBackgroundGif(string path) => string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);

    private double _backgroundGifSpeed = 1;
    private CancellationTokenSource? _gifLoadCts;
    private MemoryStream? _gifStream;
    private const long MaximumGifBytes = 128L * 1024 * 1024;

    private void SetSlideshowEnhancementOptions(double seconds, string transition)
    {
        seconds = double.IsFinite(seconds) ? Math.Clamp(seconds, 20, 60) : 60;
        transition = transition switch
        {
            "Cut" or "Fade" or "Slide Left" or "Slide Right" or "Zoom" or "Random" => transition,
            _ => "Fade"
        };

        var intervalChanged = Math.Abs(_slideshowSeconds - seconds) > 0.01;
        _slideshowSeconds = seconds;
        _slideshowTransition = transition;
        _slideshowTimer.Interval = TimeSpan.FromSeconds(_slideshowSeconds);

        if (intervalChanged && _slideshowTimer.IsEnabled)
        {
            // A slider change starts a fresh interval instead of unexpectedly advancing immediately.
            _slideshowTimer.Stop();
            _slideshowTimer.Start();
        }
    }

    private string ResolveSlideshowTransition()
    {
        if (!string.Equals(_slideshowTransition, "Random", StringComparison.OrdinalIgnoreCase))
            return _slideshowTransition;
        var choices = new[] { "Fade", "Slide Left", "Slide Right", "Zoom" };
        return choices[_slideshowRandom.Next(choices.Length)];
    }

    private void AnimateSlideshowElement(FrameworkElement element)
    {
        if (element.Visibility != Visibility.Visible)
        {
            SingerBackgroundTransitionImage.Visibility = Visibility.Collapsed;
            return;
        }

        var transition = ResolveSlideshowTransition();
        var finalOpacity = Math.Clamp(element.Opacity, 0, 1);
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.RenderTransform = Transform.Identity;
        element.RenderTransformOrigin = new Point(0.5, 0.5);

        if (string.Equals(transition, "Cut", StringComparison.OrdinalIgnoreCase))
        {
            element.Opacity = finalOpacity;
            SingerBackgroundTransitionImage.Visibility = Visibility.Collapsed;
            return;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(750));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var group = new TransformGroup();
        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform(0, 0);
        group.Children.Add(scale);
        group.Children.Add(translate);
        element.RenderTransform = group;

        var opacity = new DoubleAnimation(0, finalOpacity, duration) { EasingFunction = ease };
        opacity.Completed += (_, _) =>
        {
            element.Opacity = finalOpacity;
            SingerBackgroundTransitionImage.Visibility = Visibility.Collapsed;
            SingerBackgroundTransitionImage.Source = null;
        };
        element.BeginAnimation(UIElement.OpacityProperty, opacity);

        if (string.Equals(transition, "Slide Left", StringComparison.OrdinalIgnoreCase))
        {
            var distance = Math.Max(160, Root.ActualWidth * 0.18);
            translate.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(distance, 0, duration) { EasingFunction = ease });
        }
        else if (string.Equals(transition, "Slide Right", StringComparison.OrdinalIgnoreCase))
        {
            var distance = Math.Max(160, Root.ActualWidth * 0.18);
            translate.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(-distance, 0, duration) { EasingFunction = ease });
        }
        else if (string.Equals(transition, "Zoom", StringComparison.OrdinalIgnoreCase))
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.86, 1, duration) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.86, 1, duration) { EasingFunction = ease });
        }
    }

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
            XamlAnimatedGif.AnimationBehavior.SetRepeatBehavior(SingerBackgroundGif, RepeatBehavior.Forever);
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

    private void BackgroundVideo_Ended(object sender, RoutedEventArgs e)
    {
        if (_backgroundVideoPath.Length == 0) return;
        SingerBackgroundVideo.Position = TimeSpan.Zero;
        SingerBackgroundVideo.Play();
    }

    private void BackgroundVideo_Failed(object sender, ExceptionRoutedEventArgs e)
    {
        // Keep the selected slideshow slot, then advance; never interrupt the show with a media dialog.
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
        SingerBackgroundTransitionImage.Visibility = Visibility.Collapsed;
        SingerBackgroundTransitionImage.Source = null;
        SetBackgroundVideo(string.Empty);
        SetBackgroundGif(string.Empty);
    }

    private void ConfigureBackgroundSlideshow(AudienceOverlaySettings settings)
    {
        var enhancements = AudienceEnhancementSettingsStore.Load();
        SetSlideshowEnhancementOptions(enhancements.SlideshowSeconds, enhancements.SlideshowTransition);

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
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException)
                    { /* Skip unreadable images and try the next file. */ }
                }
                return (Path: string.Empty, Image: (BitmapSource?)null);
            }, token);
            if (token.IsCancellationRequested) return;

            // Keep the previous still image underneath the incoming item during the animation.
            if (SingerBackgroundImage.Source is not null && SingerBackgroundImage.Visibility == Visibility.Visible)
            {
                SingerBackgroundTransitionImage.Source = SingerBackgroundImage.Source;
                SingerBackgroundTransitionImage.Stretch = SingerBackgroundImage.Stretch;
                SingerBackgroundTransitionImage.Opacity = SingerBackgroundImage.Opacity;
                SingerBackgroundTransitionImage.Visibility = Visibility.Visible;
            }
            else
            {
                SingerBackgroundTransitionImage.Visibility = Visibility.Collapsed;
                SingerBackgroundTransitionImage.Source = null;
            }

            _lastSlide = slide.Path;
            var isVideo = IsBackgroundVideo(slide.Path);
            var isGif = IsBackgroundGif(slide.Path);
            SetBackgroundVideo(isVideo ? slide.Path : string.Empty);
            SetBackgroundGif(isGif ? slide.Path : string.Empty);
            SingerBackgroundImage.Source = isGif || isVideo ? null : slide.Image;

            _slideshowTimer.Stop();
            _slideshowTimer.Start();
            UpdateOverlayLayerVisibility();

            if (isVideo) AnimateSlideshowElement(SingerBackgroundVideo);
            else if (isGif) AnimateSlideshowElement(SingerBackgroundGif);
            else AnimateSlideshowElement(SingerBackgroundImage);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A disconnected/deleted folder is retried at the next selected interval.
            if (!token.IsCancellationRequested)
            {
                SingerBackgroundImage.Source = null;
                SingerBackgroundTransitionImage.Source = null;
                SingerBackgroundTransitionImage.Visibility = Visibility.Collapsed;
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
