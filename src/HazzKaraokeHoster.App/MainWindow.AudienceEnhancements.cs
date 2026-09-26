using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

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
    private CheckBox? _showSingerCallUpCheck;
    private Slider? _singerCallUpSecondsSlider;
    private TextBlock? _singerCallUpSecondsText;
    private CheckBox? _showQueueStatusCheck;
    private CheckBox? _showVenueHeaderCheck;
    private TextBox? _venueTitleTextBox;
    private ComboBox? _overlayTransitionCombo;
    private TextBox? _announcementTextBox;
    private Slider? _announcementSecondsSlider;
    private TextBlock? _announcementSecondsText;
    private DispatcherTimer? _audienceEnhancementStatusTimer;

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

        var callUpRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 2) };
        _showSingerCallUpCheck = new CheckBox
        {
            Content = "Automatic NEXT SINGER call-up",
            IsChecked = settings.ShowSingerCallUp,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "After a karaoke song finishes, briefly show the next singer in a large call-up card."
        };
        callUpRow.Children.Add(_showSingerCallUpCheck);
        callUpRow.Children.Add(Label("for"));
        _singerCallUpSecondsSlider = new Slider
        {
            Minimum = 3,
            Maximum = 15,
            Value = settings.SingerCallUpSeconds,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Width = 90,
            Margin = new Thickness(8, 0, 4, 0)
        };
        _singerCallUpSecondsText = Label($"{settings.SingerCallUpSeconds:0}s");
        _singerCallUpSecondsText.Width = 32;
        callUpRow.Children.Add(_singerCallUpSecondsSlider);
        callUpRow.Children.Add(_singerCallUpSecondsText);
        var callNextNow = new Button { Content = "CALL NEXT NOW", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(8, 0, 2, 0) };
        callNextNow.Click += (_, _) => ShowSingerCallUpForNextTest();
        callUpRow.Children.Add(callNextNow);
        host.Children.Add(callUpRow);

        var broadcastRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 2) };
        _showQueueStatusCheck = new CheckBox
        {
            Content = "Show queue status / approx time",
            IsChecked = settings.ShowQueueStatus,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0)
        };
        broadcastRow.Children.Add(_showQueueStatusCheck);
        _showVenueHeaderCheck = new CheckBox
        {
            Content = "Venue header",
            IsChecked = settings.ShowVenueHeader,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };
        broadcastRow.Children.Add(_showVenueHeaderCheck);
        _venueTitleTextBox = new TextBox
        {
            Width = 230,
            MaxLength = 120,
            Text = settings.VenueTitle,
            Margin = new Thickness(3, 0, 12, 0),
            ToolTip = "Optional venue/event name displayed at the top of the audience screen between songs."
        };
        broadcastRow.Children.Add(_venueTitleTextBox);
        broadcastRow.Children.Add(Label("Overlay animation"));
        _overlayTransitionCombo = new ComboBox
        {
            Width = 92,
            Margin = new Thickness(8, 0, 0, 0),
            ItemsSource = new[] { "Cut", "Fade", "Slide", "Zoom", "Pop", "Random" },
            SelectedItem = settings.OverlayTransition
        };
        broadcastRow.Children.Add(_overlayTransitionCombo);
        host.Children.Add(broadcastRow);

        var announcementRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 2) };
        announcementRow.Children.Add(Label("Audience announcement"));
        _announcementTextBox = new TextBox
        {
            Width = 280,
            Margin = new Thickness(8, 0, 5, 0),
            ToolTip = "Temporary message shown prominently on the audience screen."
        };
        announcementRow.Children.Add(_announcementTextBox);
        announcementRow.Children.Add(Label("for"));
        _announcementSecondsSlider = new Slider
        {
            Minimum = 5,
            Maximum = 30,
            Value = settings.AnnouncementSeconds,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Width = 100,
            Margin = new Thickness(8, 0, 4, 0)
        };
        _announcementSecondsText = Label($"{settings.AnnouncementSeconds:0}s");
        _announcementSecondsText.Width = 36;
        announcementRow.Children.Add(_announcementSecondsSlider);
        announcementRow.Children.Add(_announcementSecondsText);
        var showAnnouncement = new Button { Content = "SHOW", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2) };
        var clearAnnouncement = new Button { Content = "CLEAR", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(2) };
        showAnnouncement.Click += (_, _) => ShowAudienceAnnouncementTest();
        clearAnnouncement.Click += (_, _) => _audience?.ClearTemporaryAnnouncement();
        announcementRow.Children.Add(showAnnouncement);
        announcementRow.Children.Add(clearAnnouncement);
        host.Children.Add(announcementRow);

        _slideshowSecondsSlider.ValueChanged += (_, _) => AudienceEnhancementControlChanged();
        _slideshowTransitionCombo.SelectionChanged += (_, _) => AudienceEnhancementControlChanged();
        _comingUpCountCombo.SelectionChanged += (_, _) => AudienceEnhancementControlChanged();
        _showNowSingingCheck.Checked += (_, _) => AudienceEnhancementControlChanged();
        _showNowSingingCheck.Unchecked += (_, _) => AudienceEnhancementControlChanged();
        _nowSingingSecondsSlider.ValueChanged += (_, _) => AudienceEnhancementControlChanged();
        _showSingerCallUpCheck.Checked += (_, _) => AudienceEnhancementControlChanged();
        _showSingerCallUpCheck.Unchecked += (_, _) => AudienceEnhancementControlChanged();
        _singerCallUpSecondsSlider.ValueChanged += (_, _) => AudienceEnhancementControlChanged();
        _showQueueStatusCheck.Checked += (_, _) => AudienceEnhancementControlChanged();
        _showQueueStatusCheck.Unchecked += (_, _) => AudienceEnhancementControlChanged();
        _showVenueHeaderCheck.Checked += (_, _) => AudienceEnhancementControlChanged();
        _showVenueHeaderCheck.Unchecked += (_, _) => AudienceEnhancementControlChanged();
        _venueTitleTextBox.TextChanged += (_, _) => AudienceEnhancementControlChanged();
        _overlayTransitionCombo.SelectionChanged += (_, _) => AudienceEnhancementControlChanged();
        _announcementSecondsSlider.ValueChanged += (_, _) => AudienceEnhancementControlChanged();

        AudienceBackgroundFileText.LayoutUpdated += (_, _) => UpdateAudienceSlideshowTestLabel();
        KaraokeMedia.MediaOpened += (_, _) => ShowNowSingingForActiveSingerTest();
        KaraokeMedia.MediaEnded += async (_, _) =>
        {
            _audience?.ClearNowSinging();
            await Task.Delay(900);
            if (!IsLoaded) return;
            RefreshAudienceBroadcastStatusTest();
            ShowSingerCallUpForNextTest();
        };

        _audienceEnhancementStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _audienceEnhancementStatusTimer.Tick += (_, _) => RefreshAudienceBroadcastStatusTest();
        _audienceEnhancementStatusTimer.Start();
        Closed += (_, _) => _audienceEnhancementStatusTimer?.Stop();

        _audienceEnhancementControlsLoading = false;
        UpdateAudienceSlideshowTestLabel();
        RefreshAudienceBroadcastStatusTest();
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
            NowSingingSeconds = _nowSingingSecondsSlider?.Value ?? 7,
            ShowSingerCallUp = _showSingerCallUpCheck?.IsChecked == true,
            SingerCallUpSeconds = _singerCallUpSecondsSlider?.Value ?? 8,
            ShowQueueStatus = _showQueueStatusCheck?.IsChecked == true,
            ShowVenueHeader = _showVenueHeaderCheck?.IsChecked == true,
            VenueTitle = _venueTitleTextBox?.Text?.Trim() ?? string.Empty,
            OverlayTransition = _overlayTransitionCombo?.SelectedItem?.ToString() ?? "Slide",
            AnnouncementSeconds = _announcementSecondsSlider?.Value ?? 8
        };
    }

    private void AudienceEnhancementControlChanged()
    {
        if (_audienceEnhancementControlsLoading) return;
        var settings = ReadAudienceEnhancementControls();
        if (_slideshowSecondsText is not null) _slideshowSecondsText.Text = $"{settings.SlideshowSeconds:0} sec";
        if (_nowSingingSecondsText is not null) _nowSingingSecondsText.Text = $"{settings.NowSingingSeconds:0}s";
        if (_singerCallUpSecondsText is not null) _singerCallUpSecondsText.Text = $"{settings.SingerCallUpSeconds:0}s";
        if (_announcementSecondsText is not null) _announcementSecondsText.Text = $"{settings.AnnouncementSeconds:0}s";
        AudienceEnhancementSettingsStore.Save(settings);
        _audience?.ApplyEnhancementSettings(settings);
        UpdateAudienceSlideshowTestLabel();
        RefreshAudienceBroadcastStatusTest();
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
        var seconds = _announcementSecondsSlider?.Value ?? 8;
        _audience.ShowTemporaryAnnouncement(text, TimeSpan.FromSeconds(seconds));
        SearchStatus.Text = $"Audience announcement shown for {seconds:0} seconds.";
    }

    private void ShowNowSingingForActiveSingerTest()
    {
        if (_audience is null) return;
        _audience.SetEnhancementPlaybackActive(true);
        var settings = ReadAudienceEnhancementControls();
        if (!settings.ShowNowSinging || _activeSinger is null) return;
        var singer = _activeSinger.SingerName;
        var song = _activeSingerSong is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(_activeSingerSong.Artist)
                ? _activeSingerSong.SongTitle
                : $"{_activeSingerSong.SongTitle} — {_activeSingerSong.Artist}";
        _audience.ShowNowSinging(singer, song, TimeSpan.FromSeconds(settings.NowSingingSeconds));
    }

    private void ShowSingerCallUpForNextTest()
    {
        if (_audience is null) return;
        var settings = ReadAudienceEnhancementControls();
        if (!settings.ShowSingerCallUp || _karaokePlaying || _karaokePaused) return;

        var candidates = _queue.Where(x => !x.IsHeld && x.Songs.Count > 0).ToList();
        if (candidates.Count == 0) return;
        var next = candidates.FirstOrDefault(x => _activeSinger is null || x.Id != _activeSinger.Id) ?? candidates[0];
        var song = next.NextSong;
        var songText = song is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(song.Artist)
                ? song.SongTitle
                : $"{song.SongTitle} — {song.Artist}";
        _audience.SetEnhancementPlaybackActive(false);
        _audience.ShowSingerCallUp(next.SingerName, songText, TimeSpan.FromSeconds(settings.SingerCallUpSeconds));
    }

    private void RefreshAudienceBroadcastStatusTest()
    {
        if (_audience is null) return;
        var settings = ReadAudienceEnhancementControls();
        var active = _queue.Where(x => !x.IsHeld).ToList();
        var held = _queue.Count(x => x.IsHeld);
        var playable = active.Where(x => x.NextSong is not null).ToList();
        var seconds = playable.Sum(x => x.NextSong?.DurationSeconds is double duration && double.IsFinite(duration) && duration > 0
            ? duration : 240d);

        string queueStatus;
        if (active.Count == 0 && held == 0)
        {
            queueStatus = "Singer rotation empty";
        }
        else
        {
            var singerWord = active.Count == 1 ? "singer" : "singers";
            queueStatus = $"{active.Count} {singerWord}";
            if (held > 0) queueStatus += $" • {held} HOLD";
            if (playable.Count > 0) queueStatus += $" • approx {FormatQueueEstimate(seconds)}";
        }

        _audience.SetEnhancementPlaybackActive(_karaokePlaying || _karaokePaused);
        _audience.SetBroadcastStatus(queueStatus, settings.VenueTitle);
    }

    private static string FormatQueueEstimate(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return "0 min";
        var minutes = Math.Max(1, (int)Math.Ceiling(seconds / 60d));
        if (minutes < 60) return $"{minutes} min";
        var hours = minutes / 60;
        var remainder = minutes % 60;
        return remainder == 0 ? $"{hours}h" : $"{hours}h {remainder}m";
    }
}
