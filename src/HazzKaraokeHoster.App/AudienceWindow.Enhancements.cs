using System.Windows;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

public partial class AudienceWindow
{
    private readonly DispatcherTimer _nowSingingTimer = new();
    private readonly DispatcherTimer _announcementTimer = new();
    private bool _audienceEnhancementHostHooked;
    private AudienceEnhancementSettings _enhancementSettings = new();

    private void AudienceEnhancementHost_Loaded(object sender, RoutedEventArgs e)
    {
        if (_audienceEnhancementHostHooked) return;
        _audienceEnhancementHostHooked = true;

        _nowSingingTimer.Tick += (_, _) => ClearNowSinging();
        _announcementTimer.Tick += (_, _) => ClearTemporaryAnnouncement();
        NextSingersStack.LayoutUpdated += (_, _) => EnforceComingUpCount();
        Closed += (_, _) =>
        {
            _nowSingingTimer.Stop();
            _announcementTimer.Stop();
        };

        ApplyEnhancementSettings(AudienceEnhancementSettingsStore.Load());
    }

    internal void ApplyEnhancementSettings(AudienceEnhancementSettings settings)
    {
        _enhancementSettings = settings ?? new AudienceEnhancementSettings();
        SetSlideshowEnhancementOptions(_enhancementSettings.SlideshowSeconds, _enhancementSettings.SlideshowTransition);
        EnforceComingUpCount();
        if (!_enhancementSettings.ShowNowSinging) ClearNowSinging();
    }

    private void EnforceComingUpCount()
    {
        var count = Math.Clamp(_enhancementSettings.ComingUpCount, 1, 4);
        for (var i = 0; i < NextSingersStack.Children.Count; i++)
            NextSingersStack.Children[i].Visibility = i < count ? Visibility.Visible : Visibility.Collapsed;
        NextSingerHeadingText.Text = count == 1 ? "UP NEXT" : "COMING UP";
    }

    internal void ShowNowSinging(string singerName, string songText, TimeSpan duration)
    {
        if (!_enhancementSettings.ShowNowSinging || string.IsNullOrWhiteSpace(singerName)) return;

        NowSingingSingerText.Text = singerName.Trim();
        NowSingingSongText.Text = songText?.Trim() ?? string.Empty;
        NowSingingSongText.Visibility = string.IsNullOrWhiteSpace(NowSingingSongText.Text)
            ? Visibility.Collapsed : Visibility.Visible;
        NowSingingPanel.Visibility = Visibility.Visible;

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
        NowSingingPanel.Visibility = Visibility.Collapsed;
    }

    internal void ShowTemporaryAnnouncement(string text, TimeSpan duration)
    {
        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0) return;

        AnnouncementText.Text = text;
        AnnouncementPanel.Visibility = Visibility.Visible;
        _announcementTimer.Stop();
        var seconds = double.IsFinite(duration.TotalSeconds) ? Math.Clamp(duration.TotalSeconds, 2, 60) : 8;
        _announcementTimer.Interval = TimeSpan.FromSeconds(seconds);
        _announcementTimer.Start();
    }

    internal void ClearTemporaryAnnouncement()
    {
        _announcementTimer.Stop();
        AnnouncementPanel.Visibility = Visibility.Collapsed;
    }
}
