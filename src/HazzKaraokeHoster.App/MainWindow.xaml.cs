using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using HazzKaraokeHoster.Data;
using HazzKaraokeHoster.Playback;
using HazzKaraokeHoster.Playback.Cdg;
using Microsoft.Win32;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using ListBox = System.Windows.Controls.ListBox;

namespace HazzKaraokeHoster.App;

public partial class MainWindow : Window
{
    private const string SearchSongBatchDataFormat = "HazzSearchSongBatch";
    private const string SideListQueueBatchDataFormat = "HazzSideListQueueBatch";
    private const double KaraokePlaybackVolume = 0.9;
    private const double KaraokeStopHandoffSeconds = 1.5;
    private readonly HazzDatabase _db = new();
    private readonly ILibraryRepository _library;
    private readonly ILibraryImportService _libraryImporter;
    private readonly LibraryRootRepository _libraryRoots;
    private LibraryAutoWatchService? _libraryAutoWatch;
    private readonly ISingerRepository _singers;
    private readonly IKarmaImportService _karma;
    private readonly IBpmStudioImportService _bpmStudio;
    private readonly IExternalLibraryImportService _externalImporter;
    private readonly IExternalSingerHistoryImportService _externalSingerHistoryImporter;
    private readonly IMusicPlaylistRepository _musicPlaylists;
    private readonly CdgTimingController _cdgTiming = new();
    private readonly RealtimePitchAudioPlayer _pitchAudio = new();
    private readonly ObservableCollection<SingerQueueEntry> _queue = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _karaokeVisualTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly DispatcherTimer _musicAutomationTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly byte[] _cdgFrameBuffer = new byte[CdgDecoder.Width * CdgDecoder.Height * 4];

    private enum MusicDeckId { None, Deck1, Deck2 }

    private MusicDeckId _activeMusicDeck = MusicDeckId.None;
    private MusicDeckId _crossfadeFrom = MusicDeckId.None;
    private MusicDeckId _crossfadeTo = MusicDeckId.None;
    private MusicDeckId _resumeMusicDeck = MusicDeckId.None;
    private int _resumeMusicIndex = -1;
    private MusicQueueItem? _resumeMusicItem;
    private int _deck1CurrentIndex = -1;
    private int _deck2CurrentIndex = -1;
    private int _deck1NextIndex;
    private int _deck2NextIndex;
    private double _deck1FadeFactor;
    private double _deck2FadeFactor;
    private bool _crossfadeActive;
    private bool _musicFadeOutForKaraoke;
    private bool _musicFadeInResume;
    private double _musicResumeFadeSeconds = 4.0;
    private bool _musicSuspendedForKaraoke;
    private bool _deck1Paused;
    private bool _deck2Paused;
    private TimeSpan _deck1PausedPosition;
    private TimeSpan _deck2PausedPosition;
    private bool _deck1SeekDragging;
    private bool _deck2SeekDragging;
    private bool _quickSearchMusicActive;
    private bool _quickSearchMusicFadeInActive;
    private DateTime _quickSearchMusicTransitionStartedUtc = DateTime.MinValue;
    private double _quickSearchFadeStartDeck1;
    private double _quickSearchFadeStartDeck2;
    private double _quickSearchTargetVolume = 0.85;
    private SongRecord? _quickSearchSong;
    private bool _quickSearchMusicVideo;
    private DateTime _musicTransitionStartedUtc = DateTime.MinValue;
    private double _fadeOutStartDeck1;
    private double _fadeOutStartDeck2;
    private bool _updatingVisualCrossfader;

    private Point _queueDragStart;
    private SingerQueueEntry? _queueDragItem;
    private Point _searchDragStart;
    private SongRecord? _searchDragSong;
    private Point _musicPlaylistDragStart;
    private MusicQueueItem? _musicPlaylistDragItem;
    private ListBox? _musicPlaylistDragSource;
    private MusicQueueItem? _deck1CurrentItem;
    private MusicQueueItem? _deck2CurrentItem;
    private readonly HashSet<string> _musicPlayedThisSession = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _karaokeStopFadeCts;
    private CancellationTokenSource? _importCts;
    private CancellationTokenSource? _silenceScanCts;
    private string _searchMediaKind = "Karaoke";
    private AudienceWindow? _audience;
    private MusicDeckId _audienceMusicVideoDeck = MusicDeckId.None;
    private MusicArchiveWindow? _musicArchiveWindow;
    private ImportProgressWindow? _bpmImportProgressWindow;
    private LibraryBrowserWindow? _libraryBrowserWindow;
    private IReadOnlyList<DisplayTarget> _displayTargets = Array.Empty<DisplayTarget>();
    private KaraokePackage? _karaokePackage;
    private CdgDecoder? _cdgDecoder;
    private WriteableBitmap? _cdgBitmap;
    private long _lastRenderedCdgVersion = -1;
    private int _keyChange;
    private bool _karaokePlaying;
    private bool _karaokePaused;
    private bool _karaokePresentationActive;
    private DateTime _lastAudienceVideoSyncUtc = DateTime.MinValue;
    private DateTime _lastPitchSyncUtc = DateTime.MinValue;
    private SingerQueueEntry? _activeSinger;
    private SingerSongEntry? _activeSingerSong;
    private bool _activeSingerSongCheckedOut;
    private SongRecord? _currentKaraokeRecord;
    private IReadOnlyList<SongRecord> _alternativeCandidates = Array.Empty<SongRecord>();
    private int _alternativeIndex;
    private string _alternativeArtist = string.Empty;
    private string _alternativeTitle = string.Empty;
    private DateTime _lastTimelineUiUtc = DateTime.MinValue;
    private double _lastPreviewHeight = 250;
    private double _deckBPlayerHeightBeforeSideList = 205;
    private bool _singleDeckMode;
    private bool _autoCrossfadeBeforeSingleDeck = true;
    private string _audienceBackgroundFolderPath = string.Empty;
    private string _audienceBackgroundImagePath = string.Empty;
    private string _audienceLogoImagePath = string.Empty;
    private string _audienceNextHeadingColor = "#FFFFD34D";
    private string _audienceNextPositionColor = "#FFFFD34D";
    private string _audienceNextSingerColor = "#FFFFFFFF";
    private string _audienceNextSongColor = "#FFD8E2EF";
    private string _audienceRotationScrollerColor = "#FFFFFFFF";
    private string _audienceVenueScrollerColor = "#FFFFD34D";
    private string _audienceKamikazeColor = "#FFFFD34D";
    private bool _kamikazeBannerRequested;
    private SingerQueueEntry? _kamikazeSinger;
    private SingerSongEntry? _kamikazeAssignedSong;
    private string _kamikazeLastPath = string.Empty;
    private CancellationTokenSource? _kamikazePickCts;
    private bool _kamikazePickInProgress;
    private bool _karaokeOnlyMode;
    private double _fullModeKaraokeDeckHeight = 250;
    private bool _layoutViewportUpdatePending;
    private readonly DispatcherTimer _musicQueueSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly DispatcherTimer _recoveryCheckpointTimer = new() { Interval = TimeSpan.FromMinutes(2) };
    private bool _restoringMusicDeckQueues;
    private bool _musicQueueStateDirty;
    private readonly LiveShowStateStore _liveShowStateStore = new();
    private readonly DispatcherTimer _liveShowStateSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(650) };
    private bool _restoringLiveShowState;
    private bool _liveShowStateDirty;
    private DateTimeOffset _showStartedUtc = DateTimeOffset.UtcNow;
    private DateTime _lastLedUpdateUtc = DateTime.UtcNow;
    private double _deckALedX = double.NaN;
    private double _deckBLedX = double.NaN;
    private string _deckALedMessage = string.Empty;
    private string _deckBLedMessage = string.Empty;
    private DateTime _lastLiveTimerErrorUtc = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ScheduleViewportLayoutClamp();
        _library = new LibraryRepository(_db);
        _libraryImporter = new LibraryImportService(_db);
        _libraryRoots = new LibraryRootRepository(_db);
        _singers = new SingerRepository(_db);
        _karma = new KarmaImportService(_db, _singers);
        _bpmStudio = new BpmStudioImportService(_db);
        _externalImporter = new ExternalLibraryImportService(_db);
        _externalSingerHistoryImporter = new ExternalSingerHistoryImportService(_db);
        _musicPlaylists = new MusicPlaylistRepository(_db);

        QueueList.ItemsSource = _queue;
        foreach (var font in Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(x => x).Take(500))
        {
            NextFontCombo.Items.Add(font);
            ScrollerFontCombo.Items.Add(font);
            KamikazeFontCombo.Items.Add(font);
        }

        NextFontCombo.SelectedItem = NextFontCombo.Items.Cast<object>()
            .FirstOrDefault(x => string.Equals(x?.ToString(), "Segoe UI", StringComparison.OrdinalIgnoreCase))
            ?? NextFontCombo.Items.Cast<object>().FirstOrDefault();
        ScrollerFontCombo.SelectedItem = NextFontCombo.SelectedItem;
        KamikazeFontCombo.SelectedItem = KamikazeFontCombo.Items.Cast<object>()
            .FirstOrDefault(x => string.Equals(x?.ToString(), "Segoe UI Black", StringComparison.OrdinalIgnoreCase))
            ?? NextFontCombo.SelectedItem;
        UpdateSearchModeUi();

        _cdgTiming.OffsetChanged += (_, value) => Dispatcher.Invoke(() =>
        {
            CdgSyncText.Text = $"{value:+0.00;-0.00;0.00}s";
            if (_activeSingerSong is not null) _activeSingerSong.CdgSyncSeconds = value;
            MarkLiveShowStateDirty();
            RenderCdgAtCurrentPosition(force: true);
        });

        _karaokeVisualTimer.Tick += (_, _) => RunLiveTimerSafe("KARAOKE VISUAL TIMER", KaraokeVisualTimer_Tick);
        _musicAutomationTimer.Tick += (_, _) => RunLiveTimerSafe("MUSIC AUTOMATION TIMER", MusicAutomationTimer_Tick);
        _musicQueueSaveTimer.Tick += (_, _) =>
        {
            _musicQueueSaveTimer.Stop();
            if (_musicQueueStateDirty) RunLiveTimerSafe("MUSIC QUEUE SAVE", SaveMusicDeckQueuesNow);
        };
        ((INotifyCollectionChanged)DeckAPlaylist.Items).CollectionChanged += (_, _) => MarkMusicDeckQueuesDirty();
        ((INotifyCollectionChanged)DeckBPlaylist.Items).CollectionChanged += (_, _) => MarkMusicDeckQueuesDirty();
        _queue.CollectionChanged += Queue_CollectionChanged;
        _liveShowStateSaveTimer.Tick += (_, _) =>
        {
            _liveShowStateSaveTimer.Stop();
            if (_liveShowStateDirty) RunLiveTimerSafe("SHOW RECOVERY SAVE", () => SaveLiveShowStateNow(cleanShutdown: false));
        };
        _recoveryCheckpointTimer.Tick += (_, _) => RunLiveTimerSafe("SHOW CHECKPOINT", () =>
        {
            SaveMusicDeckQueuesNow();
            SaveLiveShowStateNow(cleanShutdown: false);
        });

        DeckAMedia.MediaEnded += (_, _) => HandleMusicDeckEnded(MusicDeckId.Deck1);
        DeckBMedia.MediaEnded += (_, _) => HandleMusicDeckEnded(MusicDeckId.Deck2);
        DeckAMedia.MediaOpened += (_, _) => UpdateOpenedMusicMetadata(MusicDeckId.Deck1);
        DeckBMedia.MediaOpened += (_, _) => UpdateOpenedMusicMetadata(MusicDeckId.Deck2);
        DeckAMedia.MediaFailed += (_, e) => HandleMusicDeckFailed(MusicDeckId.Deck1, e.ErrorException);
        DeckBMedia.MediaFailed += (_, e) => HandleMusicDeckFailed(MusicDeckId.Deck2, e.ErrorException);
        QuickMusicMedia.MediaEnded += (_, _) => FinishQuickSearchMusic("Quick music finished • waiting for NEXT KARAOKE SONG or PLAY MUSIC");
        QuickMusicMedia.MediaFailed += (_, e) =>
        {
            BrokenMediaRegistry.Mark(_quickSearchSong?.FilePath, e.ErrorException?.Message ?? "Music playback failed");
            if (_quickSearchMusicFadeInActive)
            {
                SetDeckFadeFactor(MusicDeckId.Deck1, _quickSearchFadeStartDeck1);
                SetDeckFadeFactor(MusicDeckId.Deck2, _quickSearchFadeStartDeck2);
            }
            StopQuickSearchMusic(includeRegularDecks: false, updateStatus: false);
            MessageBox.Show(e.ErrorException?.Message ?? "The selected music file could not be played with the installed Windows codecs.",
                "Quick Music Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateMusicAutomationStatus("Quick music stopped • waiting for NEXT KARAOKE SONG or PLAY MUSIC");
        };

        Closing += MainWindow_Closing;

        KaraokeMedia.MediaEnded += async (_, _) => await RunLiveOperationSafeAsync("KARAOKE COMPLETION", KaraokeCompletedAsync);
        KaraokeMedia.MediaFailed += (_, e) =>
        {
            BrokenMediaRegistry.Mark(_karaokePackage?.SourcePath, e.ErrorException?.Message ?? "Karaoke playback failed");
            _pitchAudio.Stop();
            _karaokePlaying = false;
            _karaokePaused = false;
            if (KaraokePauseButton is not null) KaraokePauseButton.Content = "Ⅱ PAUSE";
            _karaokePresentationActive = false;
            _audience?.SetKaraokeActive(false);
            _audience?.ClearKaraokeVisual();
            ClearStartedSingerSongReference();
            MessageBox.Show(e.ErrorException?.Message ?? "The media file could not be played with the installed Windows codecs.",
                "Karaoke Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateAudienceNext();
            ResumeMusicAfterKaraoke();
        };

        Loaded += async (_, _) =>
        {
            RestoreMainLayout();
            RestoreMusicDeckQueues();
            ClampFixedRowsToViewport();
            _musicAutomationTimer.Start();
            _recoveryCheckpointTimer.Start();
            UpdateMusicAutomationStatus("Music ready");
            RefreshDisplayTargets();
            UpdatePlayerTimeDisplays(force: true);
            try
            {
                await _library.InitializeAsync(_lifetime.Token);
                var databaseBytes = _db.GetStorageSizeBytes();
                if (databaseBytes >= 1024L * 1024 * 1024)
                    App.WriteDiagnostic("DATABASE SIZE", $"Hazz database storage is {databaseBytes / (1024d * 1024 * 1024):0.00} GB. Back up and review repeated imports if growth is unexpected.");
                TryRestoreLiveShowState();
                await RefreshLibraryCountsAsync();
                await RefreshSavedSingerNamesAsync();
                await RefreshLibraryAutoWatchAsync();
                SearchStatus.Text = "Database ready • library auto-watch ON";
            }
            catch (Exception ex)
            {
                SearchStatus.Text = "Database error: " + ex.Message;
            }
        };

        Closed += (_, _) =>
        {
            SaveMusicDeckQueuesNow();
            SaveLiveShowStateNow(cleanShutdown: true);
            SaveMainLayout();
            _libraryAutoWatch?.Dispose();
            _libraryAutoWatch = null;
            _lifetime.Cancel();
            _searchCts?.Cancel();
            _importCts?.Cancel();
            _silenceScanCts?.Cancel();
            _karaokeStopFadeCts?.Cancel();
            _karaokeVisualTimer.Stop();
            _musicAutomationTimer.Stop();
            _musicQueueSaveTimer.Stop();
            _liveShowStateSaveTimer.Stop();
            _recoveryCheckpointTimer.Stop();
            DeckAMedia.Stop();
            DeckBMedia.Stop();
            QuickMusicMedia.Stop();
            KaraokeMedia.Stop();
            DeckAMedia.Source = null;
            DeckBMedia.Source = null;
            QuickMusicMedia.Source = null;
            KaraokeMedia.Source = null;
            DeckAMedia.Close();
            DeckBMedia.Close();
            QuickMusicMedia.Close();
            KaraokeMedia.Close();
            _pitchAudio.Dispose();
            _karaokePackage?.Dispose();
            _audience?.Close();
            _musicArchiveWindow?.Close();
            _libraryBrowserWindow?.Close();
        };
    }

    private void RunLiveTimerSafe(string category, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var now = DateTime.UtcNow;
            if (now - _lastLiveTimerErrorUtc >= TimeSpan.FromMinutes(1))
            {
                _lastLiveTimerErrorUtc = now;
                App.WriteDiagnostic(category, ex.ToString());
            }
        }
    }

    private async Task RunLiveOperationSafeAsync(string category, Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            App.WriteDiagnostic(category, ex.ToString());
            SearchStatus.Text = "Recovered from an operation error; playback remains available.";
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        var result = MessageBox.Show(
            "Close Hazz Karaoke Hoster?\n\nAny karaoke or music playback will stop.",
            "Confirm Hazz Shutdown",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) e.Cancel = true;
    }

    private async Task RefreshLibraryAutoWatchAsync()
    {
        var roots = await _libraryRoots.GetRootsAsync(_lifetime.Token);
        _libraryAutoWatch ??= new LibraryAutoWatchService(_libraryImporter, _lifetime.Token);
        _libraryAutoWatch.TrackIndexed -= LibraryAutoWatch_TrackIndexed;
        _libraryAutoWatch.Warning -= LibraryAutoWatch_Warning;
        _libraryAutoWatch.TrackIndexed += LibraryAutoWatch_TrackIndexed;
        _libraryAutoWatch.Warning += LibraryAutoWatch_Warning;
        _libraryAutoWatch.Start(roots);
    }

    private void LibraryAutoWatch_TrackIndexed(object? sender, string path)
    {
        _ = Dispatcher.InvokeAsync(async () =>
        {
            SearchStatus.Text = $"Auto-added: {Path.GetFileNameWithoutExtension(path)}";
            try { await RefreshLibraryCountsAsync(); } catch { }
        });
    }

    private void LibraryAutoWatch_Warning(object? sender, string message)
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            SearchStatus.Text = "Library watcher warning";
            LibraryCountText.ToolTip = message;
        });
    }

    private async Task RefreshLibraryCountsAsync()
    {
        var counts = await _library.GetLibraryCountsAsync(_lifetime.Token);
        LibraryCountText.Text = $"Karaoke {counts.Karaoke:N0} • Music {counts.Music:N0}";
    }

    private CancellationTokenSource? _singerPickerCts;
    private bool _updatingSingerPicker;
    private bool _singerPickerReady;

    private Task RefreshSavedSingerNamesAsync()
    {
        _singerPickerReady = true;
        // Startup/import/add does not enumerate singers. Refresh only an open picker.
        return SingerNameBox.IsDropDownOpen ? SearchSingerPickerAsync(false) : Task.CompletedTask;
    }

    private async void SingerPicker_Opened(object sender, EventArgs e)
    {
        if (_singerPickerReady) await SearchSingerPickerAsync(false);
    }

    private void SingerPicker_Closed(object sender, EventArgs e) => _singerPickerCts?.Cancel();

    private async void SingerPicker_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingSingerPicker || !_singerPickerReady || !SingerNameBox.IsDropDownOpen) return;
        // ComboBox selection changes also update Text; do not filter during mouse/keyboard selection.
        if (SingerNameBox.SelectedItem is string selected && selected == SingerNameBox.Text) return;
        await SearchSingerPickerAsync(true);
    }

    private async Task SearchSingerPickerAsync(bool debounce)
    {
        _singerPickerCts?.Cancel();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _singerPickerCts = cts;
        var query = SingerNameBox.Text;
        try
        {
            if (debounce) await Task.Delay(180, cts.Token);
            var saved = await _singers.SearchSingersAsync(query, 100, cts.Token);
            if (cts.IsCancellationRequested || !SingerNameBox.IsDropDownOpen || SingerNameBox.Text != query) return;
            var editor = SingerNameBox.Template.FindName("PART_EditableTextBox", SingerNameBox) as TextBox;
            var caret = editor?.SelectionStart ?? query.Length;
            var selectionLength = editor?.SelectionLength ?? 0;
            _updatingSingerPicker = true;
            try
            {
                SingerNameBox.ItemsSource = saved.Select(x => x.DisplayName).ToList();
                SingerNameBox.Text = query;
                editor?.Select(Math.Min(caret, query.Length), Math.Min(selectionLength, query.Length - Math.Min(caret, query.Length)));
                SingerNameBox.ToolTip = saved.Count == 0 ? "No matching singers. You can still add a new name."
                    : "Up to 100 singers, most recently used first. Type here to search all saved singers.";
            }
            finally { _updatingSingerPicker = false; }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
                SingerNameBox.ToolTip = "Singer lookup failed. Close and reopen to retry. " + ex.Message;
        }
        finally
        {
            if (ReferenceEquals(_singerPickerCts, cts)) _singerPickerCts = null;
        }
    }

    private void RestoreMainLayout()
    {
        var settings = UiLayoutSettingsStore.Load();
        // Restore against the host monitor work area, not the whole virtual desktop.
        // This prevents a large saved host window from being restored partly off a laptop
        // simply because an audience TV extends the Windows virtual desktop.
        var work = SystemParameters.WorkArea;
        Width = Math.Clamp(settings.WindowWidth, MinWidth, Math.Max(MinWidth, work.Width));
        Height = Math.Clamp(settings.WindowHeight, MinHeight, Math.Max(MinHeight, work.Height));
        Dispatcher.BeginInvoke(() => WindowState = WindowState.Maximized, DispatcherPriority.Loaded);
        MainLeftColumn.Width = new GridLength(0.93, GridUnitType.Star);
        MainCenterColumn.Width = new GridLength(1.24, GridUnitType.Star);
        MainRightColumn.Width = new GridLength(0.93, GridUnitType.Star);
        _lastPreviewHeight = 120;
        SetPreviewVisible(settings.PreviewVisible);
        SetKaraokeOnlyMode(settings.KaraokeOnlyMode, stopMusic: false, saveImmediately: false);
        _deckBPlayerHeightBeforeSideList = Math.Max(145, settings.DeckBPlayerHeight);
        SetSingleDeckMode(settings.SingleDeckMode, stopDeck2: false, saveImmediately: false);
        ClampFixedRowsToViewport();
        BackgroundGifSpeedSlider.Value = double.IsFinite(settings.AudienceBackgroundGifSpeed) ? Math.Clamp(settings.AudienceBackgroundGifSpeed, 0.25, 4) : 1;
        SelectComboItemByContent(BackgroundStretchCombo, DisplayBackgroundStretch(settings.AudienceBackgroundStretchMode), "Fit");
        _audienceBackgroundFolderPath = settings.AudienceBackgroundFolderPath ?? string.Empty;
        _audienceBackgroundImagePath = settings.AudienceBackgroundImagePath ?? string.Empty;
        _audienceLogoImagePath = settings.AudienceLogoImagePath ?? string.Empty;
        AudienceBackgroundEnabledCheck.IsChecked = settings.AudienceBackgroundEnabled && (!string.IsNullOrWhiteSpace(_audienceBackgroundImagePath) || !string.IsNullOrWhiteSpace(_audienceBackgroundFolderPath));
        MusicVideoShowLogoCheck.IsChecked = settings.MusicVideoShowLogo;
        MusicVideoShowScrollerCheck.IsChecked = settings.MusicVideoShowScroller;
        MusicVideoShowSingersCheck.IsChecked = settings.MusicVideoShowSingers;
        MusicVideoShowKamikazeCheck.IsChecked = settings.MusicVideoShowKamikaze;
        AudienceLogoEnabledCheck.IsChecked = settings.AudienceLogoEnabled && !string.IsNullOrWhiteSpace(_audienceLogoImagePath);
        SelectComboItemByContent(LogoPositionCombo, settings.AudienceLogoPosition, "TopRight");
        LogoWidthSlider.Value = Math.Clamp(settings.AudienceLogoWidth, 60, 800);
        _audienceNextHeadingColor = NormalizeColor(settings.AudienceNextHeadingColor, "#FFFFD34D");
        _audienceNextPositionColor = NormalizeColor(settings.AudienceNextPositionColor, "#FFFFD34D");
        _audienceNextSingerColor = NormalizeColor(settings.AudienceNextSingerColor, "#FFFFFFFF");
        _audienceNextSongColor = NormalizeColor(settings.AudienceNextSongColor, "#FFD8E2EF");
        _audienceRotationScrollerColor = NormalizeColor(settings.AudienceRotationScrollerColor, "#FFFFFFFF");
        _audienceVenueScrollerColor = NormalizeColor(settings.AudienceVenueScrollerColor, "#FFFFD34D");

        // Restore the complete singer/audience text setup. Earlier builds only saved
        // colours/artwork, so the venue message and font selections reverted.
        ShowNextSingerCheck.IsChecked = settings.AudienceShowNextSinger;
        ShowNextSongCheck.IsChecked = settings.AudienceShowNextSong;
        NextFontCombo.SelectedItem = NextFontCombo.Items.Cast<object>()
            .FirstOrDefault(x => string.Equals(x?.ToString(), settings.AudienceNextSingerFontFamily, StringComparison.OrdinalIgnoreCase))
            ?? NextFontCombo.SelectedItem;
        SelectComboItemByContent(NextSizeCombo, Math.Clamp(settings.AudienceNextSingerFontSize, 32, 72).ToString("0"), "48");
        SelectComboItemByContent(PositionCombo, settings.AudienceNextSingerPosition, "BottomCenter");
        ScrollerCheck.IsChecked = settings.AudienceScrollerEnabled;
        ScrollerTextBox.Text = settings.AudienceScrollerText ?? string.Empty;
        ScrollerFontCombo.SelectedItem = ScrollerFontCombo.Items.Cast<object>()
            .FirstOrDefault(x => string.Equals(x?.ToString(), settings.AudienceScrollerFontFamily, StringComparison.OrdinalIgnoreCase))
            ?? ScrollerFontCombo.SelectedItem;
        SelectComboItemByContent(ScrollerSizeCombo, Math.Clamp(settings.AudienceScrollerFontSize, 22, 42).ToString("0"), "30");
        ScrollerSpeedSlider.Value = Math.Clamp(settings.AudienceScrollerPixelsPerSecond, ScrollerSpeedSlider.Minimum, ScrollerSpeedSlider.Maximum);
        SelectComboItemByContent(ScrollerPositionCombo, settings.AudienceScrollerPosition, "Bottom");

        _audienceKamikazeColor = NormalizeColor(settings.AudienceKamikazeColor, "#FFFFD34D");
        KamikazeTextBox.Text = string.IsNullOrWhiteSpace(settings.AudienceKamikazeText) ? "KAMIKAZE KARAOKE!" : settings.AudienceKamikazeText;
        KamikazeFontCombo.SelectedItem = KamikazeFontCombo.Items.Cast<object>()
            .FirstOrDefault(x => string.Equals(x?.ToString(), settings.AudienceKamikazeFontFamily, StringComparison.OrdinalIgnoreCase))
            ?? KamikazeFontCombo.SelectedItem;
        SelectComboItemByContent(KamikazeSizeCombo, Math.Clamp(settings.AudienceKamikazeFontSize, 48, 128).ToString("0"), "84");
        UpdateAudienceArtworkLabels();
        UpdateAudienceColorButtons();
        ApplyOverlaySettings();
    }

    private void SaveMainLayout()
    {
        var totalWidth = Math.Max(1.0, MainLeftColumn.ActualWidth + MainCenterColumn.ActualWidth + MainRightColumn.ActualWidth);
        UiLayoutSettingsStore.Save(new UiLayoutSettings
        {
            LeftColumnWeight = Math.Max(0.05, MainLeftColumn.ActualWidth / totalWidth),
            CenterColumnWeight = Math.Max(0.05, MainCenterColumn.ActualWidth / totalWidth),
            RightColumnWeight = Math.Max(0.05, MainRightColumn.ActualWidth / totalWidth),
            DeckAPlayerHeight = Math.Max(145, DeckAPlayerRow.ActualHeight),
            DeckBPlayerHeight = Math.Max(145, _singleDeckMode ? _deckBPlayerHeightBeforeSideList : DeckBPlayerRow.ActualHeight),
            KaraokeDeckHeight = Math.Max(185, _karaokeOnlyMode ? _fullModeKaraokeDeckHeight : KaraokeDeckRow.ActualHeight),
            KaraokePreviewHeight = Math.Max(80, KaraokePreviewPanel.Visibility == Visibility.Visible ? KaraokePreviewRow.ActualHeight : _lastPreviewHeight),
            PreviewVisible = KaraokePreviewPanel.Visibility == Visibility.Visible,
            WindowWidth = Math.Max(MinWidth, WindowState == WindowState.Maximized ? RestoreBounds.Width : ActualWidth),
            WindowHeight = Math.Max(MinHeight, WindowState == WindowState.Maximized ? RestoreBounds.Height : ActualHeight),
            WindowMaximized = WindowState == WindowState.Maximized,
            KaraokeOnlyMode = _karaokeOnlyMode,
            SingleDeckMode = _singleDeckMode,
            AudienceBackgroundEnabled = AudienceBackgroundEnabledCheck.IsChecked == true,
            AudienceBackgroundGifSpeed = BackgroundGifSpeedSlider.Value,
            AudienceBackgroundStretchMode = SelectedBackgroundStretch(),
            AudienceBackgroundFolderPath = _audienceBackgroundFolderPath,
            AudienceBackgroundImagePath = _audienceBackgroundImagePath,
            MusicVideoShowLogo = MusicVideoShowLogoCheck.IsChecked == true,
            MusicVideoShowScroller = MusicVideoShowScrollerCheck.IsChecked == true,
            MusicVideoShowSingers = MusicVideoShowSingersCheck.IsChecked == true,
            MusicVideoShowKamikaze = MusicVideoShowKamikazeCheck.IsChecked == true,
            AudienceLogoEnabled = AudienceLogoEnabledCheck.IsChecked == true,
            AudienceLogoImagePath = _audienceLogoImagePath,
            AudienceLogoPosition = (LogoPositionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "TopRight",
            AudienceLogoWidth = LogoWidthSlider.Value,
            AudienceNextHeadingColor = _audienceNextHeadingColor,
            AudienceNextPositionColor = _audienceNextPositionColor,
            AudienceNextSingerColor = _audienceNextSingerColor,
            AudienceNextSongColor = _audienceNextSongColor,
            AudienceRotationScrollerColor = _audienceRotationScrollerColor,
            AudienceVenueScrollerColor = _audienceVenueScrollerColor,
            AudienceShowNextSinger = ShowNextSingerCheck.IsChecked == true,
            AudienceShowNextSong = ShowNextSongCheck.IsChecked == true,
            AudienceNextSingerFontFamily = NextFontCombo.SelectedItem?.ToString() ?? "Segoe UI",
            AudienceNextSingerFontSize = double.TryParse((NextSizeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var nextSize) ? nextSize : 48,
            AudienceNextSingerPosition = (PositionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "BottomCenter",
            AudienceScrollerEnabled = ScrollerCheck.IsChecked == true,
            AudienceScrollerText = ScrollerTextBox.Text,
            AudienceScrollerFontFamily = ScrollerFontCombo.SelectedItem?.ToString() ?? "Segoe UI",
            AudienceScrollerFontSize = double.TryParse((ScrollerSizeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var scrollerSize) ? scrollerSize : 30,
            AudienceScrollerPixelsPerSecond = ScrollerSpeedSlider.Value,
            AudienceScrollerPosition = (ScrollerPositionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bottom",
            AudienceKamikazeText = KamikazeTextBox.Text,
            AudienceKamikazeFontFamily = KamikazeFontCombo.SelectedItem?.ToString() ?? "Segoe UI Black",
            AudienceKamikazeFontSize = double.TryParse((KamikazeSizeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var kamikazeSize) ? kamikazeSize : 84,
            AudienceKamikazeColor = _audienceKamikazeColor
        });
    }

    private void MarkMusicDeckQueuesDirty()
    {
        if (_restoringMusicDeckQueues) return;
        _musicQueueStateDirty = true;
        _musicQueueSaveTimer.Stop();
        _musicQueueSaveTimer.Start();
    }

    private void SaveMusicDeckQueuesNow()
    {
        _musicQueueSaveTimer.Stop();
        var state = new MusicDeckQueueState
        {
            Deck1 = CaptureUnplayedMusicQueue(DeckAPlaylist, _deck1CurrentItem),
            Deck2 = CaptureUnplayedMusicQueue(DeckBPlaylist, _deck2CurrentItem)
        };
        MusicDeckQueueStateStore.Save(state);
        _musicQueueStateDirty = false;
    }

    private static List<MusicDeckQueueStateItem> CaptureUnplayedMusicQueue(ListBox list, MusicQueueItem? current)
    {
        var rows = new List<MusicDeckQueueStateItem>();
        foreach (var item in list.Items.OfType<MusicQueueItem>())
        {
            // A track that has actually started is consumed from the live queue even
            // if Hazz is closed while it is still playing/crossfading.
            if (current is not null && ReferenceEquals(item, current)) continue;
            rows.Add(new MusicDeckQueueStateItem
            {
                SongId = item.SongId,
                FilePath = item.FilePath,
                Artist = item.Artist,
                Title = item.Title,
                DurationSeconds = item.Duration is TimeSpan d && d > TimeSpan.Zero ? d.TotalSeconds : null
            });
        }
        return rows;
    }

    private void RestoreMusicDeckQueues()
    {
        var state = MusicDeckQueueStateStore.Load();
        _restoringMusicDeckQueues = true;
        try
        {
            DeckAPlaylist.Items.Clear();
            DeckBPlaylist.Items.Clear();
            RestoreMusicDeckQueue(DeckAPlaylist, state.Deck1);
            RestoreMusicDeckQueue(DeckBPlaylist, state.Deck2);
            RenumberPlaylist(DeckAPlaylist);
            RenumberPlaylist(DeckBPlaylist);
            RecalculateMusicDeckOrder(MusicDeckId.Deck1);
            RecalculateMusicDeckOrder(MusicDeckId.Deck2);
            if (DeckAPlaylist.Items.Count > 0) DeckAPlaylist.SelectedIndex = 0;
            if (DeckBPlaylist.Items.Count > 0) DeckBPlaylist.SelectedIndex = 0;
        }
        finally
        {
            _restoringMusicDeckQueues = false;
            _musicQueueStateDirty = false;
        }

        if (DeckAPlaylist.Items.Count > 0 || DeckBPlaylist.Items.Count > 0)
            UpdateMusicAutomationStatus($"Restored unplayed music queues • D1 {DeckAPlaylist.Items.Count:N0} • D2 {DeckBPlaylist.Items.Count:N0}");
    }

    private static void RestoreMusicDeckQueue(ListBox list, IEnumerable<MusicDeckQueueStateItem>? rows)
    {
        if (rows is null) return;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.FilePath)) continue;
            list.Items.Add(new MusicQueueItem
            {
                SongId = row.SongId,
                FilePath = row.FilePath,
                Artist = row.Artist ?? string.Empty,
                Title = row.Title ?? string.Empty,
                Duration = row.DurationSeconds is double seconds && seconds > 0
                    ? TimeSpan.FromSeconds(seconds)
                    : null
            });
        }
    }

    private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // DisplaySettingsPopup has StaysOpen=True so nested controls such as ComboBox
        // popups and colour/file dialogs cannot accidentally dismiss it. Any click back
        // on the main Hazz window is an intentional click-away and closes the panel.
        if (!DisplaySettingsPopup.IsOpen) return;

        // Be defensive in case WPF routes an input event from the Popup presentation
        // source back to this Window on a particular Windows/theme combination.
        if (e.OriginalSource is DependencyObject source && DisplaySettingsPopup.Child is DependencyObject popupRoot)
        {
            for (DependencyObject? node = source; node is not null; node = GetVisualOrLogicalParent(node))
                if (ReferenceEquals(node, popupRoot)) return;
        }

        DisplaySettingsPopup.IsOpen = false;
        SaveMainLayout();
    }

    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject node)
    {
        try
        {
            var visualParent = VisualTreeHelper.GetParent(node);
            if (visualParent is not null) return visualParent;
        }
        catch
        {
            // Some content elements are not in a VisualTree; fall back to logical parent.
        }
        return LogicalTreeHelper.GetParent(node);
    }

    private void CloseDisplaySettings_Click(object sender, RoutedEventArgs e)
    {
        DisplaySettingsPopup.IsOpen = false;
        SaveMainLayout();
    }

    private void TogglePreview_Click(object sender, RoutedEventArgs e)
    {
        SetPreviewVisible(KaraokePreviewPanel.Visibility != Visibility.Visible);
    }

    private void SetPreviewVisible(bool visible)
    {
        if (visible)
        {
            KaraokePreviewRow.MinHeight = 80;
            KaraokePreviewSplitterRow.Height = new GridLength(8);
            KaraokePreviewRow.Height = new GridLength(Math.Clamp(_lastPreviewHeight, 80, 650));
            KaraokePreviewSplitter.Visibility = Visibility.Visible;
            KaraokePreviewPanel.Visibility = Visibility.Visible;
            PreviewToggleButton.Content = "HIDE PREVIEW";
        }
        else
        {
            if (KaraokePreviewPanel.Visibility == Visibility.Visible && KaraokePreviewRow.ActualHeight >= 80)
                _lastPreviewHeight = KaraokePreviewRow.ActualHeight;
            KaraokePreviewRow.MinHeight = 0;
            KaraokePreviewRow.Height = new GridLength(0);
            KaraokePreviewSplitterRow.Height = new GridLength(0);
            KaraokePreviewSplitter.Visibility = Visibility.Collapsed;
            KaraokePreviewPanel.Visibility = Visibility.Collapsed;
            PreviewToggleButton.Content = "SHOW PREVIEW";
        }
    }

    private void KaraokeOnlyMode_Click(object sender, RoutedEventArgs e)
    {
        SetKaraokeOnlyMode(KaraokeOnlyModeMenuItem.IsChecked, stopMusic: true, saveImmediately: true);
    }

    private void SingleDeckMode_Click(object sender, RoutedEventArgs e)
    {
        SetSingleDeckMode(SingleDeckModeMenuItem.IsChecked, stopDeck2: true, saveImmediately: true);
    }

    private void SetSingleDeckMode(bool enabled, bool stopDeck2, bool saveImmediately)
    {
        _singleDeckMode = enabled;
        if (SingleDeckModeMenuItem is not null) SingleDeckModeMenuItem.IsChecked = enabled;

        if (enabled)
        {
            if (DeckBPlayerRow.ActualHeight >= 145) _deckBPlayerHeightBeforeSideList = DeckBPlayerRow.ActualHeight;
            if (stopDeck2) StopDeckTwoForSideListMode();
            CancelMusicTransitions();
            _autoCrossfadeBeforeSingleDeck = AutoCrossfadeCheck.IsChecked == true;
            AutoCrossfadeCheck.IsChecked = false;
            AutoCrossfadeCheck.IsEnabled = false;
            CrossfadeSecondsSlider.IsEnabled = false;
            FadeNowButton.IsEnabled = false;
            DeckBPlayerControlsPanel.Visibility = Visibility.Collapsed;
            DeckBSideListControlsPanel.Visibility = Visibility.Visible;
            DeckBPlaylistFooter.Visibility = Visibility.Collapsed;
            DeckBPlayerRow.MinHeight = 0;
            DeckBPlayerRow.Height = GridLength.Auto;
            DeckBSplitterRow.Height = new GridLength(0);
            DeckBSplitter.Visibility = Visibility.Collapsed;
            DeckBPlaylistHeading.Text = "MUSIC SIDE LIST";
            DeckBSideAddFilesButton.Visibility = Visibility.Collapsed;
            SearchAddDeck2Button.Content = "ADD TO SIDE LIST";
            DeckBPlaylist.ToolTip = "Holding list: select and drag tracks into Deck 1. Delete or Remove Selected removes them from this list.";
        }
        else
        {
            DeckBPlayerControlsPanel.Visibility = Visibility.Visible;
            DeckBSideListControlsPanel.Visibility = Visibility.Collapsed;
            DeckBPlaylistFooter.Visibility = Visibility.Visible;
            DeckBPlayerRow.MinHeight = 145;
            DeckBPlayerRow.Height = new GridLength(Math.Max(145, _deckBPlayerHeightBeforeSideList));
            DeckBSplitterRow.Height = new GridLength(8);
            DeckBSplitter.Visibility = Visibility.Visible;
            DeckBPlaylistHeading.Text = "DECK 2 PLAYLIST";
            DeckBSideAddFilesButton.Visibility = Visibility.Collapsed;
            SearchAddDeck2Button.Content = "ADD TO DECK 2";
            DeckBPlaylist.ToolTip = "Select one or more tracks. Press Delete or use REMOVE SELECTED to take them out of this deck queue.";
            AutoCrossfadeCheck.IsEnabled = true;
            CrossfadeSecondsSlider.IsEnabled = true;
            FadeNowButton.IsEnabled = true;
            AutoCrossfadeCheck.IsChecked = _autoCrossfadeBeforeSingleDeck;
        }

        if (!_karaokeOnlyMode)
        {
            HostModeText.Text = enabled ? "KARAOKE + SINGLE MUSIC DECK" : "KARAOKE + MUSIC MODE";
            SearchStatus.Text = enabled ? "Single Deck mode • Deck 2 is a side list" : "Karaoke + Music Mode";
        }
        UpdateVisualCrossfader();
        ClampFixedRowsToViewport();
        if (saveImmediately && IsLoaded) SaveMainLayout();
    }

    private void StopDeckTwoForSideListMode()
    {
        StopDeck(MusicDeckId.Deck2);
        SetDeckFadeFactor(MusicDeckId.Deck2, 0.0);
        _deck2CurrentItem = null;
        _deck2CurrentIndex = -1;
        if (_activeMusicDeck == MusicDeckId.Deck2) _activeMusicDeck = MusicDeckId.None;
        if (_resumeMusicDeck == MusicDeckId.Deck2)
        {
            _resumeMusicDeck = MusicDeckId.Deck1;
            _resumeMusicIndex = GetScheduledIndex(MusicDeckId.Deck1);
            _resumeMusicItem = _resumeMusicIndex >= 0
                ? PlaylistFor(MusicDeckId.Deck1).Items[_resumeMusicIndex] as MusicQueueItem
                : null;
        }
        RecalculateMusicDeckOrder(MusicDeckId.Deck2);
    }

    private void SetKaraokeOnlyMode(bool enabled, bool stopMusic, bool saveImmediately)
    {
        _karaokeOnlyMode = enabled;
        if (KaraokeOnlyModeMenuItem is not null) KaraokeOnlyModeMenuItem.IsChecked = enabled;

        if (enabled)
        {
            if (stopMusic) StopMusicForKaraokeOnlyMode();

            // Keep the music code/data available so the mode is reversible, but remove
            // every music-only control from the live host workspace. The karaoke centre
            // grid spans the whole host area, reclaiming both deck columns.
            MusicDeckAPanel.Visibility = Visibility.Collapsed;
            MusicDeckBPanel.Visibility = Visibility.Collapsed;
            MainLeftSplitter.Visibility = Visibility.Collapsed;
            MainRightSplitter.Visibility = Visibility.Collapsed;
            MusicAutomationPanel.Visibility = Visibility.Collapsed;
            SearchMusicButton.Visibility = Visibility.Collapsed;
            SearchMusicVideoButton.Visibility = Visibility.Collapsed;
            Grid.SetColumn(KaraokeWorkspace, 0);
            Grid.SetColumnSpan(KaraokeWorkspace, 5);

            if (_searchMediaKind is "Music" or "MusicVideo")
            {
                _searchMediaKind = "Karaoke";
                UpdateSearchModeUi();
                _ = RunSearchAsync();
            }

            if (KaraokeDeckRow.ActualHeight >= 185)
                _fullModeKaraokeDeckHeight = KaraokeDeckRow.ActualHeight;
            KaraokeDeckRow.Height = new GridLength(220);
            HostModeText.Text = "KARAOKE ONLY MODE";
            HostModeText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD3, 0x4D));
            SearchStatus.Text = "Karaoke Only Mode • music decks hidden";
        }
        else
        {
            MusicDeckAPanel.Visibility = Visibility.Visible;
            MusicDeckBPanel.Visibility = Visibility.Visible;
            MainLeftSplitter.Visibility = Visibility.Visible;
            MainRightSplitter.Visibility = Visibility.Visible;
            MusicAutomationPanel.Visibility = Visibility.Visible;
            SearchMusicButton.Visibility = Visibility.Visible;
            SearchMusicVideoButton.Visibility = Visibility.Visible;
            Grid.SetColumn(KaraokeWorkspace, 2);
            Grid.SetColumnSpan(KaraokeWorkspace, 1);
            KaraokeDeckRow.Height = new GridLength(Math.Clamp(_fullModeKaraokeDeckHeight, 185, 700));
            HostModeText.Text = _singleDeckMode ? "KARAOKE + SINGLE MUSIC DECK" : "KARAOKE + MUSIC MODE";
            HostModeText.Foreground = new SolidColorBrush(Color.FromRgb(0x6E, 0xC1, 0xE4));
            SearchStatus.Text = _singleDeckMode ? "Single Deck mode • Deck 2 is a side list" : "Karaoke + Music Mode";
        }

        ClampFixedRowsToViewport();
        if (saveImmediately && IsLoaded) SaveMainLayout();
    }

    private void StopMusicForKaraokeOnlyMode()
    {
        CancelMusicTransitions();
        DeckAMedia.Stop();
        DeckBMedia.Stop();
        if (_deck1CurrentItem is not null) _deck1CurrentItem.IsNowPlaying = false;
        if (_deck2CurrentItem is not null) _deck2CurrentItem.IsNowPlaying = false;
        _deck1CurrentItem = null;
        _deck2CurrentItem = null;
        _deck1CurrentIndex = -1;
        _deck2CurrentIndex = -1;
        _activeMusicDeck = MusicDeckId.None;
        _resumeMusicDeck = MusicDeckId.None;
        _resumeMusicIndex = -1;
        _resumeMusicItem = null;
        _musicSuspendedForKaraoke = false;
        _deck1Paused = false;
        _deck2Paused = false;
        SetDeckFadeFactor(MusicDeckId.Deck1, 0.0);
        SetDeckFadeFactor(MusicDeckId.Deck2, 0.0);
        RecalculateMusicDeckOrder(MusicDeckId.Deck1);
        RecalculateMusicDeckOrder(MusicDeckId.Deck2);
        UpdatePlayerTimeDisplays(force: true);
    }

    private void ScheduleViewportLayoutClamp()
    {
        if (!IsLoaded || _layoutViewportUpdatePending) return;
        _layoutViewportUpdatePending = true;
        Dispatcher.BeginInvoke(() =>
        {
            _layoutViewportUpdatePending = false;
            ClampFixedRowsToViewport();
        }, DispatcherPriority.Background);
    }

    private void ClampFixedRowsToViewport()
    {
        if (HostViewport is null || HostLayout is null) return;
        var width = HostViewport.ActualWidth;
        var height = HostViewport.ActualHeight;
        if (width <= 0 || height <= 0) return;
        var scale = Math.Min(1, Math.Min(width / 1696, height / 1116));
        HostLayout.Width = Math.Max(1680, width / scale - 16);
        HostLayout.Height = Math.Max(1100, height / scale - 16);
        // Auto rows measure the complete controls; the Viewbox fits the whole console.
        // Old saved splitter sizes must not clip transport, key or sync controls at startup.
        DeckAPlayerRow.Height = GridLength.Auto;
        DeckBPlayerRow.Height = GridLength.Auto;
        KaraokeDeckRow.Height = GridLength.Auto;
        if (KaraokePreviewPanel.Visibility == Visibility.Visible)
            KaraokePreviewRow.Height = new GridLength(120);
        SettingsAutoFit.MaxWidth = Math.Max(100, width - 24);
        SettingsAutoFit.MaxHeight = Math.Max(100, height - 40);
    }

    private void HostViewport_SizeChanged(object sender, SizeChangedEventArgs e) => ClampFixedRowsToViewport();

    private void DisplaySettingsMenu_Click(object sender, RoutedEventArgs e)
    {
        DisplaySettingsPopup.IsOpen = true;
    }

    private void ChooseAudienceBackgroundFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose slideshow folder (1 minute per image or muted video)" };
        if (dlg.ShowDialog(this) != true) return;
        _audienceBackgroundFolderPath = dlg.FolderName;
        _audienceBackgroundImagePath = string.Empty;
        AudienceBackgroundEnabledCheck.IsChecked = true;
        UpdateAudienceArtworkLabels();
        ApplyOverlaySettings();
        SaveMainLayout();
    }

    private void ChooseAudienceBackground_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose singer-view background image or muted looping video",
            Filter = "Images and videos|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.mp4;*.m4v;*.wmv;*.avi;*.mov;*.mkv;*.webm;*.mpg;*.mpeg|All files|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) != true) return;
        _audienceBackgroundFolderPath = string.Empty;
        _audienceBackgroundImagePath = dlg.FileName;
        AudienceBackgroundEnabledCheck.IsChecked = true;
        UpdateAudienceArtworkLabels();
        ApplyOverlaySettings();
    }

    private void ClearAudienceBackground_Click(object sender, RoutedEventArgs e)
    {
        _audienceBackgroundFolderPath = string.Empty;
        _audienceBackgroundImagePath = string.Empty;
        AudienceBackgroundEnabledCheck.IsChecked = false;
        UpdateAudienceArtworkLabels();
        ApplyOverlaySettings();
    }

    private void ChooseAudienceLogo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose audience logo",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) != true) return;
        _audienceLogoImagePath = dlg.FileName;
        AudienceLogoEnabledCheck.IsChecked = true;
        UpdateAudienceArtworkLabels();
        ApplyOverlaySettings();
    }

    private void ClearAudienceLogo_Click(object sender, RoutedEventArgs e)
    {
        _audienceLogoImagePath = string.Empty;
        AudienceLogoEnabledCheck.IsChecked = false;
        UpdateAudienceArtworkLabels();
        ApplyOverlaySettings();
    }

    private void UpdateAudienceArtworkLabels()
    {
        if (AudienceBackgroundFileText is not null)
            AudienceBackgroundFileText.Text = !string.IsNullOrWhiteSpace(_audienceBackgroundFolderPath)
                ? $"Slideshow (1 minute): {_audienceBackgroundFolderPath}"
                : string.IsNullOrWhiteSpace(_audienceBackgroundImagePath)
                ? "No background selected"
                : Path.GetFileName(_audienceBackgroundImagePath);
        if (AudienceLogoFileText is not null)
            AudienceLogoFileText.Text = string.IsNullOrWhiteSpace(_audienceLogoImagePath)
                ? "No logo selected"
                : Path.GetFileName(_audienceLogoImagePath);
        if (LogoWidthText is not null && LogoWidthSlider is not null)
            LogoWidthText.Text = $"{LogoWidthSlider.Value:0}px";
    }

    private void NextHeadingColor_Click(object sender, RoutedEventArgs e)
        => ChooseAudienceColor(ref _audienceNextHeadingColor, NextHeadingColorButton);

    private void NextPositionColor_Click(object sender, RoutedEventArgs e)
        => ChooseAudienceColor(ref _audienceNextPositionColor, NextPositionColorButton);

    private void NextSingerColor_Click(object sender, RoutedEventArgs e)
        => ChooseAudienceColor(ref _audienceNextSingerColor, NextSingerColorButton);

    private void NextSongColor_Click(object sender, RoutedEventArgs e)
        => ChooseAudienceColor(ref _audienceNextSongColor, NextSongColorButton);

    private void RotationScrollerColor_Click(object sender, RoutedEventArgs e)
        => ChooseAudienceColor(ref _audienceRotationScrollerColor, RotationScrollerColorButton);

    private void VenueScrollerColor_Click(object sender, RoutedEventArgs e)
        => ChooseAudienceColor(ref _audienceVenueScrollerColor, VenueScrollerColorButton);

    private void KamikazeColor_Click(object sender, RoutedEventArgs e)
        => ChooseAudienceColor(ref _audienceKamikazeColor, KamikazeColorButton);

    private void ChooseAudienceColor(ref string target, Button button)
    {
        var initial = ParseWpfColor(target, Colors.White);
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = System.Drawing.Color.FromArgb(initial.A, initial.R, initial.G, initial.B)
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        var c = dialog.Color;
        target = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
        UpdateAudienceColorButton(button, target);
        ApplyOverlaySettings();
        UpdateAudienceNext();
    }

    private void UpdateAudienceColorButtons()
    {
        UpdateAudienceColorButton(NextHeadingColorButton, _audienceNextHeadingColor);
        UpdateAudienceColorButton(NextPositionColorButton, _audienceNextPositionColor);
        UpdateAudienceColorButton(NextSingerColorButton, _audienceNextSingerColor);
        UpdateAudienceColorButton(NextSongColorButton, _audienceNextSongColor);
        UpdateAudienceColorButton(RotationScrollerColorButton, _audienceRotationScrollerColor);
        UpdateAudienceColorButton(VenueScrollerColorButton, _audienceVenueScrollerColor);
        UpdateAudienceColorButton(KamikazeColorButton, _audienceKamikazeColor);
    }

    private static void UpdateAudienceColorButton(Button? button, string colour)
    {
        if (button is null) return;
        var c = ParseWpfColor(colour, Colors.White);
        button.Background = new SolidColorBrush(c);
        var luminance = (299 * c.R + 587 * c.G + 114 * c.B) / 1000;
        button.Foreground = luminance >= 145 ? Brushes.Black : Brushes.White;
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(value) && ColorConverter.ConvertFromString(value) is Color)
                return value;
        }
        catch { }
        return fallback;
    }

    private static Color ParseWpfColor(string? value, Color fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(value) && ColorConverter.ConvertFromString(value) is Color c)
                return c;
        }
        catch { }
        return fallback;
    }

    private static void SelectComboItemByContent(ComboBox combo, string? value, string fallback)
    {
        var wanted = string.IsNullOrWhiteSpace(value) ? fallback : value;
        var match = combo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Content?.ToString(), wanted, StringComparison.OrdinalIgnoreCase));
        combo.SelectedItem = match ?? combo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Content?.ToString(), fallback, StringComparison.OrdinalIgnoreCase));
    }

    private void LogoWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        UpdateAudienceArtworkLabels();
        ApplyOverlaySettings();
    }

    private void DisplayTargetsMenu_Opened(object sender, RoutedEventArgs e)
    {
        RefreshDisplayTargets();
        DisplayTargetsMenuItem.Items.Clear();
        if (_displayTargets.Count == 0)
        {
            DisplayTargetsMenuItem.Items.Add(new MenuItem { Header = "No displays detected", IsEnabled = false });
            return;
        }

        foreach (var target in _displayTargets)
        {
            var item = new MenuItem
            {
                Header = target.Label,
                Tag = target.Index,
                IsCheckable = false
            };
            item.Click += DisplayTargetMenu_Click;
            DisplayTargetsMenuItem.Items.Add(item);
        }
    }

    private void DisplayTargetMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not int index) return;
        var target = _displayTargets.FirstOrDefault(x => x.Index == index);
        if (target is null) return;
        DisplayCombo.SelectedItem = target;
        EnsureAudienceWindow().SendToDisplay(target, fullScreen: true);
    }

    private void UpdatePlayerTimeDisplays(bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && (now - _lastTimelineUiUtc).TotalMilliseconds < 180) return;
        _lastTimelineUiUtc = now;
        UpdateMediaTime(DeckAMedia, DeckATimeText, DeckAProgress, _deck1SeekDragging);
        UpdateMediaTime(DeckBMedia, DeckBTimeText, DeckBProgress, _deck2SeekDragging);

        var karaokeTotal = MediaDuration(KaraokeMedia);
        if (karaokeTotal <= TimeSpan.Zero && _pitchAudio.TotalTime > TimeSpan.Zero) karaokeTotal = _pitchAudio.TotalTime;
        UpdateTimeText(KaraokeMedia.Position, karaokeTotal, KaraokeTimeText, KaraokeProgress);
    }

    private static void UpdateMediaTime(MediaElement media, TextBlock text, Slider seek, bool dragging)
    {
        var position = media.Position;
        var total = MediaDuration(media);
        UpdateTimeTextOnly(position, total, text);
        if (dragging) return;
        seek.Maximum = Math.Max(1.0, total.TotalSeconds);
        seek.Value = total > TimeSpan.Zero ? Math.Clamp(position.TotalSeconds, 0.0, total.TotalSeconds) : 0.0;
        seek.IsEnabled = total > TimeSpan.Zero;
    }

    private static TimeSpan MediaDuration(MediaElement media)
        => media.NaturalDuration.HasTimeSpan ? media.NaturalDuration.TimeSpan : TimeSpan.Zero;

    private static void UpdateTimeText(TimeSpan position, TimeSpan total, TextBlock text, ProgressBar progress)
    {
        UpdateTimeTextOnly(position, total, text);
        progress.Value = total > TimeSpan.Zero ? Math.Clamp(position.TotalSeconds / total.TotalSeconds, 0.0, 1.0) : 0.0;
    }

    private static void UpdateTimeTextOnly(TimeSpan position, TimeSpan total, TextBlock text)
    {
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (total > TimeSpan.Zero && position > total) position = total;
        var remaining = total > TimeSpan.Zero ? total - position : TimeSpan.Zero;
        text.Text = total > TimeSpan.Zero
            ? $"ELAPSED {FormatPlayerTime(position)}  •  REMAINING {FormatPlayerTime(remaining)}  •  TOTAL {FormatPlayerTime(total)}"
            : $"ELAPSED {FormatPlayerTime(position)}  •  REMAINING --:--  •  TOTAL --:--";
    }

    private void MusicSeek_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(sender, DeckAProgress)) _deck1SeekDragging = true;
        else if (ReferenceEquals(sender, DeckBProgress)) _deck2SeekDragging = true;
    }

    private void MusicSeek_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider) return;
        var deck = ReferenceEquals(slider, DeckAProgress) ? MusicDeckId.Deck1 : MusicDeckId.Deck2;
        if (deck == MusicDeckId.Deck1) _deck1SeekDragging = false;
        else _deck2SeekDragging = false;
        SeekMusicDeck(deck, slider.Value);
    }

    private void MusicSeek_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider slider) return;
        var isDeck1 = ReferenceEquals(slider, DeckAProgress);
        if (!(isDeck1 ? _deck1SeekDragging : _deck2SeekDragging)) return;
        var media = isDeck1 ? DeckAMedia : DeckBMedia;
        var text = isDeck1 ? DeckATimeText : DeckBTimeText;
        UpdateTimeTextOnly(TimeSpan.FromSeconds(slider.Value), MediaDuration(media), text);
    }

    private void MusicSeek_KeyUp(object sender, KeyEventArgs e)
    {
        if (sender is not Slider slider || e.Key is not (Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown)) return;
        SeekMusicDeck(ReferenceEquals(slider, DeckAProgress) ? MusicDeckId.Deck1 : MusicDeckId.Deck2, slider.Value);
    }

    private void SeekMusicDeck(MusicDeckId deck, double seconds)
    {
        var media = MediaFor(deck);
        var total = MediaDuration(media);
        if (CurrentMusicItemFor(deck) is null || total <= TimeSpan.Zero)
        {
            UpdateMusicAutomationStatus($"Load and play a track on {DeckName(deck)} before seeking");
            return;
        }

        var target = TimeSpan.FromSeconds(Math.Clamp(seconds, 0.0, total.TotalSeconds));
        media.Position = target;
        if (IsDeckPaused(deck)) SetPausedPosition(deck, target);
        if (_audienceMusicVideoDeck == deck && CurrentMusicItemFor(deck) is { } videoItem)
            _audience?.ShowMusicVideo(videoItem.FilePath, target, !IsDeckPaused(deck));
        UpdatePlayerTimeDisplays(force: true);
        UpdateMusicAutomationStatus($"{DeckName(deck)} positioned at {target:hh\\:mm\\:ss\\.fff}");
    }

    private static string FormatPlayerTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}";
    }

    private void SearchKaraokeMode_Click(object sender, RoutedEventArgs e)
    {
        _searchMediaKind = "Karaoke";
        UpdateSearchModeUi();
        _ = RunSearchAsync();
    }

    private void SearchMusicMode_Click(object sender, RoutedEventArgs e)
    {
        _searchMediaKind = "Music";
        UpdateSearchModeUi();
        _ = RunSearchAsync();
    }

    private void SearchMusicVideoMode_Click(object sender, RoutedEventArgs e)
    {
        _searchMediaKind = "MusicVideo";
        UpdateSearchModeUi();
        _ = RunSearchAsync();
    }

    private bool IsMusicSearchMode => _searchMediaKind is "Music" or "MusicVideo";

    private void UpdateSearchModeUi()
    {
        if (SearchKaraokeButton is null || SearchMusicButton is null || SearchMusicVideoButton is null) return;
        // Karaoke results leave the singer rotation visible as a drop target. Music
        // results move over the centre so both music playlists remain reachable.
        Grid.SetColumn(SearchResultsOverlay, IsMusicSearchMode ? 2 : 0);
        SetButtonActive(SearchKaraokeButton, _searchMediaKind == "Karaoke");
        SetButtonActive(SearchMusicButton, _searchMediaKind == "Music");
        SetButtonActive(SearchMusicVideoButton, _searchMediaKind == "MusicVideo");
        SearchGrid.SelectionMode = IsMusicSearchMode ? DataGridSelectionMode.Extended : DataGridSelectionMode.Single;
        SearchSelectAllButton.Visibility = IsMusicSearchMode ? Visibility.Visible : Visibility.Collapsed;
        SearchResultsTitle.Text = _searchMediaKind == "MusicVideo" ? "MUSIC VIDEO SEARCH RESULTS" : _searchMediaKind.ToUpperInvariant() + " SEARCH RESULTS";
        SearchDragHint.Text = _searchMediaKind == "Karaoke"
            ? "Drag a karaoke result onto a singer. CD+G, ZIP and video karaoke are all treated as KARAOKE."
            : "Shift-click selects a range; Ctrl-click selects individual tracks; Ctrl+A selects every result. Drag or add the selection to a deck.";
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => await RunSearchAsync();

    private async Task RunSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var text = SearchBox.Text.Trim();
        if (text.Length < 2)
        {
            SearchGrid.ItemsSource = null;
            SearchResultsOverlay.Visibility = Visibility.Collapsed;
            SearchStatus.Text = "Type 2+ characters";
            return;
        }

        try
        {
            await Task.Delay(120, _searchCts.Token);
            var rows = await _library.SearchByKindAsync(text, _searchMediaKind, 300, _searchCts.Token);
            SearchGrid.ItemsSource = rows;
            SearchResultsOverlay.Visibility = Visibility.Visible;
            SearchStatus.Text = $"{rows.Count} {_searchMediaKind.ToLowerInvariant()} results";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SearchStatus.Text = "Search error: " + ex.Message; }
    }

    private void SearchClose_Click(object sender, RoutedEventArgs e)
    {
        SearchResultsOverlay.Visibility = Visibility.Collapsed;
        SearchBox.Focus();
    }

    private async void SearchGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SearchGrid.SelectedItem is not SongRecord song) return;
        if (IsMusicSearchMode)
        {
            AddSearchSongToMusicDeck(song, DeckAPlaylist);
            SearchStatus.Text = $"Added {song.Title} to Deck 1";
            return;
        }

        if (QueueList.SelectedItem is SingerQueueEntry selectedSinger)
        {
            if (!await AddSongToSingerAsync(selectedSinger, song)) return;
            QueueDragHint.Text = $"Added {song.Title} to {selectedSinger.SingerName}";
            UpdateAudienceNext();
            return;
        }

        var singerName = SingerNameBox.Text.Trim();
        if (singerName.Length > 0)
        {
            var singer = await GetOrCreateQueueSingerAsync(singerName);
            if (!await AddSongToSingerAsync(singer, song)) return;
            QueueList.SelectedItem = singer;
            SingerNameBox.Text = string.Empty;
            UpdateAudienceNext();
            return;
        }

        MessageBox.Show("Select a singer row, type a singer name, or drag this karaoke result directly onto a singer.",
            "Assign Karaoke Song", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SearchGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _searchDragStart = e.GetPosition(SearchGrid);
        _searchDragSong = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as SongRecord;
    }

    private void SearchGrid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _searchDragSong is null) return;
        var p = e.GetPosition(SearchGrid);
        if (Math.Abs(p.X - _searchDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _searchDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var song = _searchDragSong;
        var data = new DataObject(typeof(SongRecord), song);
        if (IsMusicSearchMode)
        {
            var songs = GetSelectedMusicSearchSongs();
            if (songs.Count > 0) data.SetData(SearchSongBatchDataFormat, songs.ToArray());
        }
        DragDrop.DoDragDrop(SearchGrid, data, DragDropEffects.Copy);
        _searchDragSong = null;
    }

    private void SearchGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || !IsMusicSearchMode) return;
        e.Handled = true;

        if (_quickSearchMusicActive || _quickSearchMusicFadeInActive)
        {
            StopQuickSearchMusic(includeRegularDecks: true, updateStatus: true);
            return;
        }

        if (SearchGrid.SelectedItem is not SongRecord song)
        {
            UpdateMusicAutomationStatus("Select a music search result, then press SPACE");
            return;
        }

        StartQuickSearchMusic(song);
    }

    private void StartQuickSearchMusic(SongRecord song)
    {
        if (!string.Equals(song.MediaKind, "Music", StringComparison.OrdinalIgnoreCase)) return;
        if (_karaokePresentationActive || _karaokePlaying)
        {
            UpdateMusicAutomationStatus("SPACE quick-play is unavailable while karaoke is playing");
            return;
        }
        if (string.IsNullOrWhiteSpace(song.FilePath) || !File.Exists(song.FilePath))
        {
            BrokenMediaRegistry.Mark(song.FilePath, "File missing or unavailable");
            MessageBox.Show("The indexed file is no longer present:\n" + song.FilePath, "Missing Music File",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var kind = MediaFileClassifier.Classify(song.FilePath);
        if (kind is HazzMediaKind.ZipKaraoke or HazzMediaKind.CdgGraphics or HazzMediaKind.Unknown)
        {
            MessageBox.Show("That search result cannot be quick-played as a music track.", "Music Quick Play",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        CancelMusicTransitions();
        _musicSuspendedForKaraoke = false;
        _resumeMusicDeck = MusicDeckId.None;
        _resumeMusicIndex = -1;
        _resumeMusicItem = null;

        _quickSearchFadeStartDeck1 = _deck1FadeFactor;
        _quickSearchFadeStartDeck2 = _deck2FadeFactor;
        _quickSearchTargetVolume = Math.Clamp(
            _activeMusicDeck == MusicDeckId.Deck1 ? DeckAVolume.Value :
            _activeMusicDeck == MusicDeckId.Deck2 ? DeckBVolume.Value :
            Math.Max(DeckAVolume.Value, DeckBVolume.Value), 0.0, 1.0);
        _quickSearchSong = song;

        QuickMusicMedia.Stop();
        QuickMusicMedia.Source = new Uri(song.FilePath);
        QuickMusicMedia.Volume = (_quickSearchFadeStartDeck1 > 0.0001 || _quickSearchFadeStartDeck2 > 0.0001) ? 0.0 : _quickSearchTargetVolume;
        QuickMusicMedia.Play();
        _quickSearchMusicActive = true;
        _quickSearchMusicVideo = MediaFileClassifier.Classify(song.FilePath) == HazzMediaKind.Video;
        _audienceMusicVideoDeck = MusicDeckId.None;
        if (_quickSearchMusicVideo)
            _audience?.ShowMusicVideo(song.FilePath, TimeSpan.Zero, playing: true);
        else
            _audience?.ClearMusicVideo();
        MarkMusicTrackPlayedThisSession(song.FilePath);
        _ = RecordQuickSearchMusicPlaySafeAsync(song);

        if (_quickSearchFadeStartDeck1 > 0.0001 || _quickSearchFadeStartDeck2 > 0.0001)
        {
            _quickSearchMusicFadeInActive = true;
            _quickSearchMusicTransitionStartedUtc = DateTime.UtcNow;
            UpdateMusicAutomationStatus($"Fading to quick play • {FormatSongLabel(song)}");
        }
        else
        {
            _quickSearchMusicFadeInActive = false;
            _activeMusicDeck = MusicDeckId.None;
            UpdateMusicAutomationStatus($"Quick play • {FormatSongLabel(song)} • SPACE again to stop");
        }
    }

    private void UpdateQuickSearchMusicFade()
    {
        var progress = Math.Clamp((DateTime.UtcNow - _quickSearchMusicTransitionStartedUtc).TotalSeconds / CrossfadeSeconds, 0.0, 1.0);
        SetDeckFadeFactor(MusicDeckId.Deck1, _quickSearchFadeStartDeck1 * (1.0 - progress));
        SetDeckFadeFactor(MusicDeckId.Deck2, _quickSearchFadeStartDeck2 * (1.0 - progress));
        QuickMusicMedia.Volume = _quickSearchTargetVolume * progress;
        if (progress < 1.0) return;

        ConsumePlayingRegularMusicDecks();
        _quickSearchMusicFadeInActive = false;
        _activeMusicDeck = MusicDeckId.None;
        if (_quickSearchSong is not null)
            UpdateMusicAutomationStatus($"Quick play • {FormatSongLabel(_quickSearchSong)} • SPACE again to stop");
    }

    private void StopQuickSearchMusic(bool includeRegularDecks, bool updateStatus)
    {
        _quickSearchMusicFadeInActive = false;
        _quickSearchMusicActive = false;
        QuickMusicMedia.Stop();
        QuickMusicMedia.Source = null;
        QuickMusicMedia.Volume = 0.0;
        _quickSearchSong = null;
        if (_quickSearchMusicVideo) _audience?.ClearMusicVideo();
        _quickSearchMusicVideo = false;

        if (includeRegularDecks) ConsumePlayingRegularMusicDecks();

        if (updateStatus)
            UpdateMusicAutomationStatus("Music stopped • waiting for NEXT KARAOKE SONG or PLAY MUSIC");
    }

    private void FinishQuickSearchMusic(string status)
    {
        _quickSearchMusicFadeInActive = false;
        _quickSearchMusicActive = false;
        QuickMusicMedia.Stop();
        QuickMusicMedia.Source = null;
        QuickMusicMedia.Volume = 0.0;
        _quickSearchSong = null;
        if (_quickSearchMusicVideo) _audience?.ClearMusicVideo();
        _quickSearchMusicVideo = false;
        ConsumePlayingRegularMusicDecks();
        UpdateMusicAutomationStatus(status);
    }

    private void ConsumePlayingRegularMusicDecks()
    {
        CancelMusicTransitions();
        foreach (var deck in new[] { MusicDeckId.Deck1, MusicDeckId.Deck2 })
        {
            StopDeck(deck);
            if (CurrentMusicItemFor(deck) is not null) RemovePlayedMusicItemFromPlaylist(deck);
            SetDeckFadeFactor(deck, 0.0);
        }
        _activeMusicDeck = MusicDeckId.None;
    }

    private async Task RecordQuickSearchMusicPlaySafeAsync(SongRecord song)
    {
        try
        {
            await Task.Delay(250, _lifetime.Token);
            double? durationSeconds = null;
            if (_quickSearchMusicActive && _quickSearchSong?.Id == song.Id)
            {
                var duration = MediaDuration(QuickMusicMedia);
                if (duration > TimeSpan.Zero) durationSeconds = duration.TotalSeconds;
            }
            await _musicPlaylists.RecordMusicPlayAsync(
                "Search Quick Play", 1, song.Id, song.FilePath, song.Artist, song.Title, DateTimeOffset.Now, durationSeconds, _lifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => UpdateMusicAutomationStatus("Music history warning: " + ex.Message));
        }
    }

    private static string FormatSongLabel(SongRecord song)
        => string.IsNullOrWhiteSpace(song.Artist) ? song.Title : $"{song.Artist} — {song.Title}";

    private void SearchAddDeck1_Click(object sender, RoutedEventArgs e)
    {
        AddSelectedSearchSongsToMusicDeck(DeckAPlaylist);
    }

    private void SearchAddDeck2_Click(object sender, RoutedEventArgs e)
    {
        AddSelectedSearchSongsToMusicDeck(DeckBPlaylist);
    }

    private void SearchSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (!IsMusicSearchMode || SearchGrid.Items.Count == 0) return;
        SearchGrid.SelectAll();
        SearchStatus.Text = $"Selected {SearchGrid.SelectedItems.Count:N0} music results";
    }

    private IReadOnlyList<SongRecord> GetSelectedMusicSearchSongs()
    {
        if (!IsMusicSearchMode) return Array.Empty<SongRecord>();
        var selected = SearchGrid.SelectedItems.OfType<SongRecord>()
            .Where(song => string.Equals(song.MediaKind, "Music", StringComparison.OrdinalIgnoreCase))
            .ToHashSet();
        if (selected.Count == 0 && SearchGrid.SelectedItem is SongRecord current &&
            string.Equals(current.MediaKind, "Music", StringComparison.OrdinalIgnoreCase))
            selected.Add(current);
        return SearchGrid.Items.OfType<SongRecord>().Where(selected.Contains).ToArray();
    }

    private void AddSelectedSearchSongsToMusicDeck(ListBox deck)
    {
        var songs = GetSelectedMusicSearchSongs();
        if (songs.Count == 0)
        {
            SearchStatus.Text = "Select one or more music results first";
            return;
        }

        var added = 0;
        var broken = 0;
        foreach (var song in songs)
        {
            if (!File.Exists(song.FilePath))
            {
                BrokenMediaRegistry.Mark(song.FilePath, "File missing or unavailable");
                broken++;
                continue;
            }
            deck.Items.Add(CreateMusicQueueItem(song));
            added++;
        }

        RenumberPlaylist(deck);
        if (deck.SelectedIndex < 0 && deck.Items.Count > 0) deck.SelectedIndex = 0;
        var deckNumber = ReferenceEquals(deck, DeckBPlaylist) ? 2 : 1;
        var targetName = _singleDeckMode && deckNumber == 2 ? "Side List" : $"Deck {deckNumber}";
        SearchStatus.Text = broken == 0
            ? $"Added {added:N0} track(s) to {targetName}"
            : $"Added {added:N0} track(s) to {targetName}; skipped {broken:N0} missing file(s)";
    }

    private void AddSearchSongToMusicDeck(SongRecord song, ListBox deck)
    {
        if (!IsMusicSearchMode || !string.Equals(song.MediaKind, "Music", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Switch search to MUSIC before adding a result to a music deck.", "Music Search");
            return;
        }
        if (!File.Exists(song.FilePath))
        {
            BrokenMediaRegistry.Mark(song.FilePath, "File missing or unavailable");
            MessageBox.Show("The indexed file is no longer present:\n" + song.FilePath, "Missing Music File");
            return;
        }
        deck.Items.Add(CreateMusicQueueItem(song));
        RenumberPlaylist(deck);
        if (deck.SelectedIndex < 0) deck.SelectedIndex = 0;
    }

    private void Queue_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (SingerQueueEntry singer in e.OldItems) singer.PropertyChanged -= QueueSinger_PropertyChanged;
        if (e.NewItems is not null)
            foreach (SingerQueueEntry singer in e.NewItems) singer.PropertyChanged += QueueSinger_PropertyChanged;
        MarkLiveShowStateDirty();
    }

    private void QueueSinger_PropertyChanged(object? sender, PropertyChangedEventArgs e) => MarkLiveShowStateDirty();

    private void MarkLiveShowStateDirty()
    {
        if (_restoringLiveShowState) return;
        _liveShowStateDirty = true;
        _liveShowStateSaveTimer.Stop();
        _liveShowStateSaveTimer.Start();
    }

    private void SaveLiveShowStateNow(bool cleanShutdown)
    {
        _liveShowStateSaveTimer.Stop();
        _liveShowStateDirty = false;
        if (_restoringLiveShowState) return;
        if (_queue.Count == 0)
        {
            _liveShowStateStore.Clear();
            return;
        }

        var snapshot = new LiveShowSnapshot
        {
            ShowStartedUtc = _showStartedUtc,
            SavedUtc = DateTimeOffset.UtcNow,
            CleanShutdown = cleanShutdown,
            SelectedSingerQueueId = (QueueList.SelectedItem as SingerQueueEntry)?.Id,
            Singers = _queue.Select(singer => new LiveShowSingerSnapshot
            {
                QueueId = singer.Id,
                SingerId = singer.SingerId,
                SingerName = singer.SingerName,
                IsHeld = singer.IsHeld,
                Songs = singer.Songs.Select(song => new LiveShowSongSnapshot
                {
                    QueueSongId = song.Id,
                    SongId = song.SongId,
                    SongTitle = song.SongTitle,
                    Artist = song.Artist,
                    FilePath = song.FilePath,
                    KeyChange = song.KeyChange,
                    CdgSyncSeconds = song.CdgSyncSeconds
                }).ToList()
            }).ToList()
        };
        _liveShowStateStore.Save(snapshot);
    }

    private void TryRestoreLiveShowState()
    {
        var snapshot = _liveShowStateStore.Load();
        if (snapshot is null || snapshot.Singers.Count == 0)
        {
            _showStartedUtc = DateTimeOffset.UtcNow;
            return;
        }

        var age = DateTimeOffset.UtcNow - snapshot.SavedUtc;
        var ageText = age.TotalHours >= 24
            ? $"{Math.Max(1, (int)Math.Round(age.TotalDays))} day(s) ago"
            : age.TotalHours >= 1
                ? $"{Math.Max(1, (int)Math.Round(age.TotalHours))} hour(s) ago"
                : $"{Math.Max(1, (int)Math.Round(age.TotalMinutes))} minute(s) ago";
        var heading = snapshot.CleanShutdown
            ? "Hazz has an unfinished singer rotation from the previous session."
            : "Hazz detected an interrupted show and has a recovery snapshot.";
        var result = MessageBox.Show(this,
            $"{heading}\n\nSaved {ageText}.\nSingers: {snapshot.Singers.Count:N0}\n\nRestore the singer rotation and every queued singer song?\n\nDeck 1 and Deck 2 remaining music queues are restored automatically as well.",
            "Restore Last Show", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            _liveShowStateStore.Clear();
            _showStartedUtc = DateTimeOffset.UtcNow;
            return;
        }

        _restoringLiveShowState = true;
        try
        {
            _queue.Clear();
            foreach (var savedSinger in snapshot.Singers)
            {
                if (string.IsNullOrWhiteSpace(savedSinger.SingerName)) continue;
                var singer = new SingerQueueEntry
                {
                    Id = savedSinger.QueueId == Guid.Empty ? Guid.NewGuid() : savedSinger.QueueId,
                    SingerId = savedSinger.SingerId,
                    SingerName = savedSinger.SingerName,
                    IsHeld = savedSinger.IsHeld
                };
                foreach (var savedSong in savedSinger.Songs)
                {
                    singer.Songs.Add(new SingerSongEntry
                    {
                        Id = savedSong.QueueSongId == Guid.Empty ? Guid.NewGuid() : savedSong.QueueSongId,
                        SongId = savedSong.SongId,
                        SongTitle = savedSong.SongTitle,
                        Artist = savedSong.Artist,
                        FilePath = savedSong.FilePath,
                        KeyChange = savedSong.KeyChange,
                        CdgSyncSeconds = savedSong.CdgSyncSeconds
                    });
                }
                _queue.Add(singer);
            }
            _showStartedUtc = snapshot.ShowStartedUtc == default ? DateTimeOffset.UtcNow : snapshot.ShowStartedUtc;
            var selected = snapshot.SelectedSingerQueueId is Guid selectedId
                ? _queue.FirstOrDefault(x => x.Id == selectedId)
                : null;
            QueueList.SelectedItem = selected ?? _queue.FirstOrDefault(x => !x.IsHeld) ?? _queue.FirstOrDefault();
            if (QueueList.SelectedItem is not null) QueueList.ScrollIntoView(QueueList.SelectedItem);
            QueueList.Items.Refresh();
            QueueDragHint.Text = $"Recovered {_queue.Count:N0} singer(s) from the last show";
        }
        finally
        {
            _restoringLiveShowState = false;
        }
        UpdateAudienceNext();
        MarkLiveShowStateDirty(); // Marks this running session as not-clean until Hazz closes normally.
    }

    private async void AddSinger_Click(object sender, RoutedEventArgs e)
    {
        var singerName = SingerNameBox.Text.Trim();
        var manual = ManualSongBox.Text.Trim();

        // The old unlabelled layout made the wider song field look like the singer-name field.
        // Accept a name entered there on its own so ADD SINGER always does what the host expects.
        if (singerName.Length == 0 && manual.Length > 0)
        {
            singerName = manual;
            manual = string.Empty;
        }

        if (singerName.Length == 0)
        {
            QueueDragHint.Text = "Enter a singer name, then press ADD SINGER";
            SingerNameBox.Focus();
            return;
        }

        try
        {
            var singer = await GetOrCreateQueueSingerAsync(singerName);
            if (manual.Length > 0)
                singer.Songs.Add(new SingerSongEntry { SongTitle = manual });
            SingerNameBox.Text = string.Empty;
            ManualSongBox.Clear();
            QueueList.SelectedItem = singer;
            QueueList.ScrollIntoView(singer);
            QueueDragHint.Text = $"Added {singer.SingerName} to the singers list";
            await RefreshSavedSingerNamesAsync();
            UpdateAudienceNext();
        }
        catch (Exception ex)
        {
            QueueDragHint.Text = "Singer could not be added";
            MessageBox.Show(this, $"Hazz could not add {singerName}.\n\n{ex.Message}",
                "Add Singer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<SingerQueueEntry> GetOrCreateQueueSingerAsync(string singerName)
    {
        singerName = singerName.Trim();
        var existing = _queue.FirstOrDefault(x => string.Equals(x.SingerName, singerName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        var id = await _singers.UpsertSingerAsync(singerName, cancellationToken: _lifetime.Token);
        var singer = new SingerQueueEntry { SingerId = id, SingerName = singerName };
        _queue.Add(singer);
        return singer;
    }

    private async Task<bool> AddSongToSingerAsync(SingerQueueEntry singer, SongRecord song, bool warnAboutDuplicate = true)
    {
        if (warnAboutDuplicate && !await ConfirmDuplicateRequestAsync(singer, song.Id, song.Artist, song.Title)) return false;
        singer.Songs.Add(new SingerSongEntry
        {
            SongId = song.Id,
            SongTitle = song.Title,
            Artist = song.Artist,
            FilePath = song.FilePath,
            KeyChange = song.PreferredKey,
            CdgSyncSeconds = song.CdgSyncSeconds
        });
        MarkLiveShowStateDirty();
        return true;
    }

    private async Task<bool> ConfirmDuplicateRequestAsync(SingerQueueEntry targetSinger, long? songId, string artist, string title)
    {
        try
        {
            var recent = await _singers.FindRecentPerformanceAsync(songId, artist, title, _showStartedUtc, _lifetime.Token);
            if (recent is null) return true;

            var elapsed = DateTimeOffset.UtcNow - recent.SungAt.ToUniversalTime();
            var ago = elapsed.TotalHours >= 1
                ? $"{Math.Max(1, (int)Math.Round(elapsed.TotalHours))} hour(s) ago"
                : $"{Math.Max(1, (int)Math.Round(elapsed.TotalMinutes))} minute(s) ago";
            var sameSinger = targetSinger.SingerId is long targetId && targetId == recent.SingerId;
            var message = sameSinger
                ? $"{targetSinger.SingerName} already sang '{title}' {ago} during this show.\n\nQueue the same song again?"
                : $"'{title}' was already sung by {recent.SingerName} {ago} during this show.\n\nQueue it for {targetSinger.SingerName} anyway?";
            return MessageBox.Show(this, message, "Duplicate Karaoke Request", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            App.WriteDiagnostic("DUPLICATE REQUEST CHECK", ex.ToString());
            // A duplicate-warning lookup must never prevent the host from queueing a song.
            return true;
        }
    }

    private void QueueUp_Click(object sender, RoutedEventArgs e)
    {
        var i = QueueList.SelectedIndex;
        if (i > 0) { _queue.Move(i, i - 1); QueueList.SelectedIndex = i - 1; UpdateAudienceNext(); }
    }

    private void QueueDown_Click(object sender, RoutedEventArgs e)
    {
        var i = QueueList.SelectedIndex;
        if (i >= 0 && i < _queue.Count - 1) { _queue.Move(i, i + 1); QueueList.SelectedIndex = i + 1; UpdateAudienceNext(); }
    }

    private void QueueRemove_Click(object sender, RoutedEventArgs e) => SingerMenuRemove_Click(sender, e);

    private void SetNext_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is not SingerQueueEntry singer) return;
        singer.IsHeld = false;
        var i = _queue.IndexOf(singer);
        if (i > 0) _queue.Move(i, 0);
        QueueList.SelectedItem = singer;
        UpdateAudienceNext();
    }

    private void QueueList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (QueueList.SelectedItem is SingerQueueEntry singer) OpenSingerSongsWindow(singer);
    }

    private void OpenSingerSongsWindow(SingerQueueEntry singer)
    {
        var window = new SingerSongsWindow(singer, _singers, _library,
            (songId, artist, title) => ConfirmDuplicateRequestAsync(singer, songId, artist, title)) { Owner = this };
        window.ShowDialog();
        singer.RefreshSongSummary();
        QueueList.Items.Refresh();
        UpdateAudienceNext();
    }

    private void QueueList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.Item is SingerQueueEntry singer)
        {
            QueueList.SelectedItem = singer;
            row.Focus();
        }
    }

    private void SingerContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        var singer = QueueList.SelectedItem as SingerQueueEntry;
        var holdItem = menu.Items.OfType<MenuItem>().FirstOrDefault(x => string.Equals(x.Tag?.ToString(), "HoldToggle", StringComparison.Ordinal));
        if (holdItem is not null) holdItem.Header = singer?.IsHeld == true ? "RELEASE HOLD" : "HOLD SINGER";
    }

    private void SingerMenuAddSong_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is not SingerQueueEntry singer) return;
        QueueList.SelectedItem = singer;
        _searchMediaKind = "Karaoke";
        UpdateSearchModeUi();
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        SearchStatus.Text = $"Search karaoke for {singer.SingerName}";
    }

    private void SingerMenuOpenHistory_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is SingerQueueEntry singer) OpenSingerSongsWindow(singer);
    }

    private void SingerMenuHold_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is not SingerQueueEntry singer) return;
        singer.IsHeld = !singer.IsHeld;
        QueueDragHint.Text = singer.IsHeld
            ? $"{singer.SingerName} is ON HOLD and will be skipped until released"
            : $"{singer.SingerName} released from hold";
        QueueList.Items.Refresh();
        UpdateAudienceNext();
    }

    private void SingerMenuSkipOnce_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is not SingerQueueEntry singer) return;
        var index = _queue.IndexOf(singer);
        if (index < 0) return;
        if (index < _queue.Count - 1) _queue.Move(index, _queue.Count - 1);
        QueueList.SelectedItem = singer;
        QueueList.ScrollIntoView(singer);
        QueueDragHint.Text = $"{singer.SingerName} skipped once • moved to the bottom of the rotation";
        UpdateAudienceNext();
    }

    private void SingerMenuSetNext_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is not SingerQueueEntry singer) return;
        singer.IsHeld = false;
        var index = _queue.IndexOf(singer);
        if (index > 0) _queue.Move(index, 0);
        QueueList.SelectedItem = singer;
        QueueList.ScrollIntoView(singer);
        QueueDragHint.Text = $"{singer.SingerName} is now next";
        UpdateAudienceNext();
    }

    private void SingerMenuMoveUp_Click(object sender, RoutedEventArgs e) => QueueUp_Click(sender, e);
    private void SingerMenuMoveDown_Click(object sender, RoutedEventArgs e) => QueueDown_Click(sender, e);
    private void SingerMenuMoveTop_Click(object sender, RoutedEventArgs e) => SingerMenuSetNext_Click(sender, e);

    private async void SingerMenuKamikaze_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is SingerQueueEntry singer) await ChooseKamikazeForSingerAsync(singer);
    }

    private void SingerMenuClearSongs_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is not SingerQueueEntry singer || singer.Songs.Count == 0) return;
        if (MessageBox.Show(this,
                $"Remove all {singer.Songs.Count:N0} queued song(s) from {singer.SingerName}?\n\nThis does not delete karaoke files or singer history.",
                "Clear Singer Songs", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        singer.Songs.Clear();
        QueueDragHint.Text = $"Cleared queued songs for {singer.SingerName}";
        UpdateAudienceNext();
    }

    private async void SingerMenuEdit_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is not SingerQueueEntry singer) return;
        try
        {
            var saved = singer.SingerId is long singerId ? await _singers.GetSingerAsync(singerId, _lifetime.Token) : null;
            var dialog = new SingerEditDialog(singer.SingerName, saved?.Notes) { Owner = this };
            if (dialog.ShowDialog() != true) return;

            if (singer.SingerId is long id)
                await _singers.UpdateSingerAsync(id, dialog.SingerName, dialog.Notes, _lifetime.Token);
            singer.SingerName = dialog.SingerName;
            await RefreshSavedSingerNamesAsync();
            QueueList.Items.Refresh();
            QueueDragHint.Text = $"Updated singer details for {singer.SingerName}";
            UpdateAudienceNext();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Could not update the singer. The new name may already exist.\n\n" + ex.Message,
                "Singer Details", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SingerMenuRemove_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is not SingerQueueEntry singer) return;
        if (ReferenceEquals(_activeSinger, singer) && _activeSingerSong is not null)
        {
            MessageBox.Show(this, "This singer's karaoke song is currently loaded or playing. Press STOP before removing them from the show.",
                "Remove Singer", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(this,
                $"Remove {singer.SingerName} from this show?\n\nTheir permanent singer history is kept.",
                "Remove Singer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _queue.Remove(singer);
        QueueDragHint.Text = $"Removed {singer.SingerName} from the current show";
        UpdateAudienceNext();
    }

    private void QueueList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _queueDragStart = e.GetPosition(QueueList);
        _queueDragItem = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as SingerQueueEntry;
    }

    private void QueueList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _queueDragItem is null) return;
        var p = e.GetPosition(QueueList);
        if (Math.Abs(p.X - _queueDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _queueDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var item = _queueDragItem;
        QueueDragHint.Text = $"Moving {item.SingerName} — drop at the new position";
        DragDrop.DoDragDrop(QueueList, new DataObject(typeof(SingerQueueEntry), item), DragDropEffects.Move);
        _queueDragItem = null;
    }

    private void QueueList_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(SingerQueueEntry))) e.Effects = DragDropEffects.Move;
        else if (e.Data.GetDataPresent(typeof(SongRecord)))
        {
            var song = e.Data.GetData(typeof(SongRecord)) as SongRecord;
            e.Effects = song is not null && string.Equals(song.MediaKind, "Karaoke", StringComparison.OrdinalIgnoreCase)
                ? DragDropEffects.Copy : DragDropEffects.None;
        }
        else e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void QueueList_Drop(object sender, DragEventArgs e)
    {
        var point = e.GetPosition(QueueList);
        var hit = QueueList.InputHitTest(point) as DependencyObject;
        var row = FindVisualParent<DataGridRow>(hit);
        var targetSinger = row?.Item as SingerQueueEntry;

        if (e.Data.GetData(typeof(SongRecord)) is SongRecord song)
        {
            if (targetSinger is null || !string.Equals(song.MediaKind, "Karaoke", StringComparison.OrdinalIgnoreCase)) return;
            if (!await AddSongToSingerAsync(targetSinger, song)) { e.Handled = true; return; }
            QueueList.SelectedItem = targetSinger;
            QueueDragHint.Text = $"Added {song.Title} to {targetSinger.SingerName} ({targetSinger.SongCount} songs)";
            UpdateAudienceNext();
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(typeof(SingerQueueEntry)) is not SingerQueueEntry dragged || !_queue.Contains(dragged)) return;
        if (ReferenceEquals(targetSinger, dragged)) return;
        _queue.Remove(dragged);
        var insertIndex = _queue.Count;
        if (targetSinger is not null)
        {
            insertIndex = _queue.IndexOf(targetSinger);
            if (insertIndex < 0) insertIndex = _queue.Count;
            else if (row is not null && e.GetPosition(row).Y > row.ActualHeight / 2.0) insertIndex++;
        }
        insertIndex = Math.Clamp(insertIndex, 0, _queue.Count);
        _queue.Insert(insertIndex, dragged);
        QueueList.SelectedItem = dragged;
        QueueList.ScrollIntoView(dragged);
        QueueDragHint.Text = $"{dragged.SingerName} moved to position {insertIndex + 1}";
        UpdateAudienceNext();
        e.Handled = true;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T wanted) return wanted;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void RefreshDisplayTargets()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        _displayTargets = screens.Select((screen, index) => new DisplayTarget(
            index, screen.DeviceName, screen.Primary, screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height)).ToArray();
        DisplayCombo.ItemsSource = _displayTargets;
        if (_displayTargets.Count == 0) return;
        DisplayCombo.SelectedIndex = _displayTargets.Count > 1 ? 1 : 0;
    }

    private AudienceWindow EnsureAudienceWindow()
    {
        if (_audience is { IsLoaded: true }) return _audience;
        _audience = new AudienceWindow();
        _audience.Closed += (_, _) => _audience = null;
        _audience.Show();
        ApplyOverlaySettings();
        UpdateAudienceNext();
        ApplyCurrentKaraokeVisualToAudience();
        _audience.SetKaraokeActive(_karaokePresentationActive);
        ApplyCurrentMusicVideoToAudience();
        if (_kamikazeBannerRequested) _audience.ShowKamikazeBanner();
        return _audience;
    }

    private void AudienceButton_Click(object sender, RoutedEventArgs e)
    {
        var audience = EnsureAudienceWindow();
        audience.MakeWindowed();
        audience.Activate();
    }

    private void TvDisplay2_Click(object sender, RoutedEventArgs e)
    {
        // Fast live-show shortcut: bypass the Display menu and put the singer/audience
        // output straight onto Hazz's Display 2 target in borderless fullscreen mode.
        // Never fall back to Display 1: that could cover the host controls mid-show.
        RefreshDisplayTargets();
        var target = _displayTargets.FirstOrDefault(x => x.Index == 1);
        if (target is null)
        {
            MessageBox.Show(
                "Display 2 is not connected.\n\nConnect or enable the TV/second display in Windows, then press TV again.",
                "TV / Display 2",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DisplayCombo.SelectedItem = target;
        EnsureAudienceWindow().SendToDisplay(target, fullScreen: true);
    }

    private DisplayTarget? SelectedDisplayTarget()
    {
        if (DisplayCombo.SelectedItem is DisplayTarget selected) return selected;
        RefreshDisplayTargets();
        return DisplayCombo.SelectedItem as DisplayTarget;
    }

    private void SendAudienceToDisplay_Click(object sender, RoutedEventArgs e)
    {
        var target = SelectedDisplayTarget();
        if (target is null) { MessageBox.Show("No Windows display was detected.", "Audience Display"); return; }
        EnsureAudienceWindow().SendToDisplay(target, fullScreen: false);
    }

    private void AudienceFullScreen_Click(object sender, RoutedEventArgs e)
    {
        var target = SelectedDisplayTarget();
        if (target is null) { MessageBox.Show("No Windows display was detected.", "Audience Display"); return; }
        EnsureAudienceWindow().SendToDisplay(target, fullScreen: true);
    }

    private void AudienceWindowed_Click(object sender, RoutedEventArgs e)
    {
        EnsureAudienceWindow().MakeWindowed();
    }

    private void OverlayChanged(object sender, RoutedEventArgs e) { if (IsLoaded) ApplyOverlaySettings(); }
    private void OverlayChanged(object sender, TextChangedEventArgs e) { if (IsLoaded) ApplyOverlaySettings(); }
    private void OverlayChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) ApplyOverlaySettings(); }
    private void OverlayChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (IsLoaded) ApplyOverlaySettings(); }

    private AudienceOverlaySettings CurrentOverlaySettings()
    {
        static double ComboNumber(ComboBox c, double fallback)
            => double.TryParse((c.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var n) ? n : fallback;
        Enum.TryParse<OverlayPosition>((PositionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var pos);
        return new AudienceOverlaySettings
        {
            ShowNextSinger = ShowNextSingerCheck.IsChecked == true,
            ShowNextSong = ShowNextSongCheck.IsChecked == true,
            NextSingerFontFamily = NextFontCombo.SelectedItem?.ToString() ?? "Segoe UI",
            NextSingerFontSize = ComboNumber(NextSizeCombo, 48),
            NextSingerPosition = pos,
            ScrollerEnabled = ScrollerCheck.IsChecked == true,
            ScrollerText = ScrollerTextBox.Text,
            ScrollerFontFamily = ScrollerFontCombo.SelectedItem?.ToString() ?? "Segoe UI",
            ScrollerFontSize = ComboNumber(ScrollerSizeCombo, 30),
            ScrollerPixelsPerSecond = ScrollerSpeedSlider.Value,
            ScrollerPosition = (ScrollerPositionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Bottom",
            NextHeadingColor = _audienceNextHeadingColor,
            NextPositionColor = _audienceNextPositionColor,
            NextSingerColor = _audienceNextSingerColor,
            NextSongColor = _audienceNextSongColor,
            RotationScrollerColor = _audienceRotationScrollerColor,
            VenueScrollerColor = _audienceVenueScrollerColor,
            BackgroundImageEnabled = AudienceBackgroundEnabledCheck.IsChecked == true && (!string.IsNullOrWhiteSpace(_audienceBackgroundImagePath) || !string.IsNullOrWhiteSpace(_audienceBackgroundFolderPath)),
            BackgroundGifSpeed = BackgroundGifSpeedSlider.Value,
            BackgroundStretchMode = SelectedBackgroundStretch(),
            BackgroundFolderPath = _audienceBackgroundFolderPath,
            BackgroundImagePath = _audienceBackgroundImagePath,
            MusicVideoShowLogo = MusicVideoShowLogoCheck.IsChecked == true,
            MusicVideoShowScroller = MusicVideoShowScrollerCheck.IsChecked == true,
            MusicVideoShowSingers = MusicVideoShowSingersCheck.IsChecked == true,
            MusicVideoShowKamikaze = MusicVideoShowKamikazeCheck.IsChecked == true,
            LogoEnabled = AudienceLogoEnabledCheck.IsChecked == true && !string.IsNullOrWhiteSpace(_audienceLogoImagePath),
            LogoImagePath = _audienceLogoImagePath,
            LogoPosition = Enum.TryParse<OverlayPosition>((LogoPositionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var logoPos) ? logoPos : OverlayPosition.TopRight,
            LogoWidth = LogoWidthSlider.Value,
            KamikazeText = KamikazeTextBox.Text,
            KamikazeFontFamily = KamikazeFontCombo.SelectedItem?.ToString() ?? "Segoe UI Black",
            KamikazeFontSize = ComboNumber(KamikazeSizeCombo, 84),
            KamikazeColor = _audienceKamikazeColor
        };
    }

    private string SelectedBackgroundStretch()
        => (BackgroundStretchCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "Fill (crop)" => "FillCrop",
            "Stretch" => "Stretch",
            "Center" => "Center",
            _ => "Fit"
        };

    private static string DisplayBackgroundStretch(string? value)
        => value switch
        {
            "FillCrop" => "Fill (crop)",
            "Stretch" => "Stretch",
            "Center" => "Center",
            _ => "Fit"
        };

    private void ApplyOverlaySettings()
    {
        ScrollerSpeedText.Text = $"{ScrollerSpeedSlider.Value:0} px/s";
        if (LogoWidthText is not null) LogoWidthText.Text = $"{LogoWidthSlider.Value:0}px";
        _audience?.Apply(CurrentOverlaySettings());
        _audience?.SetKaraokeActive(_karaokePresentationActive);
        if (_kamikazeBannerRequested) _audience?.ShowKamikazeBanner();
    }

    private void UpdateAudienceNext()
    {
        MarkLiveShowStateDirty();
        if (_audience is null) return;

        // Held/no-show singers remain visible to the host but are skipped from the
        // audience's live upcoming rotation until the host releases them.
        var rotation = _queue.Where(singer => !singer.IsHeld).Select((singer, index) =>
        {
            var next = singer.NextSong;
            var songText = next is null
                ? string.Empty
                : string.Join(" — ", new[] { next.SongTitle, next.Artist }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return new AudienceSingerDisplayItem
            {
                Position = index + 1,
                SingerName = singer.SingerName,
                SongText = songText
            };
        }).ToList();

        _audience.SetSingerRotation(rotation, ShowNextSongCheck.IsChecked == true);
    }

    private void CdgEarlier_Click(object sender, RoutedEventArgs e) => _cdgTiming.Earlier();
    private void CdgLater_Click(object sender, RoutedEventArgs e) => _cdgTiming.Later();
    private void CdgReset_Click(object sender, RoutedEventArgs e) => _cdgTiming.Reset();
    private void KeyDown_Click(object sender, RoutedEventArgs e) => SetKaraokeKey(_keyChange - 1);
    private void KeyUp_Click(object sender, RoutedEventArgs e) => SetKaraokeKey(_keyChange + 1);
    private void KeyReset_Click(object sender, RoutedEventArgs e) => SetKaraokeKey(0);

    private void SetKaraokeKey(int semitones)
    {
        _keyChange = Math.Clamp(semitones, -6, 6);
        KeyText.Text = $"{_keyChange:+0;-0;0}";
        _pitchAudio.SetSemitones(_keyChange);
        if (_activeSingerSong is not null) _activeSingerSong.KeyChange = _keyChange;
        MarkLiveShowStateDirty();
        if ((_karaokePlaying || _karaokePaused) && !_pitchAudio.IsLoaded)
            SearchStatus.Text = "Live key change unavailable for this file/codec; the selected key is still saved for the singer.";
    }

    private bool CheckoutActiveSingerSongForPerformance()
    {
        if (_activeSingerSongCheckedOut || _activeSinger is null || _activeSingerSong is null) return false;

        var singer = _activeSinger;
        var song = _activeSingerSong;
        if (singer.Songs.Contains(song)) singer.Songs.Remove(song);
        _activeSingerSongCheckedOut = true;

        // PLAY is the rotation handoff point: the singer who has just started moves
        // to the bottom immediately, so the next waiting singer becomes position #1.
        var index = _queue.IndexOf(singer);
        if (index >= 0 && index < _queue.Count - 1) _queue.Move(index, _queue.Count - 1);
        var nextSinger = _queue.FirstOrDefault(x => !x.IsHeld) ?? _queue.FirstOrDefault();
        QueueList.SelectedItem = nextSinger;
        if (nextSinger is not null) QueueList.ScrollIntoView(nextSinger);
        QueueDragHint.Text = $"{singer.SingerName} is singing • moved to the bottom • next singer is now at the top";
        UpdateAudienceNext();
        return true;
    }

    private async Task RecordActiveSingerHistoryAtPlayAsync()
    {
        var singer = _activeSinger;
        var song = _activeSingerSong;
        if (!_activeSingerSongCheckedOut || singer?.SingerId is not long singerId || song is null) return;

        try
        {
            song.KeyChange = _keyChange;
            song.CdgSyncSeconds = _cdgTiming.OffsetSeconds;
            await _singers.AddHistoryAsync(singerId, song.SongId, song.Artist, song.SongTitle, song.FilePath,
                DateTimeOffset.Now, song.KeyChange, song.CdgSyncSeconds, _lifetime.Token);

            // If the singer's song/history window is already open, refresh it immediately so
            // the just-started performance appears while the singer is still on stage.
            foreach (var window in Application.Current.Windows.OfType<SingerSongsWindow>()
                         .Where(w => w.SingerId == singerId))
                await window.RefreshHistoryAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            SearchStatus.Text = "History save warning: " + ex.Message;
        }
    }

    private void ClearStartedSingerSongReference()
    {
        // Once PLAY has handed a request off, that request is consumed from the active
        // singer list. STOP, playback failure, manual music resume, or loading another
        // karaoke track must never silently put it back. Singer history is written at
        // PLAY time, so even a subsequently stopped performance remains in history.
        _activeSingerSongCheckedOut = false;
        _activeSinger = null;
        _activeSingerSong = null;
        QueueList.Items.Refresh();
        UpdateAudienceNext();
    }

    private async void LoadNextSinger_Click(object sender, RoutedEventArgs e) => await LoadNextSingerAsync();

    private async Task<bool> LoadNextSingerAsync()
    {
        if (_activeSingerSongCheckedOut && !_karaokePlaying && !_karaokePaused) ClearStartedSingerSongReference();
        var singer = _queue.FirstOrDefault(x => !x.IsHeld && x.Songs.Count > 0);
        var song = singer?.NextSong;
        if (singer is null || song is null)
        {
            MessageBox.Show("There is no singer with a queued song.", "Load Next Singer", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        if (string.IsNullOrWhiteSpace(song.FilePath) || !File.Exists(song.FilePath))
        {
            BrokenMediaRegistry.Mark(song.FilePath, "File missing or unavailable");
            MessageBox.Show($"{singer.SingerName}'s next song does not have a valid karaoke file assigned.\n\nDouble-click the singer to manage their songs, or drag a KARAOKE search result onto their name.",
                "Karaoke File Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _activeSinger = singer;
        _activeSingerSong = song;
        _activeSingerSongCheckedOut = false;
        var loaded = await LoadKaraokeAsync(song.FilePath);
        if (!loaded)
        {
            _activeSinger = null;
            _activeSingerSong = null;
            return false;
        }

        _keyChange = Math.Clamp(song.KeyChange, -6, 6);
        KeyText.Text = $"{_keyChange:+0;-0;0}";
        _pitchAudio.SetSemitones(_keyChange);
        _cdgTiming.Set(song.CdgSyncSeconds);
        KaraokeNowText.Text = $"{singer.SingerName} — {song.SongTitle}" + (string.IsNullOrWhiteSpace(song.Artist) ? string.Empty : $" — {song.Artist}");
        QueueList.SelectedItem = singer;
        return true;
    }

    private async void LoadKaraoke_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Load karaoke",
            Filter = "Karaoke and video|*.zip;*.cdg;*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac;*.ogg;*.mp4;*.mkv;*.avi;*.mov;*.mpeg;*.mpg;*.wmv;*.m4v;*.vob;*.ts;*.m2ts;*.webm;*.divx|All files|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        ClearStartedSingerSongReference();
        _activeSinger = null;
        _activeSingerSong = null;
        _activeSingerSongCheckedOut = false;
        await LoadKaraokeAsync(dlg.FileName);
    }

    private void KaraokeDeck_DragOver(object sender, DragEventArgs e)
    {
        var karaokeRecord = e.Data.GetData(typeof(SongRecord)) as SongRecord;
        var filePaths = e.Data.GetData(DataFormats.FileDrop) as string[];
        var supportedFile = filePaths?.FirstOrDefault(IsSupportedKaraokeDropPath);
        e.Effects = (karaokeRecord is not null && !string.Equals(karaokeRecord.MediaKind, "Music", StringComparison.OrdinalIgnoreCase)) || supportedFile is not null
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void KaraokeDeck_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (_karaokePlaying || _karaokePresentationActive)
        {
            UpdateMusicAutomationStatus("Fade Stop the current karaoke song before loading another track");
            return;
        }

        var record = e.Data.GetData(typeof(SongRecord)) as SongRecord;
        if (record is not null && string.Equals(record.MediaKind, "Music", StringComparison.OrdinalIgnoreCase)) record = null;
        var path = record?.FilePath;
        if (string.IsNullOrWhiteSpace(path) && e.Data.GetData(DataFormats.FileDrop) is string[] filePaths)
            path = filePaths.FirstOrDefault(IsSupportedKaraokeDropPath);
        if (string.IsNullOrWhiteSpace(path))
        {
            UpdateMusicAutomationStatus("That item is not a supported karaoke track");
            return;
        }

        if (!await LoadKaraokeAsync(path)) return;
        ClearStartedSingerSongReference();
        _activeSinger = null;
        _activeSingerSong = null;
        _activeSingerSongCheckedOut = false;
        if (record is not null)
        {
            _currentKaraokeRecord = record;
            KaraokeNowText.Text = string.IsNullOrWhiteSpace(record.Artist) ? record.Title : $"{record.Artist} — {record.Title}";
        }
        UpdateMusicAutomationStatus($"Karaoke Deck loaded • {KaraokeNowText.Text}");
    }

    private static bool IsSupportedKaraokeDropPath(string? path)
        => !string.IsNullOrWhiteSpace(path) && MediaFileClassifier.Classify(path) != HazzMediaKind.Unknown;

    private async Task<bool> LoadKaraokeAsync(string path, bool preserveAlternativeCycle = false)
    {
        if (!preserveAlternativeCycle)
        {
            _alternativeCandidates = Array.Empty<SongRecord>();
            _alternativeIndex = 0;
            _alternativeArtist = string.Empty;
            _alternativeTitle = string.Empty;
        }
        KaraokeNowText.Text = "Loading " + Path.GetFileName(path) + "...";
        KaraokePackage? candidate = null;
        CdgDecoder? candidateDecoder = null;
        try
        {
            candidate = await KaraokePackageLoader.LoadAsync(path, _lifetime.Token);
            if (candidate.Kind == KaraokePackageKind.CdgPair)
            {
                if (candidate.CdgPath is null) throw new InvalidDataException("CD+G package did not resolve a graphics file.");
                var bytes = await File.ReadAllBytesAsync(candidate.CdgPath, _lifetime.Token);
                candidateDecoder = new CdgDecoder();
                candidateDecoder.Load(bytes);
            }
            ReplaceKaraokePackage(candidate, candidateDecoder, preserveAlternativeCycle);
            candidate = null;
            try { _currentKaraokeRecord = await _library.FindByFilePathAsync(path, _lifetime.Token); }
            catch { _currentKaraokeRecord = null; }
            KaraokeNowText.Text = _karaokePackage?.DisplayTitle ?? Path.GetFileNameWithoutExtension(path);
            return true;
        }
        catch (OperationCanceledException)
        {
            candidate?.Dispose();
            return false;
        }
        catch (Exception ex)
        {
            candidate?.Dispose();
            BrokenMediaRegistry.Mark(path, ex.Message);
            KaraokeNowText.Text = _karaokePackage?.DisplayTitle ?? "No karaoke track loaded";
            MessageBox.Show(ex.Message, "Load Karaoke", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void ReplaceKaraokePackage(KaraokePackage package, CdgDecoder? decoder, bool preservePresentation = false)
    {
        CancelKaraokeStopFade();
        RestoreKaraokePlaybackVolume();
        var keepAudienceInKaraokeMode = preservePresentation && _karaokePresentationActive;
        KaraokeMedia.Stop();
        KaraokeMedia.Source = null;
        _audience?.StopVideo();

        var previous = _karaokePackage;
        _karaokePackage = package;
        _cdgDecoder = decoder;
        _cdgTiming.Reset();
        _keyChange = 0;
        KeyText.Text = "0";
        _karaokePlaying = false;
        _karaokePaused = false;
        if (KaraokePauseButton is not null) KaraokePauseButton.Content = "Ⅱ PAUSE";
        _karaokePresentationActive = keepAudienceInKaraokeMode;
        _audience?.SetKaraokeActive(keepAudienceInKaraokeMode);
        _lastRenderedCdgVersion = -1;

        if (package.Kind == KaraokePackageKind.CdgPair)
        {
            _cdgBitmap = new WriteableBitmap(CdgDecoder.Width, CdgDecoder.Height, 96, 96, PixelFormats.Bgra32, null);
            CdgPreview.Source = _cdgBitmap;
            CdgPreview.Visibility = Visibility.Visible;
            KaraokeMedia.Opacity = 0;
            KaraokePreviewLabel.Visibility = Visibility.Collapsed;
            KaraokeMedia.Source = new Uri(package.PlaybackPath);
            _karaokeVisualTimer.Start();
            RenderCdgAtCurrentPosition(force: true);
        }
        else
        {
            _cdgBitmap = null;
            CdgPreview.Source = null;
            CdgPreview.Visibility = Visibility.Collapsed;
            KaraokeMedia.Opacity = package.Kind == KaraokePackageKind.Video ? 1 : 0;
            KaraokePreviewLabel.Text = package.Kind == KaraokePackageKind.AudioOnly ? "AUDIO ONLY" : "KARAOKE PREVIEW";
            KaraokePreviewLabel.Visibility = package.Kind == KaraokePackageKind.AudioOnly ? Visibility.Visible : Visibility.Collapsed;
            KaraokeMedia.Source = new Uri(package.PlaybackPath);
            _karaokeVisualTimer.Start(); // also maintains audience-video sync
        }

        var pitchReady = _pitchAudio.TryLoad(package.PlaybackPath, out var pitchError);
        KaraokeMedia.IsMuted = pitchReady;
        _pitchAudio.SetSemitones(_keyChange);
        if (!pitchReady && !string.IsNullOrWhiteSpace(pitchError))
            SearchStatus.Text = "Windows playback loaded; live key DSP unavailable for this file: " + pitchError;

        previous?.Dispose();
        ApplyCurrentKaraokeVisualToAudience();
    }

    private async void KaraokePlay_Click(object sender, RoutedEventArgs e)
    {
        if (_karaokePackage is null || KaraokeMedia.Source is null)
        {
            if (!await LoadNextSingerAsync()) return;
        }
        if (_karaokePackage is null || KaraokeMedia.Source is null) return;

        CancelKaraokeStopFade();
        RestoreKaraokePlaybackVolume();

        if (AutoSkipSilenceCheck?.IsChecked == true && KaraokeMedia.Position.TotalSeconds < 0.15)
            await SkipSilenceAsync(autoStart: true);

        // Starting the next karaoke song ends any pending Kamikaze announcement.
        _kamikazeBannerRequested = false;
        _audience?.HideKamikazeBanner();
        var startedSingerPerformance = CheckoutActiveSingerSongForPerformance();
        if (_quickSearchMusicActive || _quickSearchMusicFadeInActive)
            StopQuickSearchMusic(includeRegularDecks: false, updateStatus: false);
        BeginMusicFadeForKaraoke();
        _karaokePresentationActive = true;
        _audience?.SetKaraokeActive(true);
        ApplyCurrentKaraokeVisualToAudience();
        if (_pitchAudio.IsLoaded) _pitchAudio.Play(KaraokeMedia.Position);
        KaraokeMedia.Play();
        _karaokePlaying = true;
        _karaokePaused = false;
        if (startedSingerPerformance) await RecordActiveSingerHistoryAtPlayAsync();
        KaraokePauseButton.Content = "Ⅱ PAUSE";
        _karaokeVisualTimer.Start();
        if (_karaokePackage.Kind == KaraokePackageKind.Video)
            _audience?.PlayVideo(KaraokeMedia.Position);
    }

    private void SkipSilencePreRoll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SkipSilencePreRollText is not null) SkipSilencePreRollText.Text = $"{e.NewValue:0.00}s";
    }

    private double SkipSilencePreRollSeconds => Math.Clamp(SkipSilencePreRollSlider?.Value ?? 0.5, 0.0, 2.0);

    private async void SkipSilenceNow_Click(object sender, RoutedEventArgs e)
        => await SkipSilenceAsync(autoStart: false);

    private async Task SkipSilenceAsync(bool autoStart)
    {
        if (_karaokePackage is null || string.IsNullOrWhiteSpace(_karaokePackage.PlaybackPath) || !File.Exists(_karaokePackage.PlaybackPath))
            return;

        _silenceScanCts?.Cancel();
        _silenceScanCts?.Dispose();
        _silenceScanCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _silenceScanCts.Token;

        var start = autoStart ? TimeSpan.Zero : KaraokeMedia.Position;
        var previousStatus = SearchStatus.Text;
        SearchStatus.Text = autoStart ? "Checking karaoke lead-in for silence..." : "Looking for the next audible section...";

        try
        {
            var audible = await SilenceDetector.FindNextAudibleAsync(
                _karaokePackage.PlaybackPath,
                start,
                thresholdDb: -45.0,
                maxScan: autoStart ? TimeSpan.FromMinutes(3) : TimeSpan.FromMinutes(5),
                cancellationToken: token);

            if (audible is null)
            {
                SearchStatus.Text = autoStart ? previousStatus : "No later audible section found";
                return;
            }

            var target = audible.Value - TimeSpan.FromSeconds(SkipSilencePreRollSeconds);
            if (target < TimeSpan.Zero) target = TimeSpan.Zero;

            // If we are already in audible material, do not create a pointless seek.
            if (!autoStart && target <= start + TimeSpan.FromMilliseconds(150))
            {
                SearchStatus.Text = "Current position is already audible";
                return;
            }

            KaraokeMedia.Position = target;
            if (_pitchAudio.IsLoaded) _pitchAudio.Position = target;
            RenderCdgAtCurrentPosition(force: true);

            if (_karaokePackage.Kind == KaraokePackageKind.Video && _karaokePresentationActive)
            {
                _audience?.PlayVideo(target);
                if (_karaokePaused) _audience?.PauseVideo();
            }

            UpdatePlayerTimeDisplays(force: true);
            var skipped = Math.Max(0, (target - start).TotalSeconds);
            SearchStatus.Text = autoStart
                ? (skipped >= 0.15 ? $"Silent intro skipped • {skipped:0.00}s" : previousStatus)
                : $"Skipped {skipped:0.00}s of silence";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SearchStatus.Text = "Skip silence unavailable for this file/codec: " + ex.Message;
        }
    }

    private void KaraokePause_Click(object sender, RoutedEventArgs e)
    {
        if (_karaokePackage is null || KaraokeMedia.Source is null || !_karaokePresentationActive) return;

        if (_karaokePlaying)
        {
            KaraokeMedia.Pause();
            _pitchAudio.Pause();
            _karaokePlaying = false;
            _karaokePaused = true;
            KaraokePauseButton.Content = "▶ RESUME";
            _audience?.PauseVideo();
            _audience?.SetKaraokeActive(true);
            RenderCdgAtCurrentPosition(force: true);
            return;
        }

        if (_karaokePaused)
        {
            if (_pitchAudio.IsLoaded) _pitchAudio.Play(KaraokeMedia.Position);
            KaraokeMedia.Play();
            _karaokePlaying = true;
            _karaokePaused = false;
            KaraokePauseButton.Content = "Ⅱ PAUSE";
            _audience?.SetKaraokeActive(true);
            if (_karaokePackage.Kind == KaraokePackageKind.Video) _audience?.PlayVideo(KaraokeMedia.Position);
            _karaokeVisualTimer.Start();
        }
    }

    private async void KaraokeStop_Click(object sender, RoutedEventArgs e)
    {
        // STOP is the live-show handoff back to background music. The singer request
        // remains consumed (it is not restored and is not written to history), the
        // audience karaoke visual is cleared, and the music scheduler resumes from the
        // next track that was remembered when karaoke started.
        await FadeOutKaraokeForMusicResumeAsync();
    }

    private async Task FadeOutKaraokeForMusicResumeAsync()
    {
        CancelKaraokeStopFade();
        if (!_karaokePlaying || _karaokePaused)
        {
            KaraokeStopForMusicResume(cancelPendingFade: false);
            RestoreKaraokePlaybackVolume();
            ResumeMusicAfterKaraoke(forceStart: true, fadeInSeconds: KaraokeStopHandoffSeconds);
            return;
        }

        var fadeCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _karaokeStopFadeCts = fadeCts;
        KaraokeStopButton.IsEnabled = false;
        const int fadeSteps = 20;
        var startingMediaVolume = Math.Clamp(KaraokeMedia.Volume, 0.0, KaraokePlaybackVolume);

        try
        {
            for (var step = 1; step <= fadeSteps; step++)
            {
                fadeCts.Token.ThrowIfCancellationRequested();
                var factor = 1.0 - step / (double)fadeSteps;
                KaraokeMedia.Volume = startingMediaVolume * factor;
                _pitchAudio.SetVolume(KaraokePlaybackVolume * factor);
                await Task.Delay(75, fadeCts.Token);
            }

            KaraokeStopForMusicResume(cancelPendingFade: false);
            RestoreKaraokePlaybackVolume();
            ResumeMusicAfterKaraoke(forceStart: true, fadeInSeconds: KaraokeStopHandoffSeconds);
        }
        catch (OperationCanceledException)
        {
            RestoreKaraokePlaybackVolume();
        }
        finally
        {
            if (ReferenceEquals(_karaokeStopFadeCts, fadeCts)) _karaokeStopFadeCts = null;
            fadeCts.Dispose();
            KaraokeStopButton.IsEnabled = true;
        }
    }

    private void CancelKaraokeStopFade()
    {
        var pending = _karaokeStopFadeCts;
        _karaokeStopFadeCts = null;
        pending?.Cancel();
    }

    private void RestoreKaraokePlaybackVolume()
    {
        KaraokeMedia.Volume = KaraokePlaybackVolume;
        _pitchAudio.SetVolume(KaraokePlaybackVolume);
    }

    private async void FindAlternative_Click(object sender, RoutedEventArgs e)
    {
        var artist = (_activeSingerSong?.Artist ?? _currentKaraokeRecord?.Artist ?? string.Empty).Trim();
        var title = (_activeSingerSong?.SongTitle ?? _currentKaraokeRecord?.Title ?? string.Empty).Trim();
        var currentPath = _karaokePackage?.SourcePath ?? _activeSingerSong?.FilePath ?? _currentKaraokeRecord?.FilePath;
        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show("Hazz needs an indexed Artist + Song Title to find another version. Load this track from the karaoke library/search first.",
                "Find Alternative", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            if (_alternativeCandidates.Count == 0 ||
                !string.Equals(_alternativeArtist, artist, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(_alternativeTitle, title, StringComparison.OrdinalIgnoreCase))
            {
                _alternativeCandidates = await _library.FindAlternativesAsync(artist, title, currentPath, 200, _lifetime.Token);
                _alternativeArtist = artist;
                _alternativeTitle = title;
                _alternativeIndex = 0;
            }

            if (_alternativeCandidates.Count == 0)
            {
                MessageBox.Show("No other indexed karaoke versions of this artist/title were found.", "Find Alternative", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_alternativeIndex >= _alternativeCandidates.Count) _alternativeIndex = 0;
            var candidate = _alternativeCandidates[_alternativeIndex++];
            if (!File.Exists(candidate.FilePath))
            {
                MessageBox.Show($"The next alternative is indexed but the file is missing:\n\n{candidate.FilePath}\n\nPress FIND ALTERNATIVE again for the next version.",
                    "Find Alternative", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var singerKey = _keyChange;
            var loaded = await LoadKaraokeAsync(candidate.FilePath, preserveAlternativeCycle: true);
            if (!loaded) return;

            _currentKaraokeRecord = candidate;
            _keyChange = singerKey;
            KeyText.Text = $"{_keyChange:+0;-0;0}";
            _pitchAudio.SetSemitones(_keyChange);
            _cdgTiming.Set(candidate.CdgSyncSeconds);
            if (AutoSkipSilenceCheck?.IsChecked == true)
                await SkipSilenceAsync(autoStart: true);
            if (_activeSingerSong is not null)
            {
                _activeSingerSong.SongId = candidate.Id;
                _activeSingerSong.FilePath = candidate.FilePath;
                _activeSingerSong.CdgSyncSeconds = candidate.CdgSyncSeconds;
            }

            _kamikazeBannerRequested = false;
            _audience?.HideKamikazeBanner();
            var startedSingerPerformance = CheckoutActiveSingerSongForPerformance();
            BeginMusicFadeForKaraoke();
            _karaokePresentationActive = true;
            _karaokePlaying = true;
            _karaokePaused = false;
            KaraokePauseButton.Content = "Ⅱ PAUSE";
            _audience?.SetKaraokeActive(true);
            ApplyCurrentKaraokeVisualToAudience();
            KaraokeMedia.Position = TimeSpan.Zero;
            if (_pitchAudio.IsLoaded) _pitchAudio.Play(TimeSpan.Zero);
            KaraokeMedia.Play();
            if (_karaokePackage?.Kind == KaraokePackageKind.Video) _audience?.PlayVideo(TimeSpan.Zero);
            if (startedSingerPerformance) await RecordActiveSingerHistoryAtPlayAsync();

            var version = string.Join(" ", new[] { candidate.Manufacturer, candidate.DiscId }.Where(x => !string.IsNullOrWhiteSpace(x)));
            KaraokeNowText.Text = string.IsNullOrWhiteSpace(version)
                ? $"{artist} — {title} (alternative {_alternativeIndex}/{_alternativeCandidates.Count})"
                : $"{artist} — {title} — {version} (alternative {_alternativeIndex}/{_alternativeCandidates.Count})";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Find Alternative", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Kamikaze_Click(object sender, RoutedEventArgs e)
    {
        var singer = _queue.FirstOrDefault(x => !x.IsHeld);
        if (singer is null)
        {
            MessageBox.Show("Add or release at least one singer in the rotation before using Kamikaze Karaoke.", "Kamikaze Karaoke", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await ChooseKamikazeForSingerAsync(singer);
    }

    private async Task ChooseKamikazeForSingerAsync(SingerQueueEntry singer)
    {
        // Never let a second click start another expensive library request in parallel.
        // The button is re-enabled as soon as the small random candidate query is complete.
        if (_kamikazePickInProgress) return;
        _kamikazePickInProgress = true;
        KamikazeButton.IsEnabled = false;
        KamikazeButton.Content = "CHOOSING...";
        _kamikazePickCts?.Cancel();
        _kamikazePickCts?.Dispose();
        _kamikazePickCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _kamikazePickCts.Token;

        try
        {
            SearchStatus.Text = $"Kamikaze: choosing a random karaoke song for {singer.SingerName}...";
            var randomSong = await PickRandomKaraokeSongAsync(_kamikazeLastPath, token);
            if (randomSong is null)
            {
                MessageBox.Show("No playable karaoke tracks were found in the indexed Karaoke library. Import or rescan your karaoke folders first.",
                    "Kamikaze Karaoke", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Repeated presses replace the previous Kamikaze choice instead of stacking
            // random tracks in front of the singer's genuine requests.
            if (_kamikazeSinger is not null && _kamikazeAssignedSong is not null && _kamikazeSinger.Songs.Contains(_kamikazeAssignedSong))
                _kamikazeSinger.Songs.Remove(_kamikazeAssignedSong);

            var assigned = new SingerSongEntry
            {
                SongId = randomSong.Id,
                SongTitle = randomSong.Title,
                Artist = randomSong.Artist,
                FilePath = randomSong.FilePath,
                KeyChange = randomSong.PreferredKey,
                CdgSyncSeconds = randomSong.CdgSyncSeconds
            };
            singer.Songs.Insert(0, assigned);
            _kamikazeSinger = singer;
            _kamikazeAssignedSong = assigned;
            _kamikazeLastPath = randomSong.FilePath;
            _kamikazeBannerRequested = true;

            ApplyOverlaySettings();
            _audience?.ShowKamikazeBanner();
            UpdateAudienceNext();
            QueueList.SelectedItem = singer;
            QueueList.ScrollIntoView(singer);

            // If no karaoke performance is currently under way, prepare the random track
            // immediately. Package/ZIP work already runs off the UI thread in KaraokePackageLoader.
            if (!_karaokePresentationActive && !_karaokePlaying && !_karaokePaused)
            {
                _activeSinger = singer;
                _activeSingerSong = assigned;
                _activeSingerSongCheckedOut = false;
                if (await LoadKaraokeAsync(randomSong.FilePath))
                {
                    _keyChange = Math.Clamp(assigned.KeyChange, -6, 6);
                    KeyText.Text = $"{_keyChange:+0;-0;0}";
                    _pitchAudio.SetSemitones(_keyChange);
                    _cdgTiming.Set(assigned.CdgSyncSeconds);
                    KaraokeNowText.Text = $"KAMIKAZE • {singer.SingerName} — {assigned.SongTitle}" +
                        (string.IsNullOrWhiteSpace(assigned.Artist) ? string.Empty : $" — {assigned.Artist}");
                    // Keep the random selection visually hidden until PLAY. The audience sees
                    // the Kamikaze announcement/background rather than a revealing CD+G title card.
                    _audience?.ClearKaraokeVisual();
                }
            }

            SearchStatus.Text = $"Kamikaze selected for {singer.SingerName} • press KAMIKAZE again for another random song";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            App.WriteDiagnostic("KAMIKAZE", ex.ToString());
            MessageBox.Show(ex.Message, "Kamikaze Karaoke", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _kamikazePickInProgress = false;
            if (KamikazeButton is not null)
            {
                KamikazeButton.Content = "KAMIKAZE";
                KamikazeButton.IsEnabled = true;
            }
        }
    }

    private async Task<SongRecord?> PickRandomKaraokeSongAsync(string? excludePath, CancellationToken cancellationToken)
    {
        // IMPORTANT: do not use BrowseAsync with a random OFFSET here. OFFSET + ORDER BY Artist
        // can walk/sort a huge part of a multi-million-row library and previously froze WPF's UI.
        // The repository now jumps to a random indexed song ID and reads only a tiny candidate window.
        var candidates = await Task.Run(
            () => _library.GetRandomCandidatesAsync("Karaoke", 96, cancellationToken),
            cancellationToken);

        if (candidates.Count == 0) return null;
        SongRecord? fallback = null;
        var checkedCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(candidate.FilePath)) continue;
            if (!string.IsNullOrWhiteSpace(excludePath) && PathEqualsSafe(candidate.FilePath, excludePath)) continue;

            fallback ??= candidate;
            // File-system probes can stall on a disconnected network/USB path. Keep them off the UI
            // thread and bound the number of probes; LoadKaraokeAsync will provide the final validation.
            if (++checkedCount > 24) break;
            if (await FileExistsOffUiThreadAsync(candidate.FilePath, cancellationToken)) return candidate;
        }
        return fallback;
    }

    private static bool PathEqualsSafe(string a, string b)
    {
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }

    private static async Task<bool> FileExistsOffUiThreadAsync(string path, CancellationToken cancellationToken)
    {
        // A local File.Exists is normally instant, but a dead network share/removable drive can block.
        // A 350 ms budget keeps Kamikaze responsive and simply moves on to another indexed candidate.
        var probe = Task.Run(() => File.Exists(path));
        var timeout = Task.Delay(350, cancellationToken);
        var completed = await Task.WhenAny(probe, timeout);
        if (completed != probe)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
        return await probe;
    }

    private async Task KaraokeCompletedAsync()
    {
        CancelKaraokeStopFade();
        RestoreKaraokePlaybackVolume();
        _pitchAudio.Stop();
        _karaokePlaying = false;
        _karaokePaused = false;
        KaraokePauseButton.Content = "Ⅱ PAUSE";
        _karaokePresentationActive = false;
        _audience?.SetKaraokeActive(false);
        _audience?.ClearKaraokeVisual();

        var song = _activeSingerSong;
        if (song is not null)
        {
            try
            {
                // Singer history was written when PLAY started, so completion must not
                // create a duplicate row. Completion only persists the final per-track
                // key/CD+G preferences after any live adjustments made during the song.
                song.KeyChange = _keyChange;
                song.CdgSyncSeconds = _cdgTiming.OffsetSeconds;
                if (song.SongId is long songId)
                    await _library.SaveTrackPreferencesAsync(songId, song.KeyChange, song.CdgSyncSeconds, _lifetime.Token);
            }
            catch (Exception ex)
            {
                SearchStatus.Text = "Track preference save warning: " + ex.Message;
            }

            _activeSingerSongCheckedOut = false;
        }

        _activeSinger = null;
        _activeSingerSong = null;
        _activeSingerSongCheckedOut = false;
        UpdateAudienceNext();
        ResumeMusicAfterKaraoke();
    }

    private void KaraokeVisualTimer_Tick()
    {
        if (_karaokePlaying && _pitchAudio.IsLoaded && (DateTime.UtcNow - _lastPitchSyncUtc).TotalSeconds >= 2.0)
        {
            _lastPitchSyncUtc = DateTime.UtcNow;
            _pitchAudio.SyncTo(KaraokeMedia.Position);
        }

        if (_karaokePackage?.Kind == KaraokePackageKind.CdgPair)
        {
            RenderCdgAtCurrentPosition();
            return;
        }

        if (_karaokePackage?.Kind == KaraokePackageKind.Video && _karaokePlaying && _audience is not null)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastAudienceVideoSyncUtc).TotalSeconds >= 2)
            {
                _lastAudienceVideoSyncUtc = now;
                _audience.SyncVideo(KaraokeMedia.Position);
            }
        }
    }

    private void RenderCdgAtCurrentPosition(bool force = false)
    {
        if (_cdgDecoder is null || _cdgBitmap is null) return;
        var graphicsTime = _cdgTiming.GetGraphicsTime(KaraokeMedia.Position);
        _cdgDecoder.Seek(graphicsTime);
        if (!force && _cdgDecoder.FrameVersion == _lastRenderedCdgVersion) return;

        _cdgDecoder.CopyBgra32(_cdgFrameBuffer);
        _cdgBitmap.WritePixels(
            new Int32Rect(0, 0, CdgDecoder.Width, CdgDecoder.Height),
            _cdgFrameBuffer,
            CdgDecoder.Width * 4,
            0);
        _lastRenderedCdgVersion = _cdgDecoder.FrameVersion;
    }

    private void ApplyCurrentKaraokeVisualToAudience()
    {
        if (_audience is null) return;
        if (_karaokePackage?.Kind == KaraokePackageKind.CdgPair && _cdgBitmap is not null)
        {
            _audience.ShowCdg(_cdgBitmap);
        }
        else if (_karaokePackage?.Kind == KaraokePackageKind.Video)
        {
            _audience.LoadMutedVideo(_karaokePackage.PlaybackPath);
            if (_karaokePlaying) _audience.PlayVideo(KaraokeMedia.Position);
        }
        else
        {
            _audience.ClearKaraokeVisual();
        }
    }

    private void DeckAAdd_Click(object sender, RoutedEventArgs e) => AddMusic(DeckAPlaylist);
    private void DeckBAdd_Click(object sender, RoutedEventArgs e) => AddMusic(DeckBPlaylist);

    private void AddMusic(ListBox list)
    {
        var d = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Music / video|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac;*.ogg;*.aif;*.aiff;*.mp4;*.mkv;*.avi;*.wmv;*.mov;*.mpeg;*.mpg;*.m4v;*.vob;*.ts;*.m2ts;*.webm;*.divx|All files|*.*"
        };
        if (d.ShowDialog() != true) return;
        foreach (var f in d.FileNames) list.Items.Add(CreateMusicQueueItem(f));
        RenumberPlaylist(list);
        if (list.SelectedIndex < 0 && list.Items.Count > 0) list.SelectedIndex = 0;
    }

    private void DeckAPlay_Click(object sender, RoutedEventArgs e) => StartManualMusicDeck(MusicDeckId.Deck1);
    private void DeckBPlay_Click(object sender, RoutedEventArgs e) => StartManualMusicDeck(MusicDeckId.Deck2);

    private void DeckAShuffle_Click(object sender, RoutedEventArgs e) => ShuffleMusicDeck(MusicDeckId.Deck1);
    private void DeckBShuffle_Click(object sender, RoutedEventArgs e) => ShuffleMusicDeck(MusicDeckId.Deck2);

    private void ShuffleMusicDeck(MusicDeckId deck)
    {
        var list = PlaylistFor(deck);
        if (list.Items.Count < 2)
        {
            UpdateMusicAutomationStatus($"{DeckName(deck)} needs at least two tracks to shuffle");
            return;
        }

        var current = CurrentMusicItemFor(deck);
        var queued = list.Items.OfType<MusicQueueItem>()
            .Where(item => current is null || !ReferenceEquals(item, current))
            .ToList();
        if (queued.Count < 2)
        {
            UpdateMusicAutomationStatus($"{DeckName(deck)} has fewer than two queued tracks to shuffle");
            return;
        }

        for (var i = queued.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (queued[i], queued[j]) = (queued[j], queued[i]);
        }

        list.Items.Clear();
        if (current is not null) list.Items.Add(current);
        foreach (var item in queued) list.Items.Add(item);
        RenumberPlaylist(list);
        RecalculateMusicDeckOrder(deck);
        list.SelectedIndex = current is not null ? 0 : (list.Items.Count > 0 ? 0 : -1);
        UpdateMusicAutomationStatus($"{DeckName(deck)} shuffled • {queued.Count:N0} queued track(s) randomised");
    }

    private void StartManualMusicDeck(MusicDeckId deck)
    {
        if (_singleDeckMode && deck == MusicDeckId.Deck2)
        {
            UpdateMusicAutomationStatus("Deck 2 is a side list • drag tracks into Deck 1 to play them");
            return;
        }
        if (_karaokePresentationActive)
        {
            KaraokeStopForMusicResume();
        }
        if (IsDeckPaused(deck) && CurrentMusicItemFor(deck) is not null)
        {
            ResumePausedMusicDeck(deck);
            return;
        }

        CancelMusicTransitions();
        _musicSuspendedForKaraoke = false;
        _resumeMusicDeck = MusicDeckId.None;
        _resumeMusicIndex = -1;
        _resumeMusicItem = null;

        var list = PlaylistFor(deck);
        if (list.Items.Count == 0) return;
        var index = list.SelectedIndex >= 0 ? list.SelectedIndex : GetScheduledIndex(deck);
        if (index < 0) return;

        var other = Opposite(deck);
        StopDeck(other);
        if (StartDeckAt(deck, index, 1.0, makeActive: true))
            UpdateMusicAutomationStatus($"{DeckName(deck)} playing");
    }

    private bool StartDeckAt(MusicDeckId deck, int index, double fadeFactor, bool makeActive)
    {
        if (_singleDeckMode && deck == MusicDeckId.Deck2) return false;
        var list = PlaylistFor(deck);
        if (list.Items.Count == 0) return false;
        index = Math.Clamp(index, 0, list.Items.Count - 1);

        // Treat each music playlist as a live queue: the track that starts becomes
        // position #1, and the remaining order follows it. This keeps #1 meaningful
        // as NOW PLAYING while still allowing the user to drag the queue into any order.
        RotatePlaylistToIndex(list, index);
        index = 0;
        if (list.Items[index] is not MusicQueueItem item || string.IsNullOrWhiteSpace(item.FilePath)) return false;

        var media = MediaFor(deck);
        if (!TryLoadStandardMedia(media, item.FilePath)) return false;

        list.SelectedIndex = index;
        SetCurrentMusicItem(deck, item);
        // The started track is no longer an "unplayed" queue item, so persist the
        // remaining queue immediately even when it happened to already be row #1.
        MarkMusicDeckQueuesDirty();
        MarkMusicTrackPlayedThisSession(item.FilePath);
        TitleFor(deck).Text = string.IsNullOrWhiteSpace(item.Artist) ? item.DisplayTitle : $"{item.DisplayArtist} — {item.DisplayTitle}";
        SetCurrentIndexAndAdvance(deck, index);
        SetDeckPaused(deck, false);
        SetDeckFadeFactor(deck, fadeFactor);
        media.Play();
        if (MediaFileClassifier.Classify(item.FilePath) == HazzMediaKind.Video)
        {
            _audienceMusicVideoDeck = deck;
            _audience?.ShowMusicVideo(item.FilePath, TimeSpan.Zero, playing: true);
        }
        else if (makeActive)
        {
            _audienceMusicVideoDeck = MusicDeckId.None;
            _audience?.ClearMusicVideo();
        }
        _ = RecordMusicPlaySafeAsync(deck, item);
        if (makeActive) _activeMusicDeck = deck;
        return true;
    }

    private static bool TryLoadStandardMedia(MediaElement media, string path)
    {
        var kind = MediaFileClassifier.Classify(path);
        if (kind is HazzMediaKind.ZipKaraoke or HazzMediaKind.CdgGraphics)
        {
            MessageBox.Show("Use the Karaoke Deck for CD+G or ZIP karaoke packages.", "Hazz Karaoke Hoster");
            return false;
        }
        if (kind == HazzMediaKind.Unknown)
        {
            MessageBox.Show("That file type is not recognised by Hazz Karaoke Hoster.", "Hazz Karaoke Hoster");
            return false;
        }

        media.Stop();
        media.Source = new Uri(path);
        return true;
    }

    private void DeckAPause_Click(object sender, RoutedEventArgs e) => PauseDeck(MusicDeckId.Deck1);
    private void DeckAStop_Click(object sender, RoutedEventArgs e) => StopDeckFromButton(MusicDeckId.Deck1);
    private void DeckBPause_Click(object sender, RoutedEventArgs e) => PauseDeck(MusicDeckId.Deck2);
    private void DeckBStop_Click(object sender, RoutedEventArgs e) => StopDeckFromButton(MusicDeckId.Deck2);

    private void PauseDeck(MusicDeckId deck)
    {
        var current = CurrentMusicItemFor(deck);
        if (current is null)
        {
            UpdateMusicAutomationStatus($"{DeckName(deck)} has no playing track to pause");
            return;
        }

        if (IsDeckPaused(deck))
        {
            ResumePausedMusicDeck(deck);
            return;
        }

        var media = MediaFor(deck);
        SetPausedPosition(deck, media.Position);
        media.Pause();
        SetDeckPaused(deck, true);
        if (_audienceMusicVideoDeck == deck) _audience?.PauseMusicVideo();
        UpdateMusicAutomationStatus($"{DeckName(deck)} paused at {FormatClock(PausedPositionFor(deck))} • press Pause or Play to resume");
    }

    private void ResumePausedMusicDeck(MusicDeckId deck)
    {
        var media = MediaFor(deck);
        var resumeAt = PausedPositionFor(deck);
        media.Play();
        if (resumeAt > TimeSpan.Zero) media.Position = resumeAt;
        SetDeckPaused(deck, false);
        if (CurrentMusicItemFor(deck) is { } item && MediaFileClassifier.Classify(item.FilePath) == HazzMediaKind.Video)
        {
            _audienceMusicVideoDeck = deck;
            _audience?.ShowMusicVideo(item.FilePath, resumeAt, playing: true);
        }
        _activeMusicDeck = deck;
        UpdateMusicAutomationStatus($"{DeckName(deck)} resumed at {FormatClock(resumeAt)}");
    }

    private TimeSpan PausedPositionFor(MusicDeckId deck)
        => deck == MusicDeckId.Deck1 ? _deck1PausedPosition : _deck2PausedPosition;

    private void SetPausedPosition(MusicDeckId deck, TimeSpan position)
    {
        if (deck == MusicDeckId.Deck1) _deck1PausedPosition = position;
        else if (deck == MusicDeckId.Deck2) _deck2PausedPosition = position;
    }

    private static string FormatClock(TimeSpan time)
        => time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

    private void StopDeckFromButton(MusicDeckId deck)
    {
        var transitionWasActive = _crossfadeActive || _musicFadeInResume;
        var other = Opposite(deck);
        CancelMusicTransitions();

        // Once a music track has actually started it is consumed from the live
        // playlist. STOP must not leave a played track sitting in the queue.
        StopDeck(deck);
        RemovePlayedMusicItemFromPlaylist(deck);
        SetDeckFadeFactor(deck, 0.0);
        if (transitionWasActive)
        {
            StopDeck(other);
            RemovePlayedMusicItemFromPlaylist(other);
            SetDeckFadeFactor(other, 0.0);
        }
        if (_activeMusicDeck == deck || transitionWasActive) _activeMusicDeck = MusicDeckId.None;
        UpdateMusicAutomationStatus("Music stopped • played track removed from playlist");
    }

    private void DeckAVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DeckAMedia != null) DeckAMedia.Volume = e.NewValue * _deck1FadeFactor;
    }

    private void DeckBVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DeckBMedia != null) DeckBMedia.Volume = e.NewValue * _deck2FadeFactor;
    }

    private void CrossfadeSeconds_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CrossfadeSecondsText is not null) CrossfadeSecondsText.Text = $"{e.NewValue:0.0}s";
    }

    private double CrossfadeSeconds => Math.Max(0.5, CrossfadeSecondsSlider?.Value ?? 4.0);

    private void FadeNow_Click(object sender, RoutedEventArgs e)
    {
        if (_singleDeckMode)
        {
            UpdateMusicAutomationStatus("Fade Now is unavailable in Single Deck mode");
            return;
        }
        if (_karaokePresentationActive || _musicSuspendedForKaraoke)
        {
            UpdateMusicAutomationStatus("FADE NOW unavailable while karaoke owns the audience output");
            return;
        }
        if (_crossfadeActive)
        {
            UpdateMusicAutomationStatus("Crossfade already in progress");
            return;
        }
        if (_activeMusicDeck == MusicDeckId.None)
        {
            UpdateMusicAutomationStatus("Start Deck 1 or Deck 2 first, then press FADE NOW");
            return;
        }

        var to = Opposite(_activeMusicDeck);
        if (!HasTracks(to))
        {
            UpdateMusicAutomationStatus($"{DeckName(to)} has no queued music");
            return;
        }
        BeginCrossfade(_activeMusicDeck, to);
    }

    private void PlayMusic_Click(object sender, RoutedEventArgs e)
    {
        if (_quickSearchMusicActive || _quickSearchMusicFadeInActive)
            StopQuickSearchMusic(includeRegularDecks: true, updateStatus: false);

        if (!_musicSuspendedForKaraoke && !_karaokePresentationActive && _activeMusicDeck != MusicDeckId.None)
            return;

        if (_karaokePresentationActive || KaraokeMedia.Position > TimeSpan.Zero)
            KaraokeStopForMusicResume();
        ResumeMusicAfterKaraoke(forceStart: true);
    }

    private void KaraokeStopForMusicResume(bool cancelPendingFade = true)
    {
        if (cancelPendingFade) CancelKaraokeStopFade();
        KaraokeMedia.Stop();
        _pitchAudio.Stop();
        _karaokePlaying = false;
        _karaokePaused = false;
        KaraokePauseButton.Content = "Ⅱ PAUSE";
        _karaokePresentationActive = false;
        _audience?.SetKaraokeActive(false);
        _audience?.ClearKaraokeVisual();
        if (_cdgDecoder is not null)
        {
            _cdgDecoder.Reset();
            _lastRenderedCdgVersion = -1;
        }

        // Karaoke can be stopped before the automatic music fade-out has completed.
        // Consume any music tracks that had already started, exactly as the completed
        // fade-out path does, so Hazz never resumes/replays the interrupted track.
        FinalizeMusicSuspensionBeforeResume();

        ClearStartedSingerSongReference();
        UpdatePlayerTimeDisplays(force: true);
        UpdateAudienceNext();
    }

    private void FinalizeMusicSuspensionBeforeResume()
    {
        _musicFadeOutForKaraoke = false;
        _crossfadeActive = false;
        _crossfadeFrom = MusicDeckId.None;
        _crossfadeTo = MusicDeckId.None;

        foreach (var deck in new[] { MusicDeckId.Deck1, MusicDeckId.Deck2 })
        {
            if (CurrentMusicItemFor(deck) is null) continue;
            StopDeck(deck);
            RemovePlayedMusicItemFromPlaylist(deck);
        }

        SetDeckFadeFactor(MusicDeckId.Deck1, 0.0);
        SetDeckFadeFactor(MusicDeckId.Deck2, 0.0);
        _activeMusicDeck = MusicDeckId.None;
    }

    private void MusicAutomationTimer_Tick()
    {
        UpdatePlayerTimeDisplays();
        UpdateDeckLedDisplays();
        UpdateIlluminatedButtons();
        if (_quickSearchMusicVideo && _quickSearchMusicActive && !_karaokePresentationActive)
            _audience?.SyncMusicVideo(QuickMusicMedia.Position);
        else if (_audienceMusicVideoDeck != MusicDeckId.None && !_karaokePresentationActive && !IsDeckPaused(_audienceMusicVideoDeck))
            _audience?.SyncMusicVideo(MediaFor(_audienceMusicVideoDeck).Position);
        if (_karaokeOnlyMode) return;
        if (_quickSearchMusicFadeInActive)
        {
            UpdateQuickSearchMusicFade();
            return;
        }
        if (_quickSearchMusicActive) return;
        if (_musicFadeOutForKaraoke)
        {
            UpdateMusicFadeOutForKaraoke();
            return;
        }

        if (_musicFadeInResume)
        {
            UpdateMusicFadeInResume();
            return;
        }

        if (_crossfadeActive)
        {
            UpdateCrossfade();
            return;
        }

        if (_musicSuspendedForKaraoke || AutoCrossfadeCheck?.IsChecked != true || _activeMusicDeck == MusicDeckId.None || IsDeckPaused(_activeMusicDeck))
            return;

        TryBeginAutomaticCrossfade();
    }

    private void UpdateDeckLedDisplays()
    {
        var now = DateTime.UtcNow;
        var elapsed = Math.Clamp((now - _lastLedUpdateUtc).TotalSeconds, 0, 0.2);
        _lastLedUpdateUtc = now;
        UpdateDeckLed(MusicDeckId.Deck1, DeckALedViewport, DeckALedText, DeckALedTransform, ref _deckALedX, ref _deckALedMessage, elapsed);
        UpdateDeckLed(MusicDeckId.Deck2, DeckBLedViewport, DeckBLedText, DeckBLedTransform, ref _deckBLedX, ref _deckBLedMessage, elapsed);
    }

    private void UpdateDeckLed(
        MusicDeckId deck,
        FrameworkElement viewport,
        TextBlock textBlock,
        TranslateTransform transform,
        ref double position,
        ref string previousMessage,
        double elapsed)
    {
        var item = CurrentMusicItemFor(deck);
        var deckNumber = deck == MusicDeckId.Deck1 ? 1 : 2;
        string message;
        if (item is null)
        {
            message = $"DECK {deckNumber} READY  •  LOAD MUSIC";
        }
        else
        {
            var state = IsDeckPaused(deck)
                ? "PAUSED"
                : (deck == MusicDeckId.Deck1 ? _deck1FadeFactor : _deck2FadeFactor) > 0.001
                    ? "NOW PLAYING"
                    : "CUED";
            message = $"{state}  •  {item.DisplayArtist}  —  {item.DisplayTitle}";
        }

        if (!string.Equals(message, previousMessage, StringComparison.Ordinal))
        {
            previousMessage = message;
            textBlock.Text = message + "     ◆     ";
            textBlock.Measure(new Size(double.PositiveInfinity, viewport.ActualHeight > 0 ? viewport.ActualHeight : 48));
            position = Math.Max(0, viewport.ActualWidth);
        }

        if (double.IsNaN(position)) position = Math.Max(0, viewport.ActualWidth);
        position -= 68 * elapsed;
        var textWidth = Math.Max(textBlock.ActualWidth, textBlock.DesiredSize.Width);
        if (position < -textWidth) position = Math.Max(0, viewport.ActualWidth);
        transform.X = position;
    }

    private void UpdateIlluminatedButtons()
    {
        var deck1Playing = _deck1CurrentItem is not null && !_deck1Paused && _deck1FadeFactor > 0.001;
        var deck2Playing = _deck2CurrentItem is not null && !_deck2Paused && _deck2FadeFactor > 0.001;
        SetButtonActive(DeckAPlayButton, deck1Playing);
        SetButtonActive(DeckAPauseButton, _deck1CurrentItem is not null && _deck1Paused);
        SetButtonActive(DeckBPlayButton, deck2Playing);
        SetButtonActive(DeckBPauseButton, _deck2CurrentItem is not null && _deck2Paused);
        SetButtonActive(KaraokePlayButton, _karaokePlaying);
        SetButtonActive(KaraokePauseButton, _karaokePaused);
        SetButtonActive(TvDisplay2Button, _audience?.IsVisible == true);
        SetButtonActive(KamikazeButton, _kamikazePickInProgress);
        SetButtonActive(PlayMusicButton, (deck1Playing || deck2Playing) && !_karaokePresentationActive);
    }

    private static void SetButtonActive(Button? button, bool active)
    {
        if (button is null) return;
        var value = active ? "Active" : null;
        if (!Equals(button.Tag, value)) button.Tag = value;
    }

    private void TryBeginAutomaticCrossfade()
    {
        if (_singleDeckMode) return;
        var from = _activeMusicDeck;
        if (IsDeckPaused(from)) return;
        var media = MediaFor(from);
        if (!media.NaturalDuration.HasTimeSpan) return;

        var duration = media.NaturalDuration.TimeSpan;
        if (duration <= TimeSpan.Zero) return;
        var remaining = duration - media.Position;
        if (remaining.TotalSeconds <= 0 || remaining.TotalSeconds > CrossfadeSeconds) return;

        var to = Opposite(from);
        if (!HasTracks(to)) return;
        BeginCrossfade(from, to);
    }

    private void BeginCrossfade(MusicDeckId from, MusicDeckId to)
    {
        if (_singleDeckMode) return;
        if (_crossfadeActive || from == MusicDeckId.None || to == MusicDeckId.None) return;
        var index = GetScheduledIndex(to);
        if (index < 0 || !StartDeckAt(to, index, 0.0, makeActive: false)) return;

        _crossfadeFrom = from;
        _crossfadeTo = to;
        _crossfadeActive = true;
        _musicTransitionStartedUtc = DateTime.UtcNow;
        SetDeckFadeFactor(from, 1.0);
        SetDeckFadeFactor(to, 0.0);
        UpdateMusicAutomationStatus($"Crossfading {DeckName(from)} → {DeckName(to)}");
    }

    private void UpdateCrossfade()
    {
        var progress = TransitionProgress();
        SetDeckFadeFactor(_crossfadeFrom, 1.0 - progress);
        SetDeckFadeFactor(_crossfadeTo, progress);
        if (progress < 1.0) return;
        CompleteCrossfade();
    }

    private void CompleteCrossfade()
    {
        var from = _crossfadeFrom;
        var to = _crossfadeTo;
        StopDeck(from);
        AdvanceCompletedMusicItem(from);
        SetDeckFadeFactor(from, 0.0);
        SetDeckFadeFactor(to, 1.0);
        _activeMusicDeck = to;
        _crossfadeActive = false;
        _crossfadeFrom = MusicDeckId.None;
        _crossfadeTo = MusicDeckId.None;
        UpdateMusicAutomationStatus($"{DeckName(to)} playing • next {DeckName(Opposite(to))}");
    }

    private void HandleMusicDeckEnded(MusicDeckId deck)
    {
        if (_musicSuspendedForKaraoke || _musicFadeOutForKaraoke) return;

        if (_crossfadeActive)
        {
            if (deck == _crossfadeFrom) CompleteCrossfade();
            return;
        }

        if (_activeMusicDeck != deck) return;
        ClearMusicVideoForDeck(deck);
        SetDeckFadeFactor(deck, 0.0);
        AdvanceCompletedMusicItem(deck);

        if (_singleDeckMode && deck == MusicDeckId.Deck1)
        {
            var nextIndex = GetScheduledIndex(MusicDeckId.Deck1);
            if (nextIndex >= 0 && StartDeckAt(MusicDeckId.Deck1, nextIndex, 1.0, makeActive: true))
            {
                UpdateMusicAutomationStatus("Deck 1 playing next queued track");
                return;
            }
            _activeMusicDeck = MusicDeckId.None;
            UpdateMusicAutomationStatus("Deck 1 playlist finished");
            return;
        }

        if (AutoCrossfadeCheck?.IsChecked == true)
        {
            var nextDeck = HasTracks(Opposite(deck)) ? Opposite(deck) : deck;
            var index = GetScheduledIndex(nextDeck);
            if (index >= 0 && StartDeckAt(nextDeck, index, 1.0, makeActive: true))
            {
                UpdateMusicAutomationStatus($"{DeckName(nextDeck)} playing");
                return;
            }
        }

        _activeMusicDeck = MusicDeckId.None;
        UpdateMusicAutomationStatus("Music finished");
    }

    private void HandleMusicDeckFailed(MusicDeckId deck, Exception? error)
    {
        if (_musicSuspendedForKaraoke) return;
        ClearMusicVideoForDeck(deck);
        BrokenMediaRegistry.Mark((deck == MusicDeckId.Deck1 ? _deck1CurrentItem : _deck2CurrentItem)?.FilePath, error?.Message ?? "Music playback failed");
        UpdateMusicAutomationStatus($"{DeckName(deck)} playback error");
        MessageBox.Show(error?.Message ?? $"{DeckName(deck)} could not play that file with the installed Windows codecs.",
            "Music Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);

        if (_crossfadeActive && deck == _crossfadeTo)
        {
            StopDeck(_crossfadeTo);
            RemovePlayedMusicItemFromPlaylist(_crossfadeTo);
            SetDeckFadeFactor(_crossfadeTo, 0.0);
            SetDeckFadeFactor(_crossfadeFrom, 1.0);
            _activeMusicDeck = _crossfadeFrom;
            _crossfadeActive = false;
            _crossfadeFrom = MusicDeckId.None;
            _crossfadeTo = MusicDeckId.None;
            UpdateMusicAutomationStatus($"Crossfade cancelled • {DeckName(_activeMusicDeck)} continues");
            return;
        }

        HandleMusicDeckEnded(deck);
    }

    private void BeginMusicFadeForKaraoke()
    {
        if (_karaokeOnlyMode) return;
        if (_musicSuspendedForKaraoke) return;

        CaptureMusicResumeTarget();
        _musicSuspendedForKaraoke = true;
        _musicFadeInResume = false;
        _musicFadeOutForKaraoke = _activeMusicDeck != MusicDeckId.None || _crossfadeActive;
        _fadeOutStartDeck1 = _deck1FadeFactor;
        _fadeOutStartDeck2 = _deck2FadeFactor;
        _musicTransitionStartedUtc = DateTime.UtcNow;
        _crossfadeActive = false;
        _crossfadeFrom = MusicDeckId.None;
        _crossfadeTo = MusicDeckId.None;

        if (_musicFadeOutForKaraoke)
            UpdateMusicAutomationStatus("Karaoke started • fading music");
        else
            UpdateMusicAutomationStatus("Karaoke playing • next music remembered");
    }

    private void CaptureMusicResumeTarget()
    {
        _resumeMusicDeck = MusicDeckId.None;
        _resumeMusicIndex = -1;
        _resumeMusicItem = null;

        // During an active crossfade both current tracks have already started and
        // will therefore be consumed when karaoke takes over. Resume from the next
        // still-unplayed item on the incoming deck instead of replaying either one.
        if (_crossfadeActive && _crossfadeTo != MusicDeckId.None)
        {
            var incoming = _crossfadeTo;
            var next = GetScheduledItem(incoming);
            if (next is not null && !ReferenceEquals(next, CurrentMusicItemFor(incoming)))
            {
                _resumeMusicDeck = incoming;
                _resumeMusicItem = next;
                _resumeMusicIndex = PlaylistFor(incoming).Items.IndexOf(next);
                return;
            }
        }

        if (_activeMusicDeck != MusicDeckId.None)
        {
            var preferred = Opposite(_activeMusicDeck);
            if (HasTracks(preferred))
            {
                _resumeMusicDeck = preferred;
                _resumeMusicItem = GetScheduledItem(preferred);
                _resumeMusicIndex = _resumeMusicItem is null ? -1 : PlaylistFor(preferred).Items.IndexOf(_resumeMusicItem);
            }
            else
            {
                _resumeMusicDeck = _activeMusicDeck;
                _resumeMusicItem = GetScheduledItem(_activeMusicDeck);
                _resumeMusicIndex = _resumeMusicItem is null ? -1 : PlaylistFor(_activeMusicDeck).Items.IndexOf(_resumeMusicItem);
            }
            return;
        }

        if (HasTracks(MusicDeckId.Deck1))
        {
            _resumeMusicDeck = MusicDeckId.Deck1;
            _resumeMusicItem = GetScheduledItem(MusicDeckId.Deck1);
            _resumeMusicIndex = _resumeMusicItem is null ? -1 : PlaylistFor(MusicDeckId.Deck1).Items.IndexOf(_resumeMusicItem);
        }
        else if (HasTracks(MusicDeckId.Deck2))
        {
            _resumeMusicDeck = MusicDeckId.Deck2;
            _resumeMusicItem = GetScheduledItem(MusicDeckId.Deck2);
            _resumeMusicIndex = _resumeMusicItem is null ? -1 : PlaylistFor(MusicDeckId.Deck2).Items.IndexOf(_resumeMusicItem);
        }
    }

    private void UpdateMusicFadeOutForKaraoke()
    {
        var progress = TransitionProgress();
        SetDeckFadeFactor(MusicDeckId.Deck1, _fadeOutStartDeck1 * (1.0 - progress));
        SetDeckFadeFactor(MusicDeckId.Deck2, _fadeOutStartDeck2 * (1.0 - progress));
        if (progress < 1.0) return;

        StopDeck(MusicDeckId.Deck1);
        StopDeck(MusicDeckId.Deck2);
        RemovePlayedMusicItemFromPlaylist(MusicDeckId.Deck1);
        RemovePlayedMusicItemFromPlaylist(MusicDeckId.Deck2);
        SetDeckFadeFactor(MusicDeckId.Deck1, 0.0);
        SetDeckFadeFactor(MusicDeckId.Deck2, 0.0);
        _activeMusicDeck = MusicDeckId.None;
        _musicFadeOutForKaraoke = false;
        UpdateMusicAutomationStatus("Karaoke playing • music queued for return");
    }

    private void ResumeMusicAfterKaraoke(bool forceStart = false, double? fadeInSeconds = null)
    {
        if (_karaokeOnlyMode)
        {
            _musicSuspendedForKaraoke = false;
            _musicFadeOutForKaraoke = false;
            _musicFadeInResume = false;
            return;
        }
        if (!_musicSuspendedForKaraoke && !forceStart) return;
        if (_musicFadeInResume) return;

        _musicFadeOutForKaraoke = false;
        StopDeck(MusicDeckId.Deck1);
        StopDeck(MusicDeckId.Deck2);
        SetDeckFadeFactor(MusicDeckId.Deck1, 0.0);
        SetDeckFadeFactor(MusicDeckId.Deck2, 0.0);

        var deck = _resumeMusicDeck;
        var index = -1;
        if (deck != MusicDeckId.None && _resumeMusicItem is not null)
            index = PlaylistFor(deck).Items.IndexOf(_resumeMusicItem);
        if (index < 0) index = _resumeMusicIndex;
        if (deck == MusicDeckId.None || !HasTracks(deck) || index < 0 || index >= PlaylistFor(deck).Items.Count)
        {
            deck = HasTracks(MusicDeckId.Deck1)
                ? MusicDeckId.Deck1
                : !_singleDeckMode && HasTracks(MusicDeckId.Deck2) ? MusicDeckId.Deck2 : MusicDeckId.None;
            index = deck == MusicDeckId.None ? -1 : GetScheduledIndex(deck);
        }

        if (deck == MusicDeckId.None || index < 0)
        {
            _musicSuspendedForKaraoke = false;
            UpdateMusicAutomationStatus("No music queued");
            return;
        }

        if (!StartDeckAt(deck, index, 0.0, makeActive: true))
        {
            _musicSuspendedForKaraoke = false;
            return;
        }

        _musicSuspendedForKaraoke = false;
        _musicResumeFadeSeconds = Math.Max(0.1, fadeInSeconds ?? CrossfadeSeconds);
        _musicFadeInResume = true;
        _musicTransitionStartedUtc = DateTime.UtcNow;
        _resumeMusicDeck = MusicDeckId.None;
        _resumeMusicIndex = -1;
        _resumeMusicItem = null;
        UpdateMusicAutomationStatus($"Resuming with {DeckName(deck)} • {_musicResumeFadeSeconds:0.0}s fade in");
    }

    private void UpdateMusicFadeInResume()
    {
        var progress = Math.Clamp((DateTime.UtcNow - _musicTransitionStartedUtc).TotalSeconds / _musicResumeFadeSeconds, 0.0, 1.0);
        if (_activeMusicDeck != MusicDeckId.None) SetDeckFadeFactor(_activeMusicDeck, progress);
        if (progress < 1.0) return;
        _musicFadeInResume = false;
        UpdateMusicAutomationStatus($"{DeckName(_activeMusicDeck)} playing");
    }

    private double TransitionProgress()
        => Math.Clamp((DateTime.UtcNow - _musicTransitionStartedUtc).TotalSeconds / CrossfadeSeconds, 0.0, 1.0);

    private void CancelMusicTransitions()
    {
        _crossfadeActive = false;
        _musicFadeOutForKaraoke = false;
        _musicFadeInResume = false;
        _crossfadeFrom = MusicDeckId.None;
        _crossfadeTo = MusicDeckId.None;
    }

    private void StopDeck(MusicDeckId deck)
    {
        if (deck == MusicDeckId.None) return;
        MediaFor(deck).Stop();
        ClearMusicVideoForDeck(deck);
        var current = CurrentMusicItemFor(deck);
        if (current is not null) current.IsNowPlaying = false;
        SetDeckPaused(deck, false);
    }

    private void ApplyCurrentMusicVideoToAudience()
    {
        if (_audience is null) return;
        var deck = _audienceMusicVideoDeck != MusicDeckId.None ? _audienceMusicVideoDeck : _activeMusicDeck;
        var item = CurrentMusicItemFor(deck);
        if (deck != MusicDeckId.None && item is not null && MediaFileClassifier.Classify(item.FilePath) == HazzMediaKind.Video)
        {
            _audienceMusicVideoDeck = deck;
            _audience.ShowMusicVideo(item.FilePath, MediaFor(deck).Position, !IsDeckPaused(deck));
            return;
        }
        _audienceMusicVideoDeck = MusicDeckId.None;
        _audience.ClearMusicVideo();
    }

    private void ClearMusicVideoForDeck(MusicDeckId deck)
    {
        if (_audienceMusicVideoDeck != deck) return;
        _audienceMusicVideoDeck = MusicDeckId.None;
        _audience?.ClearMusicVideo();
    }

    private void SetDeckPaused(MusicDeckId deck, bool paused)
    {
        if (deck == MusicDeckId.Deck1)
        {
            _deck1Paused = paused;
            if (DeckAPauseButton is not null) DeckAPauseButton.Content = paused ? "▶ RESUME" : "Ⅱ PAUSE";
            if (!paused) _deck1PausedPosition = TimeSpan.Zero;
        }
        else if (deck == MusicDeckId.Deck2)
        {
            _deck2Paused = paused;
            if (DeckBPauseButton is not null) DeckBPauseButton.Content = paused ? "▶ RESUME" : "Ⅱ PAUSE";
            if (!paused) _deck2PausedPosition = TimeSpan.Zero;
        }
    }

    private bool IsDeckPaused(MusicDeckId deck)
        => deck == MusicDeckId.Deck1 ? _deck1Paused : deck == MusicDeckId.Deck2 && _deck2Paused;

    private void SetDeckFadeFactor(MusicDeckId deck, double factor)
    {
        factor = Math.Clamp(factor, 0.0, 1.0);
        if (deck == MusicDeckId.Deck1)
        {
            _deck1FadeFactor = factor;
            if (DeckAMedia is not null) DeckAMedia.Volume = DeckAVolume.Value * factor;
        }
        else if (deck == MusicDeckId.Deck2)
        {
            _deck2FadeFactor = factor;
            if (DeckBMedia is not null) DeckBMedia.Volume = DeckBVolume.Value * factor;
        }
        UpdateVisualCrossfader();
    }

    private void UpdateVisualCrossfader()
    {
        if (VisualCrossfaderSlider is null || CrossfaderPositionText is null || _updatingVisualCrossfader) return;

        var total = _deck1FadeFactor + _deck2FadeFactor;
        if (total <= 0.0001)
        {
            CrossfaderPositionText.Text = "MUSIC MUTED";
            return;
        }

        var deck1Share = _deck1FadeFactor / total;
        var deck2Share = _deck2FadeFactor / total;
        _updatingVisualCrossfader = true;
        try
        {
            VisualCrossfaderSlider.Value = deck2Share * 100.0;
            CrossfaderPositionText.Text = $"D1 {deck1Share * 100:0}%  •  D2 {deck2Share * 100:0}%";
        }
        finally
        {
            _updatingVisualCrossfader = false;
        }
    }

    private static void RotatePlaylistToIndex(ListBox list, int index)
    {
        if (list.Items.Count <= 1 || index <= 0 || index >= list.Items.Count)
        {
            RenumberPlaylist(list);
            return;
        }

        var leading = new List<object>(index);
        for (var i = 0; i < index; i++)
        {
            var item = list.Items[0]!;
            list.Items.RemoveAt(0);
            leading.Add(item);
        }
        foreach (var item in leading) list.Items.Add(item);
        RenumberPlaylist(list);
    }

    private void AdvanceCompletedMusicItem(MusicDeckId deck)
    {
        // Played tracks leave the live deck playlist permanently. They remain in
        // Music Play History and the session-played set, but are never moved to
        // the bottom of the live queue.
        RemovePlayedMusicItemFromPlaylist(deck);
    }

    private void RemovePlayedMusicItemFromPlaylist(MusicDeckId deck)
    {
        if (deck == MusicDeckId.None) return;
        var list = PlaylistFor(deck);
        var current = CurrentMusicItemFor(deck);
        if (current is not null)
        {
            current.IsNowPlaying = false;
            if (list.Items.Contains(current)) list.Items.Remove(current);
        }

        if (deck == MusicDeckId.Deck1)
        {
            _deck1CurrentItem = null;
            _deck1CurrentIndex = -1;
            _deck1NextIndex = 0;
        }
        else
        {
            _deck2CurrentItem = null;
            _deck2CurrentIndex = -1;
            _deck2NextIndex = 0;
        }

        RenumberPlaylist(list);
        if (list.Items.Count > 0) list.SelectedIndex = 0;
        else list.SelectedIndex = -1;
    }

    private MusicQueueItem CreateMusicQueueItem(SongRecord song)
    {
        var item = MusicQueueItem.FromSong(song);
        item.IsPlayedThisSession = _musicPlayedThisSession.Contains(item.FilePath);
        return item;
    }

    private MusicQueueItem CreateMusicQueueItem(string path)
    {
        var item = MusicQueueItem.FromPath(path);
        item.IsPlayedThisSession = _musicPlayedThisSession.Contains(item.FilePath);
        return item;
    }

    private void MarkMusicTrackPlayedThisSession(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _musicPlayedThisSession.Add(path);
        RefreshSessionPlayedColours();
    }

    private void RefreshSessionPlayedColours()
    {
        foreach (var list in new[] { DeckAPlaylist, DeckBPlaylist })
            foreach (var item in list.Items.OfType<MusicQueueItem>())
                item.IsPlayedThisSession = _musicPlayedThisSession.Contains(item.FilePath);
    }

    private MusicQueueItem? CurrentMusicItemFor(MusicDeckId deck)
        => deck == MusicDeckId.Deck1 ? _deck1CurrentItem : deck == MusicDeckId.Deck2 ? _deck2CurrentItem : null;

    private void SetCurrentMusicItem(MusicDeckId deck, MusicQueueItem item)
    {
        var previous = CurrentMusicItemFor(deck);
        if (previous is not null && !ReferenceEquals(previous, item)) previous.IsNowPlaying = false;
        item.IsNowPlaying = true;
        if (deck == MusicDeckId.Deck1) _deck1CurrentItem = item;
        else if (deck == MusicDeckId.Deck2) _deck2CurrentItem = item;
        RenumberPlaylist(PlaylistFor(deck));
    }

    private void UpdateOpenedMusicMetadata(MusicDeckId deck)
    {
        var item = CurrentMusicItemFor(deck);
        if (item is null) return;
        var duration = MediaDuration(MediaFor(deck));
        if (duration > TimeSpan.Zero) item.Duration = duration;
    }

    private async Task RecordMusicPlaySafeAsync(MusicDeckId deck, MusicQueueItem item)
    {
        try
        {
            // Allow MediaElement a moment to expose NaturalDuration so the history entry
            // can include it when the installed codec provides it.
            await Task.Delay(250, _lifetime.Token);
            var duration = item.Duration;
            if ((duration is null || duration <= TimeSpan.Zero) && ReferenceEquals(CurrentMusicItemFor(deck), item))
            {
                var mediaDuration = MediaDuration(MediaFor(deck));
                if (mediaDuration > TimeSpan.Zero)
                {
                    item.Duration = mediaDuration;
                    duration = mediaDuration;
                }
            }
            await _musicPlaylists.RecordMusicPlayAsync(
                DeckName(deck),
                Math.Max(1, item.Number),
                item.SongId,
                item.FilePath,
                item.Artist,
                item.DisplayTitle,
                DateTimeOffset.Now,
                duration?.TotalSeconds,
                _lifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => UpdateMusicAutomationStatus("Music history warning: " + ex.Message));
        }
    }

    private static void RenumberPlaylist(ListBox list)
    {
        for (var i = 0; i < list.Items.Count; i++)
            if (list.Items[i] is MusicQueueItem item) item.Number = i + 1;
    }

    private void RecalculateMusicDeckOrder(MusicDeckId deck)
    {
        var list = PlaylistFor(deck);
        RenumberPlaylist(list);
        var current = CurrentMusicItemFor(deck);
        var currentIndex = current is null ? -1 : list.Items.IndexOf(current);
        if (deck == MusicDeckId.Deck1)
        {
            _deck1CurrentIndex = currentIndex;
            _deck1NextIndex = list.Items.Count == 0 ? 0 : currentIndex >= 0 ? (currentIndex + 1) % list.Items.Count : 0;
        }
        else if (deck == MusicDeckId.Deck2)
        {
            _deck2CurrentIndex = currentIndex;
            _deck2NextIndex = list.Items.Count == 0 ? 0 : currentIndex >= 0 ? (currentIndex + 1) % list.Items.Count : 0;
        }
    }

    private void SetCurrentIndexAndAdvance(MusicDeckId deck, int index)
    {
        var count = PlaylistFor(deck).Items.Count;
        if (deck == MusicDeckId.Deck1)
        {
            _deck1CurrentIndex = index;
            _deck1NextIndex = count == 0 ? 0 : (index + 1) % count;
        }
        else
        {
            _deck2CurrentIndex = index;
            _deck2NextIndex = count == 0 ? 0 : (index + 1) % count;
        }
    }

    private int GetScheduledIndex(MusicDeckId deck)
    {
        var count = PlaylistFor(deck).Items.Count;
        if (count == 0) return -1;
        var index = deck == MusicDeckId.Deck1 ? _deck1NextIndex : _deck2NextIndex;
        return Math.Clamp(index, 0, count - 1);
    }

    private MusicQueueItem? GetScheduledItem(MusicDeckId deck)
    {
        var index = GetScheduledIndex(deck);
        return index >= 0 && index < PlaylistFor(deck).Items.Count
            ? PlaylistFor(deck).Items[index] as MusicQueueItem
            : null;
    }

    private int CurrentIndexFor(MusicDeckId deck)
        => deck == MusicDeckId.Deck1 ? _deck1CurrentIndex : deck == MusicDeckId.Deck2 ? _deck2CurrentIndex : -1;

    private bool HasTracks(MusicDeckId deck) => deck != MusicDeckId.None && PlaylistFor(deck).Items.Count > 0;
    private static MusicDeckId Opposite(MusicDeckId deck) => deck == MusicDeckId.Deck1 ? MusicDeckId.Deck2 : deck == MusicDeckId.Deck2 ? MusicDeckId.Deck1 : MusicDeckId.None;
    private string DeckName(MusicDeckId deck) => deck == MusicDeckId.Deck1 ? "Deck 1" : deck == MusicDeckId.Deck2 ? (_singleDeckMode ? "Side List" : "Deck 2") : "Music";

    private ListBox PlaylistFor(MusicDeckId deck) => deck == MusicDeckId.Deck1 ? DeckAPlaylist : DeckBPlaylist;
    private MediaElement MediaFor(MusicDeckId deck) => deck == MusicDeckId.Deck1 ? DeckAMedia : DeckBMedia;
    private TextBlock TitleFor(MusicDeckId deck) => deck == MusicDeckId.Deck1 ? DeckATitle : DeckBTitle;

    private void UpdateMusicAutomationStatus(string text)
    {
        if (MusicAutomationStatus is not null) MusicAutomationStatus.Text = text;
    }

    private async void RescanWatchedFolders_Click(object sender, RoutedEventArgs e)
    {
        if (_importCts is not null)
        {
            MessageBox.Show("A library import/rescan is already running.", "Library Rescan");
            return;
        }

        var roots = await _libraryRoots.GetRootsAsync(_lifetime.Token);
        var existing = roots.Where(x => Directory.Exists(x.Path)).ToArray();
        if (existing.Length == 0)
        {
            MessageBox.Show("No watched library folders are configured yet. Use IMPORT > Import Karaoke Folders or Import Music Folders first.",
                "Library Rescan", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var go = MessageBox.Show(
            $"Rescan {existing.Length:N0} watched folder(s) for new or changed tracks?\n\nThis runs in the background and updates existing records rather than rebuilding the Hazz database.",
            "Rescan Watched Folders", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (go != MessageBoxResult.Yes) return;

        _importCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        CancelImportButton.Visibility = Visibility.Visible;
        try
        {
            var token = _importCts.Token;
            long indexed = 0, scanned = 0, errors = 0;
            foreach (var group in existing.GroupBy(x => x.MediaKind, StringComparer.OrdinalIgnoreCase))
            {
                var mode = string.Equals(group.Key, "Music", StringComparison.OrdinalIgnoreCase) ? LibraryImportMode.Music
                    : string.Equals(group.Key, "Auto", StringComparison.OrdinalIgnoreCase) ? LibraryImportMode.Auto : LibraryImportMode.Karaoke;
                var rootPaths = group.Select(x => x.Path).ToArray();
                var progress = new Progress<LibraryImportProgress>(p =>
                    SearchStatus.Text = $"Rescanning {group.Key.ToLowerInvariant()} • {p.FilesScanned:N0} examined • {p.RecordsImported:N0} indexed/updated");
                var result = await Task.Run(async () =>
                    await _libraryImporter.ImportAsync(new LibraryImportOptions(rootPaths, mode, true), progress, token), token);
                indexed += result.RecordsImported;
                scanned += result.FilesScanned;
                errors += result.Errors;
            }
            await RefreshLibraryCountsAsync();
            await RefreshLibraryAutoWatchAsync();
            SearchStatus.Text = $"Rescan complete • {indexed:N0} indexed/updated";
            MessageBox.Show($"Watched-folder rescan complete.\n\nFiles examined: {scanned:N0}\nTracks indexed/updated: {indexed:N0}\nErrors: {errors:N0}",
                "Library Rescan", MessageBoxButton.OK, errors == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (OperationCanceledException) { SearchStatus.Text = "Library rescan cancelled"; }
        catch (Exception ex)
        {
            SearchStatus.Text = "Library rescan error";
            MessageBox.Show(ex.Message, "Library Rescan Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _importCts?.Dispose();
            _importCts = null;
            CancelImportButton.Visibility = Visibility.Collapsed;
        }
    }

    private async void ImportKarmaLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_importCts is not null)
        {
            MessageBox.Show("A library import is already running.", "Karma Library Import");
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title = "Select Karma karaoke database",
            Filter = "Karma database|*.kdb;*.mdb;*.accdb|All files|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        _importCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        CancelImportButton.Visibility = Visibility.Visible;
        try
        {
            var token = _importCts.Token;
            SearchStatus.Text = "Inspecting Karma database schema...";
            var inspection = await Task.Run(async () => await _karma.InspectLibraryAsync(dlg.FileName, token), token);
            if (inspection.SuggestedMapping is null)
            {
                var schema = string.Join("\n", inspection.TablesAndColumns.Take(16));
                var warnings = string.Join("\n", inspection.Warnings);
                MessageBox.Show($"Hazz could not safely identify the Karma media table. Nothing was imported.\n\n{warnings}\n\nDetected tables/columns:\n{schema}",
                    "Karma Database Inspector", MessageBoxButton.OK, MessageBoxImage.Warning);
                SearchStatus.Text = "Karma library mapping not recognised";
                return;
            }

            var m = inspection.SuggestedMapping;
            var preview = string.Join("\n", inspection.PreviewRows.Take(8).Select((r, i) =>
                $"{i + 1}. {r.Artist} - {r.Title}\n   {r.FilePath}"));
            var countText = inspection.EstimatedRows > 0 ? inspection.EstimatedRows.ToString("N0") : "unknown";
            var choice = MessageBox.Show(
                $"KARMA DATABASE INSPECTOR\n\nDetected media table: {m.TableName}\nFile/path column: {m.PathColumn}" +
                (string.IsNullOrWhiteSpace(m.FolderColumn) ? string.Empty : $"\nFolder column: {m.FolderColumn}") +
                $"\nArtist: {m.ArtistColumn ?? "filename fallback"}\nTitle: {m.TitleColumn ?? "filename fallback"}\nManufacturer: {m.ManufacturerColumn ?? "filename fallback"}\nDisc ID: {m.DiscIdColumn ?? "filename fallback"}\nApprox. rows: {countText}\n\nSample:\n{preview}\n\n" +
                "YES = FAST IMPORT using Karma's stored paths (recommended; no full HDD check)\nNO = IMPORT + verify every path exists (slower)\nCANCEL = do nothing\n\nThe Karma database remains READ-ONLY.",
                "Import Complete Karma Karaoke Library", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel) return;
            var verify = choice == MessageBoxResult.No;

            var progress = new Progress<KarmaLibraryImportProgress>(p =>
            {
                SearchStatus.Text = $"Karma library • {p.RowsRead:N0} read • {p.RecordsImported:N0} indexed" + (verify ? $" • {p.MissingFiles:N0} missing" : string.Empty);
                if (!string.IsNullOrWhiteSpace(p.CurrentPath)) LibraryCountText.ToolTip = p.CurrentPath;
            });

            var result = await Task.Run(async () =>
                await _karma.ImportLibraryAsync(dlg.FileName, m, verify, progress, token), token);
            await RefreshLibraryCountsAsync();
            await RefreshLibraryAutoWatchAsync();
            SearchStatus.Text = $"Karma library import complete • {result.RecordsImported:N0} tracks";
            var roots = result.WatchRootsAdded.Count == 0 ? "None detected" : string.Join("\n", result.WatchRootsAdded);
            var warningText = result.Warnings.Count == 0 ? string.Empty : $"\n\nNotes:\n{string.Join("\n", result.Warnings.Take(10))}";
            MessageBox.Show(
                $"Karma karaoke-library import complete.\n\nRows read: {result.RowsRead:N0}\nTracks indexed/updated: {result.RecordsImported:N0}" +
                (verify ? $"\nMissing paths: {result.MissingFiles:N0}" : string.Empty) +
                $"\nErrors: {result.Errors:N0}\n\nAuto-watch roots added:\n{roots}{warningText}",
                "Karma Library Import", MessageBoxButton.OK, result.Errors == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (OperationCanceledException) { SearchStatus.Text = "Karma library import cancelled"; }
        catch (Exception ex)
        {
            SearchStatus.Text = "Karma library import error";
            MessageBox.Show(ex.Message, "Karma Library Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _importCts?.Dispose();
            _importCts = null;
            CancelImportButton.Visibility = Visibility.Collapsed;
            LibraryCountText.ToolTip = null;
        }
    }

    private async void ImportKaraoke_Click(object sender, RoutedEventArgs e)
        => await ImportLibraryAsync(LibraryImportMode.Karaoke);

    private async void ImportMusic_Click(object sender, RoutedEventArgs e)
        => await ImportLibraryAsync(LibraryImportMode.Music);

    private void CancelImport_Click(object sender, RoutedEventArgs e) => _importCts?.Cancel();

    private async Task ImportLibraryAsync(LibraryImportMode mode)
    {
        if (_importCts is not null)
        {
            MessageBox.Show("A library import is already running.", "Library Import");
            return;
        }

        var picker = new LibrarySourcesWindow(
            mode == LibraryImportMode.Karaoke ? "Import Karaoke Folders" : "Import Music Folders")
        {
            Owner = this
        };
        if (picker.ShowDialog() != true || picker.SelectedFolders.Count == 0) return;

        _importCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        CancelImportButton.Visibility = Visibility.Visible;
        SearchStatus.Text = $"Starting {mode.ToString().ToLowerInvariant()} import from {picker.SelectedFolders.Count} folder(s)...";
        var progress = new Progress<LibraryImportProgress>(p =>
        {
            var root = p.RootCount > 0 ? $" • folder {p.CurrentRoot}/{p.RootCount}" : string.Empty;
            SearchStatus.Text = $"Scanning {p.FilesScanned:N0} • indexed {p.RecordsImported:N0} • companion audio {p.CompanionAudioIgnored:N0} • unsupported {p.Unsupported:N0}{root}";
            if (!string.IsNullOrWhiteSpace(p.CurrentPath)) LibraryCountText.ToolTip = p.CurrentPath;
        });

        try
        {
            var options = new LibraryImportOptions(picker.SelectedFolders, mode, IncludeSubfolders: true);
            var token = _importCts.Token;
            var result = await Task.Run(async () => await _libraryImporter.ImportAsync(options, progress, token), token);
            await _libraryRoots.UpsertRootsAsync(picker.SelectedFolders, mode == LibraryImportMode.Music ? "Music" : "Karaoke", true, token);
            await RefreshLibraryAutoWatchAsync();
            await RefreshLibraryCountsAsync();
            SearchStatus.Text = $"Import complete • {result.RecordsImported:N0} indexed in {result.Elapsed:g}";
            var warningText = result.Warnings.Count == 0 ? string.Empty : $"\n\nWarnings (first {Math.Min(result.Warnings.Count, 12)}):\n{string.Join("\n", result.Warnings.Take(12))}";
            MessageBox.Show(
                $"{mode} import complete.\n\nFolders processed: {result.RootsProcessed:N0}\nFiles examined: {result.FilesScanned:N0}\nTracks indexed/updated: {result.RecordsImported:N0}\nCompanion MP3/audio ignored: {result.CompanionAudioIgnored:N0}\nUnsupported/non-library files: {result.Unsupported:N0}\nErrors: {result.Errors:N0}\nElapsed: {result.Elapsed:g}{warningText}",
                "Hazz Library Import", MessageBoxButton.OK,
                result.Errors == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (OperationCanceledException)
        {
            SearchStatus.Text = "Library import cancelled";
        }
        catch (Exception ex)
        {
            SearchStatus.Text = "Import error";
            MessageBox.Show(ex.Message, "Library Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _importCts?.Dispose();
            _importCts = null;
            CancelImportButton.Visibility = Visibility.Collapsed;
            LibraryCountText.ToolTip = null;
        }
    }

    private void OpenDatabaseFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.GetDirectoryName(_db.DatabasePath);
            if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Open Database Folder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BackupDatabase_Click(object sender, RoutedEventArgs e)
    {
        var folder = Path.GetDirectoryName(_db.DatabasePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dlg = new SaveFileDialog
        {
            Title = "Back up Hazz Karaoke Hoster database",
            Filter = "Hazz database backup|*.db|All files|*.*",
            FileName = $"hazz-hoster-backup-{DateTime.Now:yyyy-MM-dd-HHmm}.db",
            InitialDirectory = Directory.Exists(folder) ? folder : null,
            AddExtension = true,
            DefaultExt = ".db"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            SearchStatus.Text = "Backing up Hazz database...";
            await _db.BackupAsync(dlg.FileName, _lifetime.Token);
            SearchStatus.Text = "Database backup complete";
            MessageBox.Show($"Backup created successfully:\n\n{dlg.FileName}", "Hazz Database Backup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SearchStatus.Text = "Database backup failed";
            MessageBox.Show(ex.Message, "Hazz Database Backup", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ImportVirtualDj_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select VirtualDJ database.xml",
            Filter = "VirtualDJ database.xml|database.xml;*.xml|XML files|*.xml|All files|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;
        await ImportExternalSourceAsync(dlg.FileName);
    }

    private async void ImportOtherSoftware_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Smart Import — select an application database, playlist or export",
            Filter = "Smart Import files|*.xml;*.vdjfolder;*.json;*.m3u;*.m3u8;*.pls;*.lst;*.kpl;*.wpl;*.xspf;*.asx;*.csv;*.tsv;*.txt;*.db;*.db3;*.sqlite;*.sqlite3;*.s3db;*.sqlitedb;*.musicdb;*.mdb;*.accdb;*.kdb|Database files|*.db;*.db3;*.sqlite;*.sqlite3;*.s3db;*.sqlitedb;*.musicdb;*.mdb;*.accdb;*.kdb|Exports and playlists|*.xml;*.vdjfolder;*.json;*.csv;*.tsv;*.txt;*.m3u;*.m3u8;*.pls;*.lst;*.kpl;*.wpl;*.xspf;*.asx|All files|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;
        await ImportExternalSourceAsync(dlg.FileName);
    }

    private async void ImportSingerHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_importCts is not null)
        {
            MessageBox.Show("An import is already running.", "Singer History Import", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new OpenFileDialog
        {
            Title = "Import Singer History — select another karaoke program's export or database",
            Filter = "Singer history files|*.csv;*.tsv;*.txt;*.json;*.xml;*.db;*.db3;*.sqlite;*.sqlite3;*.s3db;*.sqlitedb;*.musicdb;*.mdb;*.accdb;*.kdb|All files|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;

        _importCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        CancelImportButton.Visibility = Visibility.Visible;
        try
        {
            var token = _importCts.Token;
            SearchStatus.Text = "Inspecting singer history read-only...";
            var preview = await _externalSingerHistoryImporter.PreviewAsync(dlg.FileName, token);
            var samples = preview.SampleRows.Count == 0 ? "No usable sample rows found."
                : string.Join("\n", preview.SampleRows.Take(8).Select(x => $"{x.Singer} — {x.Artist} — {x.Title} — {x.SungAt}"));
            var notes = preview.Warnings.Count == 0 ? string.Empty : $"\n\nNotes:\n{string.Join("\n", preview.Warnings.Take(6))}";
            var go = MessageBox.Show(
                $"Detected: {preview.DetectedSource}\n\nMapping:\n{preview.DetectedMapping}\n\nSample:\n{samples}{notes}\n\nImport this singer history into Hazz?\nThe source remains read-only. Reimporting the same file safely replaces its earlier imported rows.",
                "Singer History Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (go != MessageBoxResult.Yes) { SearchStatus.Text = "Singer history import cancelled"; return; }

            var progress = new Progress<SingerHistoryImportProgress>(p => SearchStatus.Text =
                $"Singer history • {p.RowsRead:N0} read • {p.Imported:N0} imported • {p.MatchedSongs:N0} matched");
            var result = await Task.Run(async () => await _externalSingerHistoryImporter.ImportAsync(dlg.FileName, progress, token), token);
            await RefreshSavedSingerNamesAsync();
            SearchStatus.Text = $"Singer history imported • {result.HistoryRowsImported:N0} performances";
            var warningText = result.Warnings.Count == 0 ? string.Empty : $"\n\nNotes (first {Math.Min(10, result.Warnings.Count)}):\n{string.Join("\n", result.Warnings.Take(10))}";
            MessageBox.Show(
                $"{result.DetectedSource} singer history import complete.\n\nRows read: {result.RowsRead:N0}\nSingers: {result.SingersImported:N0}\nHistory performances: {result.HistoryRowsImported:N0}\nMatched to Hazz songs: {result.MatchedSongs:N0}\nPreserved without a library match: {result.UnmatchedSongs:N0}\nErrors: {result.Errors:N0}{warningText}",
                "Singer History Import", MessageBoxButton.OK, result.Errors == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (OperationCanceledException) { SearchStatus.Text = "Singer history import cancelled"; }
        catch (Exception ex)
        {
            SearchStatus.Text = "Singer history import error";
            MessageBox.Show(ex.Message, "Singer History Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _importCts?.Dispose();
            _importCts = null;
            CancelImportButton.Visibility = Visibility.Collapsed;
        }
    }

    private async void ImportSmartFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Smart Import — select the other application's data or export folder" };
        if (dlg.ShowDialog(this) != true) return;
        await ImportExternalSourceAsync(dlg.FolderName);
    }

    private async Task ImportExternalSourceAsync(string sourcePath)
    {
        if (_importCts is not null)
        {
            MessageBox.Show("A library import is already running.", "Other Software Import", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _importCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        CancelImportButton.Visibility = Visibility.Visible;
        try
        {
            var token = _importCts.Token;
            SearchStatus.Text = "Inspecting external library read-only...";
            var preview = await _externalImporter.PreviewAsync(sourcePath, token);
            var options = new ExternalImportWindow(preview) { Owner = this };
            if (options.ShowDialog() != true)
            {
                SearchStatus.Text = "External import cancelled";
                return;
            }

            // Read WPF control-backed options while still on the UI thread. Accessing
            // VerifyPathsCheck from inside Task.Run throws a cross-thread ownership error.
            var selectedMediaMode = options.SelectedMediaMode;
            var verifyPaths = options.VerifyPaths;

            var progress = new Progress<ExternalImportProgress>(p =>
            {
                _ = Dispatcher.InvokeAsync(() =>
                {
                    SearchStatus.Text = $"External import • {p.RowsRead:N0} read • {p.RecordsImported:N0} indexed • {p.MissingFiles:N0} missing";
                    if (!string.IsNullOrWhiteSpace(p.CurrentPath)) LibraryCountText.ToolTip = p.CurrentPath;
                });
            });

            var result = await Task.Run(async () =>
                await _externalImporter.ImportAsync(sourcePath, selectedMediaMode, verifyPaths, progress, token), token);

            foreach (var group in result.WatchRoots.GroupBy(x => x.MediaKind, StringComparer.OrdinalIgnoreCase))
                await _libraryRoots.UpsertRootsAsync(group.Select(x => x.Path), group.Key, true, token);

            await RefreshLibraryCountsAsync();
            await RefreshLibraryAutoWatchAsync();
            SearchStatus.Text = $"{result.DetectedSource} import complete • {result.RecordsImported:N0} tracks";

            var rootsText = result.WatchRoots.Count == 0
                ? "No watch roots inferred. Add folders under IMPORT if you want Hazz to monitor them for new files."
                : string.Join("\n", result.WatchRoots.Select(x => $"{x.MediaKind}: {x.Path}"));
            var warnings = result.Warnings.Count == 0 ? string.Empty : $"\n\nNotes (first {Math.Min(12, result.Warnings.Count)}):\n{string.Join("\n", result.Warnings.Take(12))}";
            MessageBox.Show(
                $"{result.DetectedSource} import complete.\n\nRows read: {result.RowsRead:N0}\nTracks indexed/updated: {result.RecordsImported:N0}\nKaraoke: {result.KaraokeImported:N0}\nMusic: {result.MusicImported:N0}\nPlaylists: {result.PlaylistsImported:N0}\nPlaylist items: {result.PlaylistItemsImported:N0}\nVirtual folders created: {result.VirtualFoldersImported:N0}\nVirtual-folder track links added: {result.VirtualFolderTrackLinksImported:N0}\nMissing paths: {result.MissingFiles:N0}\nErrors: {result.Errors:N0}\n\nWatched roots:\n{rootsText}{warnings}",
                "Other Software Import", MessageBoxButton.OK,
                result.Errors == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (OperationCanceledException)
        {
            SearchStatus.Text = "External import cancelled";
        }
        catch (Exception ex)
        {
            SearchStatus.Text = "External import error";
            MessageBox.Show(ex.Message, "Other Software Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _importCts?.Dispose();
            _importCts = null;
            CancelImportButton.Visibility = Visibility.Collapsed;
            LibraryCountText.ToolTip = null;
        }
    }

    private async void ImportKarma_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Karma KDB / legacy datamain.xml",
            Filter = "Karma data|*.kdb;*.mdb;*.accdb;*.xml|All files|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var preview = await _karma.PreviewAsync(dlg.FileName, _lifetime.Token);
            var go = MessageBox.Show(
                $"Detected: {preview.SourceType}\nObjects: {preview.TablesOrFiles.Count}\n\nImport singers into Hazz?\nThe Karma file will remain READ-ONLY.",
                "Karma Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (go != MessageBoxResult.Yes) return;

            var result = await _karma.ImportAsync(dlg.FileName, _lifetime.Token);
            await RefreshSavedSingerNamesAsync();
            MessageBox.Show(
                $"Singers imported: {result.SingersImported}\nHistory rows imported: {result.HistoryRowsImported}\n\n{string.Join("\n", result.Warnings)}",
                "Karma Import", MessageBoxButton.OK,
                result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Karma Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ImportBpmStudio_Click(object sender, RoutedEventArgs e)
    {
        if (_bpmImportProgressWindow is { IsLoaded: true })
        {
            _bpmImportProgressWindow.Activate();
            return;
        }

        var folder = new OpenFolderDialog { Title = "Select BPM Studio data / playlist folder" };
        if (folder.ShowDialog(this) != true) return;
        try
        {
            var preview = await _bpmStudio.PreviewAsync(folder.FolderName, _lifetime.Token);
            var go = MessageBox.Show(
                $"BPM Studio files found:\n\nPlaylists: {preview.PlaylistFiles:N0}\nHistory lists: {preview.HistoryFiles:N0}\nArchive group files detected: {preview.ArchiveGroupFiles:N0}\n\nImport playlists, history and BPM virtual folders using FAST mode?\n\nThe import does not test every music file on disk. BPM .GRP/.PLG archive groups become Hazz virtual folders while their tracks stay in their original locations.\n\nBPM Studio files remain READ-ONLY.\n\nA progress window will remain visible during the import.",
                "BPM Studio Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (go != MessageBoxResult.Yes) return;

            var progressWindow = new ImportProgressWindow("Importing BPM Studio") { Owner = this };
            _bpmImportProgressWindow = progressWindow;
            progressWindow.Closed += (_, _) =>
            {
                if (ReferenceEquals(_bpmImportProgressWindow, progressWindow))
                    _bpmImportProgressWindow = null;
            };
            progressWindow.Show();

            using var importCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, progressWindow.CancellationToken);
            var progress = new Progress<BpmStudioImportProgress>(progressWindow.Update);

            try
            {
                var result = await _bpmStudio.ImportAsync(folder.FolderName, progress, importCts.Token);
                await RefreshLibraryCountsAsync();
                progressWindow.Complete();
                progressWindow.CloseAfterImport();

                var warningText = result.Warnings.Count == 0 ? string.Empty : $"\n\nWarnings (first {Math.Min(12, result.Warnings.Count)}):\n{string.Join("\n", result.Warnings.Take(12))}";
                MessageBox.Show(
                    $"BPM Studio fast import complete.\n\nPlaylists imported: {result.PlaylistsImported:N0}\nHistory lists imported: {result.HistoryListsImported:N0}\nPlaylist items: {result.PlaylistItemsImported:N0}\nHistory items: {result.HistoryItemsImported:N0}\nUnique referenced music paths indexed: {result.MusicTracksLinkedOrIndexed:N0}\nVirtual folders created: {result.VirtualFoldersImported:N0}\nVirtual-folder track links added: {result.VirtualFolderTrackLinksImported:N0}\nUnreadable/unsupported list/group files: {result.UnsupportedFiles:N0}{warningText}",
                    "BPM Studio Import", MessageBoxButton.OK, result.UnsupportedFiles == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (OperationCanceledException)
            {
                progressWindow.Complete("Import cancelled — no partial transaction was committed.");
                progressWindow.CloseAfterImport();
                MessageBox.Show(this, "BPM Studio import was cancelled. Hazz rolled back the in-progress database transaction.",
                    "BPM Studio Import", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch
            {
                progressWindow.CloseAfterImport();
                throw;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "BPM Studio Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenLibraryBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (_libraryBrowserWindow is { IsLoaded: true })
        {
            _libraryBrowserWindow.Activate();
            return;
        }

        _libraryBrowserWindow = new LibraryBrowserWindow(_library) { Owner = this };
        _libraryBrowserWindow.AddToSingerRequested += async (_, song) => await AddBrowserSongToSelectedSingerAsync(song);
        _libraryBrowserWindow.AddToDeckRequested += (_, request) => AddBrowserSongToDeck(request.Deck, request.Song);
        _libraryBrowserWindow.Closed += (_, _) => _libraryBrowserWindow = null;
        _libraryBrowserWindow.Show();
    }

    private async Task AddBrowserSongToSelectedSingerAsync(SongRecord song)
    {
        if (!string.Equals(song.MediaKind, "Karaoke", StringComparison.OrdinalIgnoreCase)) return;
        SingerQueueEntry? singer = QueueList.SelectedItem as SingerQueueEntry;
        if (singer is null)
        {
            var typedName = SingerNameBox.Text.Trim();
            if (typedName.Length > 0) singer = await GetOrCreateQueueSingerAsync(typedName);
        }
        if (singer is null)
        {
            MessageBox.Show("Select a singer in the main rotation first, then choose ADD TO SELECTED SINGER in the Library Browser.",
                "Library Browser", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!await AddSongToSingerAsync(singer, song)) return;
        QueueList.SelectedItem = singer;
        QueueList.ScrollIntoView(singer);
        QueueDragHint.Text = $"Added {song.Title} to {singer.SingerName} ({singer.SongCount} songs)";
        UpdateAudienceNext();
    }

    private void AddBrowserSongToDeck(int deck, SongRecord song)
    {
        if (!string.Equals(song.MediaKind, "Music", StringComparison.OrdinalIgnoreCase)) return;
        if (!File.Exists(song.FilePath))
        {
            BrokenMediaRegistry.Mark(song.FilePath, "File missing or unavailable");
            MessageBox.Show("The indexed music file is no longer present:\n" + song.FilePath, "Missing Music File");
            return;
        }
        var list = deck == 2 ? DeckBPlaylist : DeckAPlaylist;
        list.Items.Add(CreateMusicQueueItem(song));
        RenumberPlaylist(list);
        if (list.SelectedIndex < 0) list.SelectedIndex = 0;
        UpdateMusicAutomationStatus(_singleDeckMode && deck == 2
            ? $"Added {song.Title} to the Side List"
            : $"Added {song.Title} to Deck {deck}");
    }

    private void MusicPlaylist_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox list) return;
        _musicPlaylistDragStart = e.GetPosition(list);
        _musicPlaylistDragSource = list;
        _musicPlaylistDragItem = FindMusicQueueItemAt(list, e.OriginalSource as DependencyObject);
        if (_musicPlaylistDragItem?.IsNowPlaying == true)
        {
            _musicPlaylistDragItem = null;
            _musicPlaylistDragSource = null;
            UpdateMusicAutomationStatus("The NOW PLAYING row stays at #1; reorder the queued tracks underneath it");
        }
    }

    private void MusicPlaylist_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _musicPlaylistDragItem is null || _musicPlaylistDragSource is null) return;
        var p = e.GetPosition(_musicPlaylistDragSource);
        if (Math.Abs(p.X - _musicPlaylistDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _musicPlaylistDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var item = _musicPlaylistDragItem;
        var source = _musicPlaylistDragSource;
        var data = new DataObject();
        data.SetData(typeof(MusicQueueItem), item);
        data.SetData("HazzMusicPlaylistSource", source.Name);
        if (_singleDeckMode && ReferenceEquals(source, DeckBPlaylist))
        {
            var selected = source.SelectedItems.OfType<MusicQueueItem>().ToHashSet();
            if (!selected.Contains(item)) selected = new HashSet<MusicQueueItem> { item };
            var ordered = source.Items.OfType<MusicQueueItem>()
                .Where(selected.Contains)
                .Where(candidate => !candidate.IsNowPlaying)
                .ToArray();
            if (ordered.Length > 0) data.SetData(SideListQueueBatchDataFormat, ordered);
        }
        DragDrop.DoDragDrop(source, data, DragDropEffects.Move);
        _musicPlaylistDragItem = null;
        _musicPlaylistDragSource = null;
    }

    private void MusicPlaylist_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(SideListQueueBatchDataFormat) is MusicQueueItem[] queued && queued.Length > 0)
            e.Effects = _singleDeckMode && (ReferenceEquals(sender, DeckAPlaylist) || ReferenceEquals(sender, DeckBPlaylist))
                ? DragDropEffects.Move : DragDropEffects.None;
        else if (e.Data.GetData(typeof(MusicQueueItem)) is MusicQueueItem)
            e.Effects = DragDropEffects.Move;
        else if (e.Data.GetData(SearchSongBatchDataFormat) is SongRecord[] songs && songs.Length > 0 &&
                 songs.All(song => string.Equals(song.MediaKind, "Music", StringComparison.OrdinalIgnoreCase)))
            e.Effects = DragDropEffects.Copy;
        else if (e.Data.GetData(typeof(SongRecord)) is SongRecord song &&
                 string.Equals(song.MediaKind, "Music", StringComparison.OrdinalIgnoreCase))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void DeckAPlaylist_Drop(object sender, DragEventArgs e) => HandleMusicPlaylistDrop(DeckAPlaylist, MusicDeckId.Deck1, e);
    private void DeckBPlaylist_Drop(object sender, DragEventArgs e) => HandleMusicPlaylistDrop(DeckBPlaylist, MusicDeckId.Deck2, e);

    private void HandleMusicPlaylistDrop(ListBox target, MusicDeckId targetDeck, DragEventArgs e)
    {
        if (e.Data.GetData(SideListQueueBatchDataFormat) is MusicQueueItem[] sideListItems && sideListItems.Length > 0)
        {
            var sideSourceName = e.Data.GetData("HazzMusicPlaylistSource") as string;
            if (!_singleDeckMode || !string.Equals(sideSourceName, DeckBPlaylist.Name, StringComparison.Ordinal) ||
                (!ReferenceEquals(target, DeckAPlaylist) && !ReferenceEquals(target, DeckBPlaylist)))
            {
                e.Handled = true;
                return;
            }

            var insertAt = GetMusicPlaylistInsertIndex(target, e.OriginalSource as DependencyObject, e.GetPosition(target));
            var movingItems = sideListItems.Where(item => DeckBPlaylist.Items.Contains(item) && !item.IsNowPlaying).ToArray();
            if (ReferenceEquals(target, DeckBPlaylist))
            {
                insertAt -= movingItems.Count(item => DeckBPlaylist.Items.IndexOf(item) < insertAt);
            }
            foreach (var sideListItem in movingItems) DeckBPlaylist.Items.Remove(sideListItem);
            insertAt = Math.Clamp(insertAt, 0, target.Items.Count);
            foreach (var sideListItem in movingItems) target.Items.Insert(insertAt++, sideListItem);
            RenumberPlaylist(DeckBPlaylist);
            RenumberPlaylist(target);
            RecalculateMusicDeckOrder(MusicDeckId.Deck1);
            RecalculateMusicDeckOrder(MusicDeckId.Deck2);
            var firstMoved = movingItems.FirstOrDefault();
            if (firstMoved is not null)
            {
                target.SelectedItem = firstMoved;
                target.ScrollIntoView(firstMoved);
            }
            UpdateMusicAutomationStatus(ReferenceEquals(target, DeckAPlaylist)
                ? $"Moved {movingItems.Length:N0} track(s) from the side list to Deck 1"
                : $"Reordered {movingItems.Length:N0} track(s) in the side list");
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(SearchSongBatchDataFormat) is SongRecord[] songs && songs.Length > 0)
        {
            var insertAt = GetMusicPlaylistInsertIndex(target, e.OriginalSource as DependencyObject, e.GetPosition(target));
            var firstInserted = -1;
            var added = 0;
            var broken = 0;
            foreach (var selectedSong in songs.Where(candidate => string.Equals(candidate.MediaKind, "Music", StringComparison.OrdinalIgnoreCase)))
            {
                if (!File.Exists(selectedSong.FilePath))
                {
                    BrokenMediaRegistry.Mark(selectedSong.FilePath, "File missing or unavailable");
                    broken++;
                    continue;
                }
                if (firstInserted < 0) firstInserted = insertAt;
                target.Items.Insert(insertAt++, CreateMusicQueueItem(selectedSong));
                added++;
            }
            RenumberPlaylist(target);
            RecalculateMusicDeckOrder(targetDeck);
            if (firstInserted >= 0) target.SelectedIndex = firstInserted;
            var targetName = _singleDeckMode && targetDeck == MusicDeckId.Deck2
                ? "Side List"
                : $"Deck {(targetDeck == MusicDeckId.Deck2 ? 2 : 1)}";
            UpdateMusicAutomationStatus(broken == 0
                ? $"Added {added:N0} selected track(s) to {targetName}"
                : $"Added {added:N0} selected track(s); skipped {broken:N0} missing file(s)");
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(typeof(SongRecord)) is SongRecord song)
        {
            if (!string.Equals(song.MediaKind, "Music", StringComparison.OrdinalIgnoreCase)) return;
            var insertAt = GetMusicPlaylistInsertIndex(target, e.OriginalSource as DependencyObject, e.GetPosition(target));
            target.Items.Insert(insertAt, CreateMusicQueueItem(song));
            RenumberPlaylist(target);
            RecalculateMusicDeckOrder(targetDeck);
            target.SelectedIndex = insertAt;
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(typeof(MusicQueueItem)) is not MusicQueueItem item) return;
        var sourceName = e.Data.GetData("HazzMusicPlaylistSource") as string;
        var source = string.Equals(sourceName, DeckBPlaylist.Name, StringComparison.Ordinal) ? DeckBPlaylist : DeckAPlaylist;
        var sourceDeck = ReferenceEquals(source, DeckBPlaylist) ? MusicDeckId.Deck2 : MusicDeckId.Deck1;
        if (!source.Items.Contains(item)) return;
        if (!ReferenceEquals(source, target) && !(_singleDeckMode && ReferenceEquals(source, DeckBPlaylist) && ReferenceEquals(target, DeckAPlaylist)))
        {
            UpdateMusicAutomationStatus("Drag music within the same deck playlist to reorder it");
            e.Handled = true;
            return;
        }

        var insertIndex = GetMusicPlaylistInsertIndex(target, e.OriginalSource as DependencyObject, e.GetPosition(target));
        var oldIndex = source.Items.IndexOf(item);
        source.Items.Remove(item);

        if (ReferenceEquals(source, target) && oldIndex < insertIndex) insertIndex--;
        insertIndex = Math.Clamp(insertIndex, 0, target.Items.Count);
        target.Items.Insert(insertIndex, item);

        RecalculateMusicDeckOrder(sourceDeck);
        if (targetDeck != sourceDeck) RecalculateMusicDeckOrder(targetDeck);
        else RenumberPlaylist(target);
        target.SelectedItem = item;
        target.ScrollIntoView(item);
        UpdateMusicAutomationStatus(!ReferenceEquals(source, target)
            ? $"Moved to Deck 1 • {item.DisplayArtist} — {item.DisplayTitle}"
            : $"Playlist order changed • {item.DisplayArtist} — {item.DisplayTitle}");
        e.Handled = true;
    }

    private static MusicQueueItem? FindMusicQueueItemAt(ListBox list, DependencyObject? original)
    {
        if (original is null) return null;
        var container = ItemsControl.ContainerFromElement(list, original) as ListBoxItem;
        return container is null ? null : list.ItemContainerGenerator.ItemFromContainer(container) as MusicQueueItem;
    }

    private static int GetMusicPlaylistInsertIndex(ListBox list, DependencyObject? original, Point pointInList)
    {
        if (original is null) return list.Items.Count;
        var container = ItemsControl.ContainerFromElement(list, original) as ListBoxItem;
        if (container is null) return list.Items.Count;
        var index = list.ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0) return list.Items.Count;
        var pointInItem = list.TranslatePoint(pointInList, container);
        if (pointInItem.Y > container.ActualHeight / 2.0) index++;
        return Math.Clamp(index, 0, list.Items.Count);
    }

    private void OpenMusicArchive_Click(object sender, RoutedEventArgs e) => OpenMusicArchive(null);
    private void DeckALoadPlaylist_Click(object sender, RoutedEventArgs e) => OpenMusicArchive(1);
    private void DeckBLoadPlaylist_Click(object sender, RoutedEventArgs e) => OpenMusicArchive(2);
    private async void DeckASavePlaylist_Click(object sender, RoutedEventArgs e) => await SaveMusicDeckPlaylistAsync(1);
    private async void DeckBSavePlaylist_Click(object sender, RoutedEventArgs e) => await SaveMusicDeckPlaylistAsync(2);
    private void DeckARemoveSelected_Click(object sender, RoutedEventArgs e) => RemoveSelectedMusicTracks(MusicDeckId.Deck1);
    private void DeckBRemoveSelected_Click(object sender, RoutedEventArgs e) => RemoveSelectedMusicTracks(MusicDeckId.Deck2);

    private void DeckAPlaylist_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete) return;
        RemoveSelectedMusicTracks(MusicDeckId.Deck1);
        e.Handled = true;
    }

    private void DeckBPlaylist_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete) return;
        RemoveSelectedMusicTracks(MusicDeckId.Deck2);
        e.Handled = true;
    }

    private void SideListSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (!_singleDeckMode) return;
        DeckBPlaylist.SelectAll();
        UpdateMusicAutomationStatus(DeckBPlaylist.Items.Count == 0
            ? "Side List is empty"
            : $"Side List • selected all {DeckBPlaylist.Items.Count:N0} track(s)");
    }

    private void SideListMoveUp_Click(object sender, RoutedEventArgs e) => MoveSideListSelection(-1);
    private void SideListMoveDown_Click(object sender, RoutedEventArgs e) => MoveSideListSelection(1);

    private void MoveSideListSelection(int direction)
    {
        if (!_singleDeckMode) return;
        var selected = DeckBPlaylist.SelectedItems.OfType<MusicQueueItem>().ToHashSet();
        if (selected.Count == 0)
        {
            UpdateMusicAutomationStatus("Select one or more Side List tracks to move");
            return;
        }

        if (direction < 0)
        {
            for (var index = 1; index < DeckBPlaylist.Items.Count; index++)
            {
                var item = DeckBPlaylist.Items[index] as MusicQueueItem;
                var previous = DeckBPlaylist.Items[index - 1] as MusicQueueItem;
                if (item is null || previous is null || !selected.Contains(item) || selected.Contains(previous)) continue;
                DeckBPlaylist.Items.RemoveAt(index);
                DeckBPlaylist.Items.Insert(index - 1, item);
            }
        }
        else
        {
            for (var index = DeckBPlaylist.Items.Count - 2; index >= 0; index--)
            {
                var item = DeckBPlaylist.Items[index] as MusicQueueItem;
                var next = DeckBPlaylist.Items[index + 1] as MusicQueueItem;
                if (item is null || next is null || !selected.Contains(item) || selected.Contains(next)) continue;
                DeckBPlaylist.Items.RemoveAt(index);
                DeckBPlaylist.Items.Insert(index + 1, item);
            }
        }

        RenumberPlaylist(DeckBPlaylist);
        RecalculateMusicDeckOrder(MusicDeckId.Deck2);
        DeckBPlaylist.SelectedItems.Clear();
        foreach (var item in DeckBPlaylist.Items.OfType<MusicQueueItem>().Where(selected.Contains))
            DeckBPlaylist.SelectedItems.Add(item);
        if (DeckBPlaylist.SelectedItems.Count > 0) DeckBPlaylist.ScrollIntoView(DeckBPlaylist.SelectedItems[0]);
        MarkMusicDeckQueuesDirty();
        UpdateMusicAutomationStatus($"Side List • moved {selected.Count:N0} track(s) {(direction < 0 ? "up" : "down")}");
    }

    private void SideListSendToDeck1_Click(object sender, RoutedEventArgs e)
    {
        if (!_singleDeckMode) return;
        var selected = DeckBPlaylist.Items.OfType<MusicQueueItem>()
            .Where(item => DeckBPlaylist.SelectedItems.Contains(item))
            .ToArray();
        if (selected.Length == 0)
        {
            UpdateMusicAutomationStatus("Select one or more Side List tracks to send to Deck 1");
            return;
        }

        foreach (var item in selected) DeckBPlaylist.Items.Remove(item);
        foreach (var item in selected) DeckAPlaylist.Items.Add(item);
        RenumberPlaylist(DeckBPlaylist);
        RenumberPlaylist(DeckAPlaylist);
        RecalculateMusicDeckOrder(MusicDeckId.Deck2);
        RecalculateMusicDeckOrder(MusicDeckId.Deck1);
        DeckAPlaylist.SelectedItems.Clear();
        foreach (var item in selected) DeckAPlaylist.SelectedItems.Add(item);
        DeckAPlaylist.ScrollIntoView(selected[0]);
        MarkMusicDeckQueuesDirty();
        UpdateMusicAutomationStatus($"Moved {selected.Length:N0} track(s) from the Side List to Deck 1");
    }

    private void SideListClear_Click(object sender, RoutedEventArgs e)
    {
        if (!_singleDeckMode) return;
        if (DeckBPlaylist.Items.Count == 0)
        {
            UpdateMusicAutomationStatus("Side List is already empty");
            return;
        }
        if (MessageBox.Show(this,
                $"Remove all {DeckBPlaylist.Items.Count:N0} tracks from the Side List?\n\nNo music files will be deleted.",
                "Clear Side List", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        DeckBPlaylist.Items.Clear();
        RecalculateMusicDeckOrder(MusicDeckId.Deck2);
        MarkMusicDeckQueuesDirty();
        UpdateMusicAutomationStatus("Side List cleared");
    }

    private void RemoveSelectedMusicTracks(MusicDeckId deck)
    {
        var list = PlaylistFor(deck);
        var selected = list.SelectedItems.OfType<MusicQueueItem>().ToList();
        if (selected.Count == 0)
        {
            UpdateMusicAutomationStatus($"Select a track in {DeckName(deck)} to remove it");
            return;
        }

        var current = CurrentMusicItemFor(deck);
        var removable = selected
            .Where(item => current is null || !ReferenceEquals(item, current))
            .ToList();
        var skippedCurrent = selected.Count - removable.Count;

        if (removable.Count == 0)
        {
            UpdateMusicAutomationStatus($"{DeckName(deck)}: the NOW PLAYING track cannot be removed until it is stopped or finishes");
            return;
        }

        var firstIndex = removable
            .Select(item => list.Items.IndexOf(item))
            .Where(index => index >= 0)
            .DefaultIfEmpty(0)
            .Min();

        foreach (var item in removable)
            list.Items.Remove(item);

        RecalculateMusicDeckOrder(deck);

        // If karaoke had remembered a specific unplayed track to resume with and
        // the operator removes it, immediately pick the next valid queued track.
        if (_resumeMusicDeck == deck && (_resumeMusicItem is null || !list.Items.Contains(_resumeMusicItem)))
        {
            _resumeMusicItem = GetScheduledItem(deck);
            _resumeMusicIndex = _resumeMusicItem is null ? -1 : list.Items.IndexOf(_resumeMusicItem);
        }

        list.SelectedItems.Clear();
        if (list.Items.Count > 0)
        {
            var selectionIndex = Math.Clamp(firstIndex, 0, list.Items.Count - 1);
            if (list.Items[selectionIndex] is MusicQueueItem remaining)
            {
                list.SelectedItem = remaining;
                list.ScrollIntoView(remaining);
            }
        }

        MarkMusicDeckQueuesDirty();
        var suffix = skippedCurrent > 0 ? " • NOW PLAYING left in place" : string.Empty;
        UpdateMusicAutomationStatus($"{DeckName(deck)} • removed {removable.Count:N0} queued track(s){suffix}");
    }

    private async Task SaveMusicDeckPlaylistAsync(int deck)
    {
        var list = deck == 1 ? DeckAPlaylist : DeckBPlaylist;
        var current = deck == 1 ? _deck1CurrentItem : _deck2CurrentItem;
        var items = list.Items.OfType<MusicQueueItem>()
            .Where(item => current is null || !ReferenceEquals(item, current))
            .Select(item => new MusicPlaylistSaveItem(item.SongId, item.FilePath, item.Artist, item.Title))
            .ToArray();

        if (items.Length == 0)
        {
            MessageBox.Show(this,
                $"Deck {deck} has no remaining unplayed tracks to save.",
                "Save Playlist",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var suggestedName = $"Deck {deck} - {DateTime.Now:yyyy-MM-dd HHmm}";
        var dialog = new PlaylistNameDialog(suggestedName) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await _musicPlaylists.SavePlaylistAsync(dialog.PlaylistName, items, _lifetime.Token);
            if (_musicArchiveWindow is { IsLoaded: true })
                await _musicArchiveWindow.RefreshFromHostAsync();

            UpdateMusicAutomationStatus($"Saved playlist '{dialog.PlaylistName}' • {items.Length:N0} track(s)");
            MessageBox.Show(this,
                $"Saved '{dialog.PlaylistName}' with {items.Length:N0} remaining track(s).\n\nYou can reload it from LOAD PLAYLIST / HISTORY.",
                "Playlist Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Could not save the playlist.\n\n" + ex.Message,
                "Save Playlist",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenMusicArchive(int? preferredDeck)
    {
        if (_musicArchiveWindow is { IsLoaded: true })
        {
            _musicArchiveWindow.SetPreferredDeck(preferredDeck);
            _musicArchiveWindow.Activate();
            return;
        }
        _musicArchiveWindow = new MusicArchiveWindow(_musicPlaylists, preferredDeck) { Owner = this };
        _musicArchiveWindow.LoadToDeckRequested += (_, request) => AddPathsToMusicDeck(request.Deck, request.Paths);
        _musicArchiveWindow.Closed += (_, _) => _musicArchiveWindow = null;
        _musicArchiveWindow.Show();
    }

    private void AddPathsToMusicDeck(int deck, IReadOnlyList<string> paths)
    {
        var list = deck == 1 ? DeckAPlaylist : DeckBPlaylist;
        foreach (var path in paths.Where(File.Exists)) list.Items.Add(CreateMusicQueueItem(path));
        RenumberPlaylist(list);
        RecalculateMusicDeckOrder(deck == 1 ? MusicDeckId.Deck1 : MusicDeckId.Deck2);
        if (list.SelectedIndex < 0 && list.Items.Count > 0) list.SelectedIndex = 0;
        UpdateMusicAutomationStatus($"Added {paths.Count:N0} track(s) to Deck {deck}");
    }

    private void NewShow_Click(object sender, RoutedEventArgs e)
    {
        if (_queue.Count > 0 && MessageBox.Show("Clear the current singer rotation for a new show?", "New Show", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        _queue.Clear();
        _showStartedUtc = DateTimeOffset.UtcNow;
        _liveShowStateStore.Clear();
        _kamikazeSinger = null;
        _kamikazeAssignedSong = null;
        _kamikazeLastPath = string.Empty;
        _kamikazeBannerRequested = false;
        _audience?.HideKamikazeBanner();
        _musicPlayedThisSession.Clear();
        RefreshSessionPlayedColours();
        UpdateMusicAutomationStatus("New show started — session played-track colours cleared");
        UpdateAudienceNext();
    }
}
