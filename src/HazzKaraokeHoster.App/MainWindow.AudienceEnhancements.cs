using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private bool _audienceEnhancementTestInitialized;
    private bool _audienceEnhancementControlsLoading;
    private Slider? _slideshowSecondsSlider;
    private TextBlock? _slideshowSecondsText;
    private ComboBox? _slideshowTransitionCombo;
    private ComboBox? _comingUpCountCombo;
    private CheckBox? _showNowSingingCheck;
    private Slider? _nowSingingSecondsSlider;
    private TextBlock? _nowSingingSecondsText;
    private TextBox? _announcementTextBox;

    internal void InitializeAudienceEnhancementsTest()
    {
        if (_audienceEnhancementTestInitialized) return;
        _audienceEnhancementTestInitialized = true;

        var gifRow = BackgroundGifSpeedSlider?.Parent as Panel;
        var host = gifRow?.Parent as Panel;
        if (host is null)
        {
            App.WriteDiagnostic("AUDIENCE ENHANCEMENTS", "Could not locate the audience slideshow settings host.");
            return;
        }

        var settings = AudienceEnhancementSettingsStore.Load();
        _audienceEnhancementControlsLoading = true;

        var slideRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 2) };
        slideRow.Children.Add(Label("Next slide"));
        _slideshowSecondsSlider = new Slider
        {
            Minimum = 20,
            Maximum = 60,
            Value = settings.SlideshowSeconds,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            Width = 150,
            Margin = new Thickness(8, 0, 4, 0),
            ToolTip = "Time between singer-display slideshow items: 20 seconds to 1 minute."
        };
        _slideshowSecondsText = Label($"{settings.SlideshowSeconds:0} sec");
        _slideshowSecondsText.Width = 55;
        slideRow.Children.Add(_slideshowSecondsSlider);
        slideRow.Children.Add(_slideshowSecondsText);
        slideRow.Children.Add(Label("Transition"));
        _slideshowTransitionCombo = new ComboBox
        {
            Width = 125,
            Margin = new Thickness(8, 0, 12, 0),
            ItemsSource = new[] { "Cut", "Fade", "Slide Left", "Slide Right", "Zoom", "Random" },
            SelectedItem = settings.SlideshowTransition,
            ToolTip = "Transition used when the singer-display slideshow changes item. Random chooses a different effect each time."
        };
        slideRow.Children.Add(_slideshowTransitionCombo);
        host.Children.Add(slideRow);

        var singerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 2) };
        singerRow.Children.Add(Label("Coming up"));
        _comingUpCountCombo = new ComboBox
        {
            Width = 58,
            Margin = new Thickness(8, 0, 14, 0),
            ItemsSource = new[] { 1, 2, 3, 4 },
            SelectedItem = settings.ComingUpCount,
            ToolTip = "How many upcoming singers are shown between songs."
        };
        singerRow.Children.Add(_comingUpCountCombo);
        _showNowSingingCheck = new CheckBox
        {
            Content = "Show NOW SINGING",
            IsChecked = settings.ShowNowSinging,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 8, 0),
            ToolTip = "Briefly show the current singer and song when karaoke starts."
        };
        singerRow.Children.Add(_showNowSingingCheck);
        singerRow.Children.Add(Label("for"));
        _nowSingingSecondsSlider = new Slider
        {
            Minimum = 3,
            Maximum = 15,
            Value = settings.NowSingingSeconds,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Width = 90,
            Margin = new Thickness(8, 0, 4, 0)
        };
        _nowSingingSecondsText = Label($"{settings.NowSingingSeconds:0}s");
        _nowSingingSecondsText.Width = 32;
        singerRow.Children.Add(_nowSingingSecondsSlider);
        singerRow.Children.Add(_nowSingingSecondsText);
        host.Children.Add(singerRow);

        var announcementRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 2) };
        announcementRow.Children.Add(Label("Audience announcement"));
        _announcementTextBox = new TextBox
        {
            Width = 280,
            Margin = new Thickness(8, 0, 5, 0),
            ToolTip = "Temporary message shown prominently on the audience screen."
        };
        var showAnnouncement = new Button { Content = "SHOW 8 SEC", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2) };
        var clearAnnouncement = new Button { Content = "CLEAR", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2) };
        showAnnouncement.Click += (_, _) => ShowAudienceAnnouncementTest();
        clearAnnouncement.Click += (_, _) => _audience?.ClearTemporaryAnnouncement();
        announcementRow.Children.Add(_announcementTextBox);
        announcementRow.Children.Add(showAnnouncement);
        announcementRow.Children.Add(clearAnnouncement);
        host.Children.Add(announcementRow);

        _slideshowSecondsSlider.ValueChanged += (_, _) => AudienceEnhancementControlChanged();
        _slideshowTransitionCombo.SelectionChanged += (_, _) => AudienceEnhancementControlChanged();
        _comingUpCountCombo.SelectionChanged += (_, _) => AudienceEnhancementControlChanged();
        _showNowSingingCheck.Checked += (_, _) => AudienceEnhancementControlChanged();
        _showNowSingingCheck.Unchecked += (_, _) => AudienceEnhancementControlChanged();
        _nowSingingSecondsSlider.ValueChanged += (_, _) => AudienceEnhancementControlChanged();

        AudienceBackgroundFileText.LayoutUpdated += (_, _) => UpdateAudienceSlideshowTestLabel();
        KaraokeMedia.MediaOpened += (_, _) => ShowNowSingingForActiveSingerTest();
        KaraokeMedia.MediaEnded += (_, _) => _audience?.ClearNowSinging();

        _audienceEnhancementControlsLoading = false;
        UpdateAudienceSlideshowTestLabel();
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = Brushes.White
    };

    private AudienceEnhancementSettings ReadAudienceEnhancementControls()
    {
        return new AudienceEnhancementSettings
        {
            SlideshowSeconds = _slideshowSecondsSlider?.Value ?? 60,
            SlideshowTransition = _slideshowTransitionCombo?.SelectedItem?.ToString() ?? "Fade",
            ComingUpCount = _comingUpCountCombo?.SelectedItem is int count ? count : 4,
            ShowNowSinging = _showNowSingingCheck?.IsChecked == true,
            NowSingingSeconds = _nowSingingSecondsSlider?.Value ?? 7
        };
    }

    private void AudienceEnhancementControlChanged()
    {
        if (_audienceEnhancementControlsLoading) return;
        var settings = ReadAudienceEnhancementControls();
        if (_slideshowSecondsText is not null) _slideshowSecondsText.Text = $"{settings.SlideshowSeconds:0} sec";
        if (_nowSingingSecondsText is not null) _nowSingingSecondsText.Text = $"{settings.NowSingingSeconds:0}s";
        AudienceEnhancementSettingsStore.Save(settings);
        _audience?.ApplyEnhancementSettings(settings);
        UpdateAudienceSlideshowTestLabel();
    }

    private void UpdateAudienceSlideshowTestLabel()
    {
        if (AudienceBackgroundFileText is null || string.IsNullOrWhiteSpace(_audienceBackgroundFolderPath)) return;
        var seconds = _slideshowSecondsSlider?.Value ?? AudienceEnhancementSettingsStore.Load().SlideshowSeconds;
        var expected = $"Slideshow ({seconds:0}s): {_audienceBackgroundFolderPath}";
        if (!string.Equals(AudienceBackgroundFileText.Text, expected, StringComparison.Ordinal))
            AudienceBackgroundFileText.Text = expected;
    }

    private void ShowAudienceAnnouncementTest()
    {
        var text = _announcementTextBox?.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            SearchStatus.Text = "Type an audience announcement first.";
            return;
        }
        if (_audience is null)
        {
            SearchStatus.Text = "Open or send the Audience Display first, then show the announcement.";
            return;
        }
        _audience.ShowTemporaryAnnouncement(text, TimeSpan.FromSeconds(8));
        SearchStatus.Text = "Audience announcement shown for 8 seconds.";
    }

    private void ShowNowSingingForActiveSingerTest()
    {
        var settings = AudienceEnhancementSettingsStore.Load();
        if (!settings.ShowNowSinging || _audience is null || _activeSinger is null) return;
        var singer = _activeSinger.SingerName;
        var song = _activeSingerSong is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(_activeSingerSong.Artist)
                ? _activeSingerSong.SongTitle
                : $"{_activeSingerSong.SongTitle} — {_activeSingerSong.Artist}";
        _audience.ShowNowSinging(singer, song, TimeSpan.FromSeconds(settings.NowSingingSeconds));
    }
}
