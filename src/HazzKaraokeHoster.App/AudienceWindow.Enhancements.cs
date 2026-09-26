using System.Windows;
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
        _enhancementSettings = settings ?? new AudienceEnhancementSettings();
        SetSlideshowEnhancementOptions(_enhancementSettings.SlideshowSeconds, _enhancementSettings.SlideshowTransition);
        EnforceComingUpCount();
        if (!_enhancementSettings.ShowNowSinging) ClearNowSinging();
        if (!_enhancementSettings.ShowSingerCallUp) ClearSingerCallUp();
        RefreshBroadcastBars(animate: false);
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
