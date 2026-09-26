using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

public partial class AudienceWindow
{
    private readonly DispatcherTimer _nowSingingTimer = new();
    private readonly DispatcherTimer _announcementTimer = new();
    private readonly DispatcherTimer _singerCallUpTimer = new();
    private bool _audienceEnhancementHostHooked;
    private bool _enhancementPlaybackActive;
    private string _broadcastQueueStatus = string.Empty;
    private string _broadcastVenueTitle = string.Empty;
    private AudienceEnhancementSettings _enhancementSettings = new();

    private void AudienceEnhancementHost_Loaded(object sender, RoutedEventArgs e)
    {
        if (_audienceEnhancementHostHooked) return;
        _audienceEnhancementHostHooked = true;

        _nowSingingTimer.Tick += (_, _) => ClearNowSinging();
        _announcementTimer.Tick += (_, _) => ClearTemporaryAnnouncement();
        _singerCallUpTimer.Tick += (_, _) => ClearSingerCallUp();
        NextSingersStack.LayoutUpdated += (_, _) => EnforceComingUpCount();
        Closed += (_, _) =>
        {
            _nowSingingTimer.Stop();
            _announcementTimer.Stop();
            _singerCallUpTimer.Stop();
        };

        ApplyEnhancementSettings(AudienceEnhancementSettingsStore.Load());
    }

    internal void ApplyEnhancementSettings(AudienceEnhancementSettings settings)
    {
        _enhancementSettings = AudienceEnhancementSettingsStore.Normalize(settings ?? new AudienceEnhancementSettings());
        SetSlideshowEnhancementOptions(_enhancementSettings.SlideshowSeconds, _enhancementSettings.SlideshowTransition);
        ApplyEditableOverlayStyles();
        EnforceComingUpCount();
        if (!_enhancementSettings.ShowNowSinging) ClearNowSinging();
        if (!_enhancementSettings.ShowSingerCallUp) ClearSingerCallUp();
        RefreshBroadcastBars(animate: false);
    }

    private void ApplyEditableOverlayStyles()
    {
        if (SingerCallUpPanel.Child is StackPanel callStack && callStack.Children.Count >= 4)
        {
            if (callStack.Children[0] is TextBlock heading)
            {
                heading.Text = _enhancementSettings.SingerCallUpHeadingText;
                heading.Visibility = string.IsNullOrWhiteSpace(heading.Text) ? Visibility.Collapsed : Visibility.Visible;
                ApplyTextStyle(heading, _enhancementSettings.SingerCallUpHeadingStyle);
            }
            ApplyTextStyle(SingerCallUpNameText, _enhancementSettings.SingerCallUpNameStyle);
            ApplyTextStyle(SingerCallUpSongText, _enhancementSettings.SingerCallUpSongStyle);
            if (callStack.Children[3] is TextBlock prompt)
            {
                prompt.Text = _enhancementSettings.SingerCallUpPromptText;
                prompt.Visibility = string.IsNullOrWhiteSpace(prompt.Text) ? Visibility.Collapsed : Visibility.Visible;
                ApplyTextStyle(prompt, _enhancementSettings.SingerCallUpPromptStyle);
            }
        }
        SingerCallUpPanel.Background = BrushFrom(_enhancementSettings.SingerCallUpBackgroundColor, Brushes.Black);
        SingerCallUpPanel.BorderBrush = BrushFrom(_enhancementSettings.SingerCallUpBorderColor, Brushes.Gold);
        ApplyPanelPosition(SingerCallUpPanel, _enhancementSettings.SingerCallUpPosition);

        if (NowSingingPanel.Child is StackPanel nowStack && nowStack.Children.Count >= 3 && nowStack.Children[0] is TextBlock nowHeading)
        {
            nowHeading.Text = _enhancementSettings.NowSingingHeadingText;
            nowHeading.Visibility = string.IsNullOrWhiteSpace(nowHeading.Text) ? Visibility.Collapsed : Visibility.Visible;
            ApplyTextStyle(nowHeading, _enhancementSettings.NowSingingHeadingStyle);
        }
        ApplyTextStyle(NowSingingSingerText, _enhancementSettings.NowSingingNameStyle);
        ApplyTextStyle(NowSingingSongText, _enhancementSettings.NowSingingSongStyle);
        NowSingingPanel.Background = BrushFrom(_enhancementSettings.NowSingingBackgroundColor, Brushes.Black);
        NowSingingPanel.BorderBrush = BrushFrom(_enhancementSettings.NowSingingBorderColor, Brushes.White);
        ApplyPanelPosition(NowSingingPanel, _enhancementSettings.NowSingingPosition);

        ApplyTextStyle(QueueStatusText, _enhancementSettings.QueueStatusStyle);
        QueueStatusPanel.Background = BrushFrom(_enhancementSettings.QueueStatusBackgroundColor, Brushes.Black);
        QueueStatusPanel.BorderBrush = BrushFrom(_enhancementSettings.QueueStatusBorderColor, Brushes.White);
        ApplyPanelPosition(QueueStatusPanel, _enhancementSettings.QueueStatusPosition);

        ApplyTextStyle(VenueHeaderText, _enhancementSettings.VenueHeaderStyle);
        VenueHeaderPanel.Background = BrushFrom(_enhancementSettings.VenueHeaderBackgroundColor, Brushes.Black);
        VenueHeaderPanel.BorderBrush = BrushFrom(_enhancementSettings.VenueHeaderBorderColor, Brushes.White);
        ApplyPanelPosition(VenueHeaderPanel, _enhancementSettings.VenueHeaderPosition);

        ApplyTextStyle(AnnouncementText, _enhancementSettings.AnnouncementStyle);
        AnnouncementPanel.Background = BrushFrom(_enhancementSettings.AnnouncementBackgroundColor, Brushes.Black);
        AnnouncementPanel.BorderBrush = BrushFrom(_enhancementSettings.AnnouncementBorderColor, Brushes.Gold);
        ApplyPanelPosition(AnnouncementPanel, _enhancementSettings.AnnouncementPosition);
    }

    private static void ApplyTextStyle(TextBlock text, AudienceOverlayTextStyle style)
    {
        text.FontFamily = new FontFamily(style.FontFamily);
        text.FontSize = style.FontSize;
        text.Foreground = BrushFrom(style.TextColor, Brushes.White);
    }

    private static void ApplyPanelPosition(FrameworkElement panel, string? position)
    {
        position = AudienceEnhancementSettingsStore.OverlayPositions.FirstOrDefault(x =>
            string.Equals(x, position, StringComparison.OrdinalIgnoreCase)) ?? "Center";

        panel.HorizontalAlignment = position.EndsWith("Left", StringComparison.OrdinalIgnoreCase)
            ? HorizontalAlignment.Left
            : position.EndsWith("Right", StringComparison.OrdinalIgnoreCase)
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Center;

        panel.VerticalAlignment = position.StartsWith("Top", StringComparison.OrdinalIgnoreCase)
            ? VerticalAlignment.Top
            : position.StartsWith("Bottom", StringComparison.OrdinalIgnoreCase)
                ? VerticalAlignment.Bottom
                : VerticalAlignment.Center;

        // Keep top and bottom anchors clear of the existing 52px scroller strips.
        var top = position.StartsWith("Top", StringComparison.OrdinalIgnoreCase) ? 66d : 30d;
        var bottom = position.StartsWith("Bottom", StringComparison.OrdinalIgnoreCase) ? 74d : 30d;
        panel.Margin = new Thickness(30, top, 30, bottom);
    }

    private static Brush BrushFrom(string value, Brush fallback)
    {
        try
        {
            var converted = new BrushConverter().ConvertFromString(value) as Brush;
            if (converted is not null)
            {
                if (converted.CanFreeze) converted.Freeze();
                return converted;
            }
        }
        catch { }
        return fallback;
    }

    private void EnforceComingUpCount()
    {
        var count = Math.Clamp(_enhancementSettings.ComingUpCount, 1, 4);
        for (var i = 0; i < NextSingersStack.Children.Count; i++)
            NextSingersStack.Children[i].Visibility = i < count ? Visibility.Visible : Visibility.Collapsed;
        NextSingerHeadingText.Text = count == 1 ? "UP NEXT" : "COMING UP";
    }

    internal void SetEnhancementPlaybackActive(bool active)
    {
        if (_enhancementPlaybackActive == active) return;
        _enhancementPlaybackActive = active;
        if (active)
        {
            ClearSingerCallUp();
            VenueHeaderPanel.Visibility = Visibility.Collapsed;
            QueueStatusPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            RefreshBroadcastBars(animate: true);
        }
    }

    internal void SetBroadcastStatus(string queueStatus, string venueTitle)
    {
        queueStatus = queueStatus?.Trim() ?? string.Empty;
        venueTitle = venueTitle?.Trim() ?? string.Empty;
        var changed = !string.Equals(_broadcastQueueStatus, queueStatus, StringComparison.Ordinal)
            || !string.Equals(_broadcastVenueTitle, venueTitle, StringComparison.Ordinal);
        _broadcastQueueStatus = queueStatus;
        _broadcastVenueTitle = venueTitle;
        RefreshBroadcastBars(animate: changed);
    }

    private void RefreshBroadcastBars(bool animate)
    {
        QueueStatusText.Text = _broadcastQueueStatus;
        VenueHeaderText.Text = _broadcastVenueTitle;

        var showQueue = !_enhancementPlaybackActive && _enhancementSettings.ShowQueueStatus
            && !string.IsNullOrWhiteSpace(_broadcastQueueStatus);
        var showVenue = !_enhancementPlaybackActive && _enhancementSettings.ShowVenueHeader
            && !string.IsNullOrWhiteSpace(_broadcastVenueTitle);

        SetEnhancementPanelVisible(QueueStatusPanel, showQueue, animate);
        SetEnhancementPanelVisible(VenueHeaderPanel, showVenue, animate);
    }

    internal void ShowSingerCallUp(string singerName, string songText, TimeSpan duration)
    {
        if (!_enhancementSettings.ShowSingerCallUp || _enhancementPlaybackActive || string.IsNullOrWhiteSpace(singerName)) return;

        ClearNowSinging();
        SingerCallUpNameText.Text = singerName.Trim();
        SingerCallUpSongText.Text = songText?.Trim() ?? string.Empty;
        SingerCallUpSongText.Visibility = string.IsNullOrWhiteSpace(SingerCallUpSongText.Text)
            ? Visibility.Collapsed : Visibility.Visible;
        SingerCallUpPanel.Visibility = Visibility.Visible;
        AnimateEnhancementPanel(SingerCallUpPanel);

        _singerCallUpTimer.Stop();
        var seconds = double.IsFinite(duration.TotalSeconds)
            ? Math.Clamp(duration.TotalSeconds, 3, 15)
            : Math.Clamp(_enhancementSettings.SingerCallUpSeconds, 3, 15);
        _singerCallUpTimer.Interval = TimeSpan.FromSeconds(seconds);
        _singerCallUpTimer.Start();
    }

    internal void ClearSingerCallUp()
    {
        _singerCallUpTimer.Stop();
        HideEnhancementPanel(SingerCallUpPanel);
    }

    internal void ShowNowSinging(string singerName, string songText, TimeSpan duration)
    {
        if (!_enhancementSettings.ShowNowSinging || string.IsNullOrWhiteSpace(singerName)) return;

        ClearSingerCallUp();
        NowSingingSingerText.Text = singerName.Trim();
        NowSingingSongText.Text = songText?.Trim() ?? string.Empty;
        NowSingingSongText.Visibility = string.IsNullOrWhiteSpace(NowSingingSongText.Text)
            ? Visibility.Collapsed : Visibility.Visible;
        NowSingingPanel.Visibility = Visibility.Visible;
        AnimateEnhancementPanel(NowSingingPanel);

        _nowSingingTimer.Stop();
        var seconds = double.IsFinite(duration.TotalSeconds)
            ? Math.Clamp(duration.TotalSeconds, 3, 15)
            : Math.Clamp(_enhancementSettings.NowSingingSeconds, 3, 15);
        _nowSingingTimer.Interval = TimeSpan.FromSeconds(seconds);
        _nowSingingTimer.Start();
    }

    internal void ClearNowSinging()
    {
        _nowSingingTimer.Stop();
        HideEnhancementPanel(NowSingingPanel);
    }

    internal void ShowTemporaryAnnouncement(string text, TimeSpan duration)
    {
        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0) return;

        AnnouncementText.Text = text;
        AnnouncementPanel.Visibility = Visibility.Visible;
        AnimateEnhancementPanel(AnnouncementPanel);
        _announcementTimer.Stop();
        var seconds = double.IsFinite(duration.TotalSeconds) ? Math.Clamp(duration.TotalSeconds, 5, 30) : 8;
        _announcementTimer.Interval = TimeSpan.FromSeconds(seconds);
        _announcementTimer.Start();
    }

    internal void ClearTemporaryAnnouncement()
    {
        _announcementTimer.Stop();
        HideEnhancementPanel(AnnouncementPanel);
    }

    private void SetEnhancementPanelVisible(FrameworkElement panel, bool visible, bool animate)
    {
        if (!visible)
        {
            HideEnhancementPanel(panel);
            return;
        }
        if (panel.Visibility == Visibility.Visible && !animate) return;
        panel.Visibility = Visibility.Visible;
        if (animate) AnimateEnhancementPanel(panel);
        else ResetEnhancementPanel(panel);
    }

    private static void HideEnhancementPanel(FrameworkElement panel)
    {
        panel.BeginAnimation(OpacityProperty, null);
        panel.Opacity = 1;
        panel.RenderTransform = Transform.Identity;
        panel.Visibility = Visibility.Collapsed;
    }

    private static void ResetEnhancementPanel(FrameworkElement panel)
    {
        panel.BeginAnimation(OpacityProperty, null);
        panel.Opacity = 1;
        panel.RenderTransform = Transform.Identity;
    }

    private void AnimateEnhancementPanel(FrameworkElement panel)
    {
        ResetEnhancementPanel(panel);
        var style = ResolveOverlayTransition(_enhancementSettings.OverlayTransition);
        if (style == "Cut") return;

        var duration = TimeSpan.FromMilliseconds(360);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        panel.Opacity = 0;
        panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });

        switch (style)
        {
            case "Slide":
            {
                var transform = new TranslateTransform(-90, 0);
                panel.RenderTransform = transform;
                transform.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(-90, 0, duration) { EasingFunction = ease });
                break;
            }
            case "Zoom":
            {
                var transform = new ScaleTransform(0.82, 0.82);
                panel.RenderTransform = transform;
                transform.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(0.82, 1, duration) { EasingFunction = ease });
                transform.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(0.82, 1, duration) { EasingFunction = ease });
                break;
            }
            case "Pop":
            {
                var transform = new ScaleTransform(1.18, 1.18);
                panel.RenderTransform = transform;
                var popEase = new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseOut };
                transform.BeginAnimation(ScaleTransform.ScaleXProperty,
                    new DoubleAnimation(1.18, 1, TimeSpan.FromMilliseconds(430)) { EasingFunction = popEase });
                transform.BeginAnimation(ScaleTransform.ScaleYProperty,
                    new DoubleAnimation(1.18, 1, TimeSpan.FromMilliseconds(430)) { EasingFunction = popEase });
                break;
            }
        }
    }

    private static string ResolveOverlayTransition(string? requested)
    {
        requested = requested?.Trim() ?? "Slide";
        if (requested != "Random") return requested;
        var options = new[] { "Fade", "Slide", "Zoom", "Pop" };
        return options[Random.Shared.Next(options.Length)];
    }
}
