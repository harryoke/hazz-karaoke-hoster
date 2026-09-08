using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class AudienceWindow : Window
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpFrameChanged = 0x0020;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<AudienceSingerDisplayItem> _rotation = new();

    private double _lastSeconds;
    private double _speed = 110;
    private bool _videoPlaying;
    private bool _musicVideoPlaying;
    private bool _karaokeActive;
    private bool _showNextSinger = true;
    private bool _showNextSong = true;
    private bool _hasNextSinger;
    private bool _scrollerEnabled = true;
    private bool _scrollerAtTop;
    private OverlayPosition _nextSingerPosition = OverlayPosition.BottomCenter;
    private bool _backgroundImageEnabled;
    private bool _logoEnabled;
    private bool _kamikazeVisible;
    private string _backgroundImagePath = string.Empty;
    private string _logoImagePath = string.Empty;
    private string _venueMessage = "WELCOME TO KARAOKE WITH HAZZ";
    private FontFamily _nextFontFamily = new("Segoe UI");
    private double _nextFontSize = 48;
    private FontFamily _kamikazeFontFamily = new("Segoe UI Black");
    private double _kamikazeFontSize = 84;
    private Brush _kamikazeBrush = BrushFromHex("#FFFFD34D", Brushes.Gold);

    private Brush _nextHeadingBrush = BrushFromHex("#FFFFD34D", Brushes.Gold);
    private Brush _nextPositionBrush = BrushFromHex("#FFFFD34D", Brushes.Gold);
    private Brush _nextSingerBrush = Brushes.White;
    private Brush _nextSongBrush = BrushFromHex("#FFD8E2EF", Brushes.LightGray);
    private Brush _rotationScrollerBrush = Brushes.White;
    private Brush _venueScrollerBrush = BrushFromHex("#FFFFD34D", Brushes.Gold);
    private DateTime _lastTickerErrorUtc = DateTime.MinValue;

    public AudienceWindow()
    {
        InitializeComponent();
        _ticker.Tick += (_, _) => TickScrollerSafe();
        Loaded += (_, _) => { ResetScroller(); _ticker.Start(); UpdateOverlayLayerVisibility(); };
        Closed += (_, _) =>
        {
            _ticker.Stop();
            StopBackgroundSlideshow();
            AudienceMedia.Stop();
            AudienceMedia.Source = null;
            AudienceMedia.Close();
            MusicVideoMedia.Stop();
            MusicVideoMedia.Source = null;
            MusicVideoMedia.Close();
            SingerBackgroundVideo.Stop();
            SingerBackgroundVideo.Source = null;
            SingerBackgroundVideo.Close();
        };
        SizeChanged += (_, _) => ResetScroller();
    }

    public void SendToDisplay(DisplayTarget target, bool fullScreen)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowState = WindowState.Normal;

        if (!IsVisible) Show();

        if (fullScreen)
        {
            // Screen.Bounds is reported by WinForms in monitor coordinates. Using the native
            // window API here avoids WPF device-independent-coordinate/DPI conversion issues
            // when the host laptop and audience TV use different Windows scaling values.
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;

            UpdateLayout();
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            if (!SetWindowPos(hwnd, HwndTopmost, target.Left, target.Top, target.Width, target.Height, SwpShowWindow | SwpFrameChanged))
            {
                // Safe WPF fallback if the native move is rejected for any reason.
                Left = target.Left;
                Top = target.Top;
                Width = target.Width;
                Height = target.Height;
            }
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = true;
            Topmost = false;
            var width = Math.Min(Math.Max(640, Width), Math.Max(640, target.Width));
            var height = Math.Min(Math.Max(360, Height), Math.Max(360, target.Height));
            Width = width;
            Height = height;
            Left = target.Left + Math.Max(0, (target.Width - width) / 2.0);
            Top = target.Top + Math.Max(0, (target.Height - height) / 2.0);
        }

        Activate();
        Focus();
    }

    public void MakeWindowed()
    {
        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = true;
        Topmost = false;
        Activate();
    }

    public void SetKaraokeActive(bool active)
    {
        _karaokeActive = active;
        if (active) MusicVideoMedia.Pause();
        else if (_musicVideoPlaying && MusicVideoMedia.Source is not null) MusicVideoMedia.Play();
        UpdateOverlayLayerVisibility();
    }

    public void ShowKamikazeBanner()
    {
        _kamikazeVisible = true;
        UpdateOverlayLayerVisibility();
    }

    public void HideKamikazeBanner()
    {
        _kamikazeVisible = false;
        UpdateOverlayLayerVisibility();
    }

    public void SetSingerRotation(IReadOnlyList<AudienceSingerDisplayItem> rotation, bool showSong)
    {
        _rotation.Clear();
        if (rotation is not null) _rotation.AddRange(rotation);
        _hasNextSinger = _rotation.Count > 0;
        _showNextSong = showSong;
        RenderNextSingers();
        RebuildScroller();
        UpdateOverlayLayerVisibility();
    }

    public void Apply(AudienceOverlaySettings s)
    {
        _showNextSinger = s.ShowNextSinger;
        _showNextSong = s.ShowNextSong;
        _nextFontFamily = new FontFamily(s.NextSingerFontFamily);
        _nextFontSize = s.NextSingerFontSize;
        _nextSingerPosition = s.NextSingerPosition;

        _scrollerEnabled = s.ScrollerEnabled;
        _venueMessage = s.ScrollerText?.Trim() ?? string.Empty;
        ScrollerText.FontFamily = new FontFamily(s.ScrollerFontFamily);
        ScrollerText.FontSize = s.ScrollerFontSize;
        _speed = Math.Clamp(s.ScrollerPixelsPerSecond, 20, 500);
        _scrollerAtTop = string.Equals(s.ScrollerPosition, "Top", StringComparison.OrdinalIgnoreCase);
        ScrollerPanel.VerticalAlignment = _scrollerAtTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        Position(_nextSingerPosition);

        var backgroundStretch = s.BackgroundStretchMode switch
        {
            "FillCrop" => Stretch.UniformToFill,
            "Stretch" => Stretch.Fill,
            "Center" => Stretch.None,
            _ => Stretch.Uniform
        };
        SingerBackgroundImage.Stretch = backgroundStretch;
        SingerBackgroundGif.Stretch = backgroundStretch;
        SingerBackgroundVideo.Stretch = backgroundStretch;

        _nextHeadingBrush = BrushFromHex(s.NextHeadingColor, Brushes.Gold);
        _nextPositionBrush = BrushFromHex(s.NextPositionColor, Brushes.Gold);
        _nextSingerBrush = BrushFromHex(s.NextSingerColor, Brushes.White);
        _nextSongBrush = BrushFromHex(s.NextSongColor, Brushes.LightGray);
        _rotationScrollerBrush = BrushFromHex(s.RotationScrollerColor, Brushes.White);
        _venueScrollerBrush = BrushFromHex(s.VenueScrollerColor, Brushes.Gold);
        NextSingerHeadingText.Foreground = _nextHeadingBrush;

        _backgroundImageEnabled = s.BackgroundImageEnabled;
        _logoEnabled = s.LogoEnabled;
        _musicVideoShowLogo = s.MusicVideoShowLogo;
        _musicVideoShowScroller = s.MusicVideoShowScroller;
        _musicVideoShowSingers = s.MusicVideoShowSingers;
        _musicVideoShowKamikaze = s.MusicVideoShowKamikaze;

        ConfigureBackgroundSlideshow(s);
        SetCachedImage(AudienceLogoImage, s.LogoImagePath, ref _logoImagePath);
        PositionLogo(s.LogoPosition, s.LogoWidth);

        _kamikazeFontFamily = new FontFamily(string.IsNullOrWhiteSpace(s.KamikazeFontFamily) ? "Segoe UI Black" : s.KamikazeFontFamily);
        _kamikazeFontSize = Math.Clamp(s.KamikazeFontSize, 36, 180);
        _kamikazeBrush = BrushFromHex(s.KamikazeColor, Brushes.Gold);
        KamikazeTextBlock.Text = string.IsNullOrWhiteSpace(s.KamikazeText) ? "KAMIKAZE KARAOKE!" : s.KamikazeText.Trim();
        KamikazeTextBlock.FontFamily = _kamikazeFontFamily;
        KamikazeTextBlock.FontSize = _kamikazeFontSize;
        KamikazeTextBlock.Foreground = _kamikazeBrush;

        RenderNextSingers();
        RebuildScroller();
        UpdateOverlayLayerVisibility();
    }

    private void RenderNextSingers()
    {
        NextSingersStack.Children.Clear();
        foreach (var item in _rotation.Take(4))
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.42, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.58, GridUnitType.Star) });

            var number = new TextBlock
            {
                Text = $"{item.Position}.",
                FontFamily = _nextFontFamily,
                FontSize = Math.Max(20, _nextFontSize * 0.72),
                FontWeight = FontWeights.Bold,
                Foreground = _nextPositionBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(number, 0);
            row.Children.Add(number);

            var singer = new TextBlock
            {
                Text = item.SingerName,
                FontFamily = _nextFontFamily,
                FontSize = _nextFontSize,
                FontWeight = FontWeights.Bold,
                Foreground = _nextSingerBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 14, 0)
            };
            Grid.SetColumn(singer, 1);
            row.Children.Add(singer);

            var songText = string.IsNullOrWhiteSpace(item.SongText) ? "Waiting for song" : item.SongText;
            var song = new TextBlock
            {
                Text = _showNextSong ? songText : string.Empty,
                FontFamily = _nextFontFamily,
                FontSize = Math.Max(18, _nextFontSize * 0.55),
                Foreground = _nextSongBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Visibility = _showNextSong ? Visibility.Visible : Visibility.Collapsed
            };
            Grid.SetColumn(song, 2);
            row.Children.Add(song);

            NextSingersStack.Children.Add(row);
        }
    }

    private void RebuildScroller()
    {
        ScrollerText.Inlines.Clear();

        if (_rotation.Count > 0)
        {
            for (var i = 0; i < _rotation.Count; i++)
            {
                var item = _rotation[i];
                if (i > 0) ScrollerText.Inlines.Add(new Run("   •   ") { Foreground = _rotationScrollerBrush });
                ScrollerText.Inlines.Add(new Run($"{item.Position}. {item.SingerName}") { Foreground = _rotationScrollerBrush });
            }
        }
        else
        {
            ScrollerText.Inlines.Add(new Run("Singer rotation empty") { Foreground = _rotationScrollerBrush });
        }

        if (!string.IsNullOrWhiteSpace(_venueMessage))
        {
            ScrollerText.Inlines.Add(new Run("   ◆   ") { Foreground = _venueScrollerBrush });
            ScrollerText.Inlines.Add(new Run(_venueMessage) { Foreground = _venueScrollerBrush });
        }

        ResetScroller();
    }

    private bool _musicVideoShowLogo;
    private bool _musicVideoShowScroller;
    private bool _musicVideoShowSingers;
    private bool _musicVideoShowKamikaze;
    private void UpdateOverlayLayerVisibility()
    {
        var musicVideoVisible = !_karaokeActive && MusicVideoMedia.Source is not null;
        SingerBackgroundGif.Visibility = !musicVideoVisible && !_karaokeActive && _backgroundImageEnabled && _backgroundGifPath.Length > 0
            ? Visibility.Visible : Visibility.Collapsed;
        SingerBackgroundVideo.Visibility = !musicVideoVisible && !_karaokeActive && _backgroundImageEnabled && SingerBackgroundVideo.Source is not null
            ? Visibility.Visible : Visibility.Collapsed;
        MusicVideoMedia.Visibility = musicVideoVisible
            ? Visibility.Visible : Visibility.Collapsed;
        // Singer-view artwork and informational overlays are deliberately hidden during karaoke.
        // The logo is different: if enabled it remains above CD+G/video for the whole show.
        SingerBackgroundImage.Visibility = !musicVideoVisible && !_karaokeActive && _backgroundImageEnabled && SingerBackgroundImage.Source is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
        AudienceLogoImage.Visibility = (!musicVideoVisible || _musicVideoShowLogo) && _logoEnabled && AudienceLogoImage.Source is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

        KamikazePanel.Visibility = (!musicVideoVisible || _musicVideoShowKamikaze) && !_karaokeActive && _kamikazeVisible
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Kamikaze mode intentionally replaces the ordinary singer/rotation information so
        // the random selection remains a surprise until the karaoke track actually starts.
        NextSingerPanel.Visibility = (!musicVideoVisible || _musicVideoShowSingers) && !_karaokeActive && !_kamikazeVisible && _showNextSinger && _hasNextSinger
            ? Visibility.Visible
            : Visibility.Collapsed;
        ScrollerPanel.Visibility = (!musicVideoVisible || _musicVideoShowScroller) && !_karaokeActive && !_kamikazeVisible && _scrollerEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Brush BrushFromHex(string? value, Brush fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var converted = ColorConverter.ConvertFromString(value);
            if (converted is Color color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
        }
        catch { }
        return fallback;
    }

    private static void SetCachedImage(Image image, string? path, ref string cachedPath)
    {
        path = path?.Trim() ?? string.Empty;
        if (string.Equals(path, cachedPath, StringComparison.OrdinalIgnoreCase)) return;
        cachedPath = path;
        image.Source = LoadImageUnlocked(path);
    }

    private static BitmapSource? LoadImageUnlocked(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void PositionLogo(OverlayPosition position, double width)
    {
        AudienceLogoImage.Width = Math.Clamp(width, 60, 800);
        AudienceLogoImage.HorizontalAlignment = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.BottomLeft => HorizontalAlignment.Left,
            OverlayPosition.TopRight or OverlayPosition.BottomRight => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center
        };
        AudienceLogoImage.VerticalAlignment = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight => VerticalAlignment.Top,
            _ => VerticalAlignment.Bottom
        };
        AudienceLogoImage.Margin = position is OverlayPosition.BottomLeft or OverlayPosition.BottomCenter or OverlayPosition.BottomRight
            ? new Thickness(24, 24, 24, 74)
            : new Thickness(24);
    }

    public void ShowCdg(ImageSource source)
    {
        AudienceMedia.Stop();
        AudienceMedia.Source = null;
        AudienceMedia.Visibility = Visibility.Collapsed;
        AudienceCdgImage.Source = source;
        AudienceCdgImage.Visibility = Visibility.Visible;
        _videoPlaying = false;
    }

    public void LoadMutedVideo(string path)
    {
        AudienceCdgImage.Source = null;
        AudienceCdgImage.Visibility = Visibility.Collapsed;
        AudienceMedia.Visibility = Visibility.Visible;
        AudienceMedia.Source = new Uri(path);
        _videoPlaying = false;
    }

    public void PlayVideo(TimeSpan position)
    {
        if (AudienceMedia.Source is null) return;
        AudienceMedia.Position = position;
        AudienceMedia.Play();
        _videoPlaying = true;
    }

    public void PauseVideo()
    {
        AudienceMedia.Pause();
        _videoPlaying = false;
    }

    public void StopVideo()
    {
        AudienceMedia.Stop();
        _videoPlaying = false;
    }

    public void SyncVideo(TimeSpan hostPosition)
    {
        if (!_videoPlaying || AudienceMedia.Source is null) return;
        var drift = Math.Abs((AudienceMedia.Position - hostPosition).TotalMilliseconds);
        if (drift > 250) AudienceMedia.Position = hostPosition;
    }

    public void ClearKaraokeVisual()
    {
        AudienceMedia.Stop();
        AudienceMedia.Source = null;
        AudienceMedia.Visibility = Visibility.Collapsed;
        AudienceCdgImage.Source = null;
        AudienceCdgImage.Visibility = Visibility.Collapsed;
        _videoPlaying = false;
    }

    public void ShowMusicVideo(string path, TimeSpan position, bool playing)
    {
        var uri = new Uri(path);
        if (MusicVideoMedia.Source is null || !string.Equals(MusicVideoMedia.Source.LocalPath, uri.LocalPath, StringComparison.OrdinalIgnoreCase))
        {
            MusicVideoMedia.Stop();
            MusicVideoMedia.Source = uri;
        }
        MusicVideoMedia.Position = position;
        _musicVideoPlaying = playing;
        if (!_karaokeActive && playing) MusicVideoMedia.Play();
        else MusicVideoMedia.Pause();
        UpdateOverlayLayerVisibility();
    }

    public void PauseMusicVideo()
    {
        MusicVideoMedia.Pause();
        _musicVideoPlaying = false;
    }

    public void ResumeMusicVideo(TimeSpan position)
    {
        if (MusicVideoMedia.Source is null) return;
        MusicVideoMedia.Position = position;
        _musicVideoPlaying = true;
        if (!_karaokeActive) MusicVideoMedia.Play();
        UpdateOverlayLayerVisibility();
    }

    public void SyncMusicVideo(TimeSpan hostPosition)
    {
        if (!_musicVideoPlaying || _karaokeActive || MusicVideoMedia.Source is null) return;
        var drift = Math.Abs((MusicVideoMedia.Position - hostPosition).TotalMilliseconds);
        if (drift > 250) MusicVideoMedia.Position = hostPosition;
    }

    public void ClearMusicVideo()
    {
        MusicVideoMedia.Stop();
        MusicVideoMedia.Source = null;
        MusicVideoMedia.Visibility = Visibility.Collapsed;
        _musicVideoPlaying = false;
        UpdateOverlayLayerVisibility();
    }

    private void MusicVideo_Ended(object sender, RoutedEventArgs e) => ClearMusicVideo();
    private void MusicVideo_Failed(object sender, ExceptionRoutedEventArgs e) => ClearMusicVideo();

    private void Position(OverlayPosition p)
    {
        NextSingerPanel.HorizontalAlignment = p switch
        {
            OverlayPosition.TopLeft or OverlayPosition.BottomLeft => System.Windows.HorizontalAlignment.Left,
            OverlayPosition.TopRight or OverlayPosition.BottomRight => System.Windows.HorizontalAlignment.Right,
            _ => System.Windows.HorizontalAlignment.Center
        };
        NextSingerPanel.VerticalAlignment = p switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight => System.Windows.VerticalAlignment.Top,
            _ => System.Windows.VerticalAlignment.Bottom
        };
        var atTop = p is OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight;
        NextSingerPanel.Margin = new Thickness(
            30,
            atTop && _scrollerAtTop ? 74 : 30,
            30,
            !atTop && !_scrollerAtTop ? 74 : 30);
    }

    private void ResetScroller()
    {
        if (ScrollerText is null || ScrollerPanel is null) return;
        ScrollerText.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(ScrollerText, Math.Max(ActualWidth, 10));
        Canvas.SetTop(ScrollerText, Math.Max(0, (ScrollerPanel.ActualHeight - ScrollerText.DesiredSize.Height) / 2));
        _lastSeconds = _clock.Elapsed.TotalSeconds;
    }

    private void TickScroller()
    {
        if (ScrollerPanel.Visibility != Visibility.Visible) return;
        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.1);
        _lastSeconds = now;
        var left = Canvas.GetLeft(ScrollerText);
        if (double.IsNaN(left)) left = ActualWidth;
        left -= _speed * dt;
        if (left + ScrollerText.ActualWidth < 0) left = Math.Max(ActualWidth, 10);
        Canvas.SetLeft(ScrollerText, left);
    }

    private void TickScrollerSafe()
    {
        try
        {
            TickScroller();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var now = DateTime.UtcNow;
            if (now - _lastTickerErrorUtc < TimeSpan.FromMinutes(1)) return;
            _lastTickerErrorUtc = now;
            App.WriteDiagnostic("AUDIENCE TICKER", ex.ToString());
        }
    }
}
