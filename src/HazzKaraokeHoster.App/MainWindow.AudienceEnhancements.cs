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
    private CheckBox? _autoSingerCallUpCheck;
    private Slider? _singerCallUpSecondsSlider;
    private TextBlock? _singerCallUpSecondsText;
    private CheckBox? _showQueueStatusCheck;
    private CheckBox? _showVenueHeaderCheck;
    private TextBox? _venueTitleTextBox;
    private ComboBox? _overlayTransitionCombo;
    private TextBox? _announcementTextBox;
    private Slider? _announcementSecondsSlider;
    private TextBlock? _announcementSecondsText;
    private AudienceOverlayStyleEditor? _audienceStyleEditor;
    private DispatcherTimer? _audienceEnhancementStatusTimer;

    internal void InitializeAudienceEnhancementsTest()
    {
        if (_audienceEnhancementTestInitialized) return;
        _audienceEnhancementTestInitialized = true;

        ConvertLoadNextButtonToCallNextSinger();

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
            Content = "Show NEXT SINGER call-up card",
            IsChecked = settings.ShowSingerCallUp,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        callUpRow.Children.Add(_showSingerCallUpCheck);
        _autoSingerCallUpCheck = new CheckBox
        {
            Content = "Auto after song",
            IsChecked = settings.AutoSingerCallUp,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "If enabled, show the next-singer card automatically when karaoke finishes. The main CALL NEXT SINGER button works independently and also loads the track."
        };
        callUpRow.Children.Add(_autoSingerCallUpCheck);
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
        var previewCallUp = new Button { Content = "PREVIEW", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(8, 0, 2, 0) };
        previewCallUp.Click += (_, _) => ShowSingerCallUpForNextTest();
        callUpRow.Children.Add(previewCallUp);
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
            ToolTip = "Editable venue/event message shown between songs."
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
            MaxLength = 300,
            Text = settings.AnnouncementText,
            Margin = new Thickness(8, 0, 5, 0),
            ToolTip = "Temporary editable message shown prominently on the audience screen."
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

        _audienceStyleEditor = new AudienceOverlayStyleEditor(settings);
        _audienceStyleEditor.SettingsChanged += (_, _) => AudienceEnhancementControlChanged();
        host.Children.Add(_audienceStyleEditor);

        _slideshowSecondsSlider.ValueChanged += (_, _) => AudienceEnhancementControlChanged();
        _slideshowTransitionCombo.SelectionChanged += (_, _) => AudienceEnhancementControlChanged();
        _comingUpCountCombo.SelectionChanged += (_, _) => AudienceEnhancementControlChanged();
        _showNowSingingCheck.Checked += (_, _) => AudienceEnhancementControlChanged();
        _showNowSingingCheck.Unchecked += (_, _) => AudienceEnhancementControlChanged();
        _nowSingingSecondsSlider.ValueChanged += (_, _) => AudienceEnhancementControlChanged();
        _showSingerCallUpCheck.Checked += (_, _) => AudienceEnhancementControlChanged();
        _showSingerCallUpCheck.Unchecked += (_, _) => AudienceEnhancementControlChanged();
        _autoSingerCallUpCheck.Checked += (_, _) => AudienceEnhancementControlChanged();
        _autoSingerCallUpCheck.Unchecked += (_, _) => AudienceEnhancementControlChanged();
        _singerCallUpSecondsSlider.ValueChanged += (_, _) => AudienceEnhancementControlChanged();
        _showQueueStatusCheck.Checked += (_, _) => AudienceEnhancementControlChanged();
        _showQueueStatusCheck.Unchecked += (_, _) => AudienceEnhancementControlChanged();
        _showVenueHeaderCheck.Checked += (_, _) => AudienceEnhancementControlChanged();
        _showVenueHeaderCheck.Unchecked += (_, _) => AudienceEnhancementControlChanged();
        _venueTitleTextBox.TextChanged += (_, _) => AudienceEnhancementControlChanged();
        _overlayTransitionCombo.SelectionChanged += (_, _) => AudienceEnhancementControlChanged();
        _announcementTextBox.TextChanged += (_, _) => AudienceEnhancementControlChanged();
        _announcementSecondsSlider.ValueChanged += (_, _) => AudienceEnhancementControlChanged();

        AudienceBackgroundFileText.LayoutUpdated += (_, _) => UpdateAudienceSlideshowTestLabel();
        KaraokeMedia.MediaOpened += (_, _) => ShowNowSingingForActiveSingerTest();
        KaraokeMedia.MediaEnded += async (_, _) =>
        {
            _audience?.ClearNowSinging();
            await Task.Delay(900);
            if (!IsLoaded) return;
            RefreshAudienceBroadcastStatusTest();
            if (ReadAudienceEnhancementControls().AutoSingerCallUp)
                ShowSingerCallUpForNextTest();
        };

        _audienceEnhancementStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _audienceEnhancementStatusTimer.Tick += (_, _) => RefreshAudienceBroadcastStatusTest();
        _audienceEnhancementStatusTimer.Start();
        Closed += (_, _) => _audienceEnhancementStatusTimer?.Stop();

        _audienceEnhancementControlsLoading = false;
        AudienceEnhancementControlChanged();
    }

    private void ConvertLoadNextButtonToCallNextSinger()
    {
        var button = FindButtonByContent(this, "LOAD NEXT SINGER");
        if (button is null) return;
        button.Content = "CALL NEXT SINGER";
        button.ToolTip = "Load the next singer's queued karaoke track ready to play, apply their saved key/sync, and show the editable audience call-up card.";
        button.Click -= LoadNextSinger_Click;
        button.Click += CallNextSingerMain_Click;
    }

    private static Button? FindButtonByContent(DependencyObject root, string content)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button button && string.Equals(button.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase))
                return button;
            var nested = FindButtonByContent(child, content);
            if (nested is not null) return nested;
        }
        return null;
    }

    private async void CallNextSingerMain_Click(object sender, RoutedEventArgs e)
    {
        if (_karaokePlaying || _karaokePaused || _karaokePresentationActive)
        {
            SearchStatus.Text = "Fade Stop the current karaoke song before calling/loading the next singer.";
            return;
        }

        var loaded = await LoadNextSingerAsync();
        if (!loaded || _activeSinger is null || _activeSingerSong is null) return;

        var settings = ReadAudienceEnhancementControls();
        if (_audience is not null && settings.ShowSingerCallUp)
        {
            _audience.ApplyEnhancementSettings(settings);
            _audience.SetEnhancementPlaybackActive(false);
            _audience.ShowSingerCallUp(_activeSinger.SingerName, SongDisplay(_activeSingerSong), TimeSpan.FromSeconds(settings.SingerCallUpSeconds));
        }

        SearchStatus.Text = $"{_activeSinger.SingerName} called • {_activeSingerSong.SongTitle} loaded and ready • press PLAY when the singer is at the microphone";
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = Brushes.White
    };

    private AudienceEnhancementSettings ReadAudienceEnhancementControls()
    {
        var settings = AudienceEnhancementSettingsStore.Load();
        settings.SlideshowSeconds = _slideshowSecondsSlider?.Value ?? settings.SlideshowSeconds;
        settings.SlideshowTransition = _slideshowTransitionCombo?.SelectedItem?.ToString() ?? settings.SlideshowTransition;
        settings.ComingUpCount = _comingUpCountCombo?.SelectedItem is int count ? count : settings.ComingUpCount;
        settings.ShowNowSinging = _showNowSingingCheck?.IsChecked == true;
        settings.NowSingingSeconds = _nowSingingSecondsSlider?.Value ?? settings.NowSingingSeconds;
        settings.ShowSingerCallUp = _showSingerCallUpCheck?.IsChecked == true;
        settings.AutoSingerCallUp = _autoSingerCallUpCheck?.IsChecked == true;
        settings.SingerCallUpSeconds = _singerCallUpSecondsSlider?.Value ?? settings.SingerCallUpSeconds;
        settings.ShowQueueStatus = _showQueueStatusCheck?.IsChecked == true;
        settings.ShowVenueHeader = _showVenueHeaderCheck?.IsChecked == true;
        settings.VenueTitle = _venueTitleTextBox?.Text ?? settings.VenueTitle;
        settings.OverlayTransition = _overlayTransitionCombo?.SelectedItem?.ToString() ?? settings.OverlayTransition;
        settings.AnnouncementText = _announcementTextBox?.Text ?? settings.AnnouncementText;
        settings.AnnouncementSeconds = _announcementSecondsSlider?.Value ?? settings.AnnouncementSeconds;
        _audienceStyleEditor?.WriteTo(settings);
        return AudienceEnhancementSettingsStore.Normalize(settings);
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
        var settings = ReadAudienceEnhancementControls();
        var text = settings.AnnouncementText.Trim();
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
        _audience.ApplyEnhancementSettings(settings);
        _audience.ShowTemporaryAnnouncement(text, TimeSpan.FromSeconds(settings.AnnouncementSeconds));
        SearchStatus.Text = $"Audience announcement shown for {settings.AnnouncementSeconds:0} seconds.";
    }

    private void ShowNowSingingForActiveSingerTest()
    {
        if (_audience is null) return;
        _audience.SetEnhancementPlaybackActive(true);
        var settings = ReadAudienceEnhancementControls();
        if (!settings.ShowNowSinging || _activeSinger is null) return;
        _audience.ApplyEnhancementSettings(settings);
        _audience.ShowNowSinging(
            _activeSinger.SingerName,
            _activeSingerSong is null ? string.Empty : SongDisplay(_activeSingerSong),
            TimeSpan.FromSeconds(settings.NowSingingSeconds));
    }

    private void ShowSingerCallUpForNextTest()
    {
        if (_audience is null) return;
        var settings = ReadAudienceEnhancementControls();
        if (!settings.ShowSingerCallUp || _karaokePlaying || _karaokePaused) return;

        var candidates = _queue.Where(x => !x.IsHeld && x.Songs.Count > 0).ToList();
        if (candidates.Count == 0) return;
        var next = candidates.FirstOrDefault(x => _activeSinger is null || x.Id != _activeSinger.Id) ?? candidates[0];
        _audience.ApplyEnhancementSettings(settings);
        _audience.SetEnhancementPlaybackActive(false);
        _audience.ShowSingerCallUp(
            next.SingerName,
            next.NextSong is null ? string.Empty : SongDisplay(next.NextSong),
            TimeSpan.FromSeconds(settings.SingerCallUpSeconds));
    }

    private static string SongDisplay(HazzKaraokeHoster.Core.Models.SingerSongEntry song)
        => string.IsNullOrWhiteSpace(song.Artist) ? song.SongTitle : $"{song.SongTitle} — {song.Artist}";

    private void RefreshAudienceBroadcastStatusTest()
    {
        if (_audience is null) return;
        var settings = ReadAudienceEnhancementControls();
        var active = _queue.Where(x => !x.IsHeld).ToList();
        var held = _queue.Count(x => x.IsHeld);
        var playable = active.Where(x => x.NextSong is not null).ToList();
        var seconds = playable.Sum(x => x.NextSong?.DurationSeconds is double duration && double.IsFinite(duration) && duration > 0
            ? duration : 240d);
        var time = FormatQueueEstimate(seconds);

        string queueStatus;
        if (active.Count == 0 && held == 0)
        {
            queueStatus = settings.QueueEmptyText;
        }
        else
        {
            var singerWord = active.Count == 1 ? "singer" : "singers";
            var holdPart = held > 0 ? $" • {held} HOLD" : string.Empty;
            var timePart = playable.Count > 0 ? $" • approx {time}" : string.Empty;
            queueStatus = settings.QueueStatusTemplate
                .Replace("{active}", active.Count.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{singerWord}", singerWord, StringComparison.OrdinalIgnoreCase)
                .Replace("{hold}", held.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{holdPart}", holdPart, StringComparison.OrdinalIgnoreCase)
                .Replace("{playable}", playable.Count.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{time}", time, StringComparison.OrdinalIgnoreCase)
                .Replace("{timePart}", timePart, StringComparison.OrdinalIgnoreCase);
        }

        _audience.ApplyEnhancementSettings(settings);
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
