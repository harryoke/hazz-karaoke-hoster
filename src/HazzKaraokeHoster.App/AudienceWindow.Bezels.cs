using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class AudienceWindow
{
    private readonly DispatcherTimer _bezelSlideshowTimer = new();
    private string _bezelImagePath = string.Empty;
    private string _bezelFolderPath = string.Empty;
    private string _lastBezelSlide = string.Empty;
    private bool _bezelSlideshowHooked;
    private bool _bezelEnabled;
    private Thickness _bezelViewport = new(0);

    private void ConfigureBezel(AudienceOverlaySettings settings)
    {
        _bezelEnabled = settings.BezelEnabled;
        var folder = _bezelEnabled ? settings.BezelFolderPath?.Trim() ?? string.Empty : string.Empty;
        var image = _bezelEnabled ? settings.BezelImagePath?.Trim() ?? string.Empty : string.Empty;
        _bezelSlideshowTimer.Interval = TimeSpan.FromSeconds(
            double.IsFinite(settings.BezelSlideshowSeconds) ? Math.Clamp(settings.BezelSlideshowSeconds, 5, 600) : 30);

        if (!Directory.Exists(folder)) folder = string.Empty;
        if (folder.Length > 0)
        {
            var changed = !string.Equals(_bezelFolderPath, folder, StringComparison.OrdinalIgnoreCase);
            _bezelFolderPath = folder;
            if (changed) _lastBezelSlide = string.Empty;
            if (changed || !_bezelSlideshowTimer.IsEnabled) AdvanceBezelSlide();
            if (!_bezelSlideshowHooked)
            {
                _bezelSlideshowTimer.Tick += (_, _) => AdvanceBezelSlide();
                _bezelSlideshowHooked = true;
            }
            if (!_bezelSlideshowTimer.IsEnabled) _bezelSlideshowTimer.Start();
            return;
        }

        _bezelFolderPath = string.Empty;
        _bezelSlideshowTimer.Stop();
        SetBezelImage(image);
    }

    private void AdvanceBezelSlide()
    {
        if (!_bezelEnabled || string.IsNullOrWhiteSpace(_bezelFolderPath)) return;
        try
        {
            var frames = Directory.EnumerateFiles(_bezelFolderPath)
                .Where(path => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(10_000)
                .ToArray();
            if (frames.Length == 0) { SetBezelImage(string.Empty); return; }
            var current = Array.FindIndex(frames, path => string.Equals(path, _lastBezelSlide, StringComparison.OrdinalIgnoreCase));
            _lastBezelSlide = frames[(current + 1) % frames.Length];
            SetBezelImage(_lastBezelSlide);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            App.WriteDiagnostic("AUDIENCE BEZEL", ex.Message);
        }
    }

    private void SetBezelImage(string path)
    {
        var previousPath = _bezelImagePath;
        var old = AudienceBezelImage.Source;
        SetCachedImage(AudienceBezelImage, path, ref _bezelImagePath);
        _bezelViewport = _bezelEnabled && TryFindTransparentViewport(AudienceBezelImage.Source as BitmapSource, out var viewport)
            ? viewport : new Thickness(0);
        ApplyBezelViewport();

        var visible = _bezelEnabled && AudienceBezelImage.Source is not null;
        AudienceBezelImage.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible || old is null || string.Equals(previousPath, _bezelImagePath, StringComparison.OrdinalIgnoreCase)) return;

        AudienceBezelImage.BeginAnimation(UIElement.OpacityProperty, null);
        AudienceBezelImage.Opacity = 0;
        AudienceBezelImage.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(400))));
    }

    private void ApplyBezelViewport()
    {
        var margins = !_bezelEnabled || Root.ActualWidth <= 0 || Root.ActualHeight <= 0
            ? new Thickness(0)
            : new Thickness(
                Root.ActualWidth * _bezelViewport.Left,
                Root.ActualHeight * _bezelViewport.Top,
                Root.ActualWidth * _bezelViewport.Right,
                Root.ActualHeight * _bezelViewport.Bottom);
        SingerBackgroundTransitionImage.Margin = margins;
        SingerBackgroundImage.Margin = margins;
        SingerBackgroundVideo.Margin = margins;
        SingerBackgroundGif.Margin = margins;
        AudienceMedia.Margin = margins;
        MusicVideoMedia.Margin = margins;
        AudienceCdgImage.Margin = margins;
        // A native VLC overlay is hosted inside the already-shrunk video surface.
        AudienceOverlays.Margin = _overlayOwner is null ? margins : new Thickness(0);
    }

    // Finds the outer transparent viewing area. Decorative artwork is allowed to
    // overlap that area, so a row/column only needs to be mostly transparent.
    private static bool TryFindTransparentViewport(BitmapSource? source, out Thickness viewport)
    {
        viewport = new Thickness(0);
        if (source is null || source.PixelWidth < 8 || source.PixelHeight < 8) return false;
        try
        {
            var scale = Math.Min(1d, 960d / source.PixelWidth);
            BitmapSource sampled = source;
            if (scale < 1)
            {
                var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
                scaled.Freeze();
                sampled = scaled;
            }
            var pixels = new byte[sampled.PixelWidth * sampled.PixelHeight * 4];
            var converted = new FormatConvertedBitmap(sampled, PixelFormats.Bgra32, null, 0);
            converted.CopyPixels(pixels, sampled.PixelWidth * 4, 0);
            var transparentRows = new int[sampled.PixelHeight];
            var transparentColumns = new int[sampled.PixelWidth];
            for (var row = 0; row < sampled.PixelHeight; row++)
            {
                for (var column = 0; column < sampled.PixelWidth; column++)
                {
                    var alpha = pixels[(row * sampled.PixelWidth + column) * 4 + 3];
                    if (alpha > 8) continue;
                    transparentRows[row]++;
                    transparentColumns[column]++;
                }
            }
            var (top, bottom) = LongestMostlyTransparentRun(transparentRows, sampled.PixelWidth);
            var (left, right) = LongestMostlyTransparentRun(transparentColumns, sampled.PixelHeight);
            if (right - left < sampled.PixelWidth * 0.08 || bottom - top < sampled.PixelHeight * 0.08) return false;
            viewport = new Thickness(
                (double)left / sampled.PixelWidth,
                (double)top / sampled.PixelHeight,
                (double)(sampled.PixelWidth - right) / sampled.PixelWidth,
                (double)(sampled.PixelHeight - bottom) / sampled.PixelHeight);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or OutOfMemoryException)
        {
            App.WriteDiagnostic("AUDIENCE BEZEL", ex.Message);
            return false;
        }
    }

    private static (int Start, int End) LongestMostlyTransparentRun(int[] transparentCounts, int perpendicularLength)
    {
        const double minimumTransparentShare = 0.50;
        var bestStart = 0;
        var bestEnd = 0;
        var currentStart = 0;
        for (var index = 0; index <= transparentCounts.Length; index++)
        {
            var clear = index < transparentCounts.Length && transparentCounts[index] >= perpendicularLength * minimumTransparentShare;
            if (clear) continue;
            if (index - currentStart > bestEnd - bestStart)
            {
                bestStart = currentStart;
                bestEnd = index;
            }
            currentStart = index + 1;
        }
        return (bestStart, bestEnd);
    }
}
