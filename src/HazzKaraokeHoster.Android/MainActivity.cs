using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using HazzKaraokeHoster.Data;

namespace HazzKaraokeHoster.Android;

[Activity(
    Label = "Hazz Karaoke Hoster",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : Activity
{
    private const int ImportDatabaseRequest = 401;
    private const int MapMediaRootRequest = 402;

    private readonly List<SingerQueueEntry> _rotation = new();
    private readonly List<SongRecord> _searchResults = new();
    private SingerQueueEntry? _selectedSinger;
    private SongRecord? _selectedSong;
    private string _searchKind = "Karaoke";

    private HazzDatabase? _database;
    private ILibraryRepository? _library;
    private ISingerRepository? _singers;
    private AndroidMediaRootService? _mediaRoots;
    private AndroidShowStateService? _showState;
    private AndroidMediaPlaybackService? _mediaPlayback;
    private string? _pendingWindowsPrefix;

    private TextView _status = null!;
    private TextView _modeLabel = null!;
    private EditText _searchBox = null!;
    private ListView _rotationList = null!;
    private ListView _searchList = null!;
    private SingerRotationAdapter _rotationAdapter = null!;
    private SearchResultAdapter _searchAdapter = null!;
    private VideoView _videoView = null!;

    private string DatabasePath => System.IO.Path.Combine(FilesDir!.AbsolutePath, "hazz-hoster.db");

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.SetSoftInputMode(SoftInput.AdjustResize);
        SetContentView(BuildUi());

        _mediaRoots = new AndroidMediaRootService(this);
        _showState = new AndroidShowStateService(this);
        _mediaPlayback = new AndroidMediaPlaybackService(this);
        _mediaPlayback.Completed += (_, _) => RunOnUiThread(() => SetStatus("Playback finished"));

        try
        {
            await OpenDatabaseAsync();
            await RestoreShowStateAsync();
            await RefreshLibraryStatusAsync();
        }
        catch (Exception ex)
        {
            SetStatus("Database startup failed: " + ex.Message);
        }
    }

    private View BuildUi()
    {
        var root = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        root.SetPadding(Dp(10), Dp(8), Dp(10), Dp(8));
        root.SetBackgroundColor(Color.Rgb(11, 15, 20));

        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        header.SetGravity(GravityFlags.CenterVertical);
        var title = MakeText("HAZZ KARAOKE HOSTER • ANDROID v0.2", 20, true, Color.White);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        var mapRoot = MakeButton("MAP USB / MEDIA ROOT");
        mapRoot.Click += (_, _) => BeginMapMediaRoot();
        header.AddView(mapRoot);
        var roots = MakeButton("USB / ROOTS");
        roots.Click += (_, _) => ShowMediaRoots();
        header.AddView(roots);
        var importDb = MakeButton("IMPORT HAZZ DB");
        importDb.Click += (_, _) => BeginDatabaseImport();
        header.AddView(importDb);
        root.AddView(header);

        _status = MakeText("Starting…", 12, false, Color.Rgb(168, 185, 201));
        root.AddView(_status, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        var body = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        root.AddView(body, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        body.AddView(BuildSingerPanel(), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 0.46f));
        body.AddView(BuildSearchPanel(), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 0.54f));

        return root;
    }

    private View BuildSingerPanel()
    {
        var panel = MakePanel();
        panel.SetPadding(Dp(6), Dp(6), Dp(6), Dp(6));

        var heading = MakeText("SINGER ROTATION", 17, true, Color.White);
        panel.AddView(heading);

        var singerButtons = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var add = MakeButton("ADD SINGER");
        var hold = MakeButton("HOLD");
        var up = MakeButton("▲");
        var down = MakeButton("▼");
        var remove = MakeButton("REMOVE");

        add.Click += (_, _) => ShowAddSingerDialog();
        hold.Click += (_, _) => ToggleHold();
        up.Click += (_, _) => MoveSinger(-1);
        down.Click += (_, _) => MoveSinger(1);
        remove.Click += (_, _) => RemoveSinger();

        foreach (var button in new[] { add, hold, up, down, remove })
            singerButtons.AddView(button, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        panel.AddView(singerButtons);

        _rotationList = new ListView(this)
        {
            ChoiceMode = ChoiceMode.Single
        };
        _rotationAdapter = new SingerRotationAdapter(this, _rotation);
        _rotationList.Adapter = _rotationAdapter;
        _rotationList.ItemClick += (_, e) =>
        {
            _selectedSinger = _rotation[e.Position];
            SetStatus($"Selected singer: {_selectedSinger.SingerName}");
        };
        panel.AddView(_rotationList, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        var addSong = MakeButton("ADD SELECTED SEARCH RESULT → SINGER");
        addSong.Click += (_, _) => AddSelectedSongToSinger();
        panel.AddView(addSong);

        return panel;
    }

    private View BuildSearchPanel()
    {
        var panel = MakePanel();
        panel.SetPadding(Dp(6), Dp(6), Dp(6), Dp(6));

        var modeRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _modeLabel = MakeText("KARAOKE SEARCH", 17, true, Color.White);
        modeRow.AddView(_modeLabel, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        var karaoke = MakeButton("KARAOKE");
        var music = MakeButton("MUSIC");
        var video = MakeButton("VIDEO");
        karaoke.Click += (_, _) => SetSearchKind("Karaoke");
        music.Click += (_, _) => SetSearchKind("Music");
        video.Click += (_, _) => SetSearchKind("MusicVideo");
        modeRow.AddView(karaoke);
        modeRow.AddView(music);
        modeRow.AddView(video);
        panel.AddView(modeRow);

        var searchRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _searchBox = new EditText(this)
        {
            Hint = "Artist, title, disc or maker…",
            TextSize = 16
        };
        _searchBox.SetSingleLine(true);
        _searchBox.SetTextColor(Color.White);
        _searchBox.SetHintTextColor(Color.Rgb(130, 145, 160));
        _searchBox.ImeOptions = ImeAction.Search;
        _searchBox.EditorAction += async (_, e) =>
        {
            if (e.ActionId == ImeAction.Search) await SearchAsync();
        };

        var search = MakeButton("SEARCH");
        search.Click += async (_, _) => await SearchAsync();
        searchRow.AddView(_searchBox, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        searchRow.AddView(search);
        panel.AddView(searchRow);

        _searchList = new ListView(this) { ChoiceMode = ChoiceMode.Single };
        _searchAdapter = new SearchResultAdapter(this, _searchResults);
        _searchList.Adapter = _searchAdapter;
        _searchList.ItemClick += (_, e) =>
        {
            if (e.Position < 0 || e.Position >= _searchResults.Count) return;
            _selectedSong = _searchResults[e.Position];
            SetStatus($"Selected: {_selectedSong.Artist} — {_selectedSong.Title}");
        };
        panel.AddView(_searchList, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        _videoView = new VideoView(this);
        _videoView.Visibility = ViewStates.Gone;
        panel.AddView(_videoView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(170)));

        var playbackRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var play = MakeButton("PLAY SELECTED");
        var pause = MakeButton("PAUSE / RESUME");
        var stop = MakeButton("STOP");
        play.Click += async (_, _) => await PlaySelectedAsync();
        pause.Click += (_, _) => TogglePlaybackPause();
        stop.Click += (_, _) => StopPlayback();
        foreach (var button in new[] { play, pause, stop })
            playbackRow.AddView(button, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        panel.AddView(playbackRow);

        var footer = MakeText(
            "v0.2: map Windows library roots to internal/SD/USB-OTG folders, keep persistent USB read access, play mapped audio/video, and restore the active singer rotation after restart. CD+G graphics and HDMI audience output are next.",
            11, false, Color.Rgb(160, 174, 188));
        panel.AddView(footer);

        return panel;
    }

    private LinearLayout MakePanel()
    {
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetBackgroundColor(Color.Rgb(17, 24, 32));
        return panel;
    }

    private Button MakeButton(string text)
    {
        var button = new Button(this)
        {
            Text = text,
            TextSize = 12,
        };
        button.SetMinimumHeight(Dp(42));
        return button;
    }

    private TextView MakeText(string text, float size, bool bold, Color colour)
    {
        var view = new TextView(this)
        {
            Text = text,
            TextSize = size,
            Gravity = GravityFlags.CenterVertical
        };
        view.SetTextColor(colour);
        if (bold) view.SetTypeface(null, global::Android.Graphics.TypefaceStyle.Bold);
        view.SetPadding(Dp(6), Dp(4), Dp(6), Dp(4));
        return view;
    }

    private async Task OpenDatabaseAsync()
    {
        _database = new HazzDatabase(DatabasePath);
        _library = new LibraryRepository(_database);
        _singers = new SingerRepository(_database);
        await _library.InitializeAsync();
    }

    private async Task RefreshLibraryStatusAsync()
    {
        if (_library is null) return;
        var counts = await _library.GetLibraryCountsAsync();
        SetStatus($"Database ready • Karaoke {counts.Karaoke:N0} • Music {counts.Music:N0} • Videos {counts.MusicVideo:N0} • {DatabasePath}");
    }

    private async Task SearchAsync()
    {
        if (_library is null) return;
        var query = _searchBox.Text?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            SetStatus("Enter an artist, title, disc or maker.");
            return;
        }

        try
        {
            SetStatus($"Searching {_searchKind}…");
            HideKeyboard();
            var found = await _library.SearchByKindAsync(query, _searchKind, 250);
            _searchResults.Clear();
            _searchResults.AddRange(found);
            _selectedSong = null;
            _searchAdapter.NotifyDataSetChanged();
            SetStatus($"{_searchResults.Count:N0} {_searchKind} result(s)");
        }
        catch (Exception ex)
        {
            SetStatus("Search failed: " + ex.Message);
        }
    }

    private void SetSearchKind(string kind)
    {
        _searchKind = kind;
        _modeLabel.Text = kind switch
        {
            "Music" => "MUSIC SEARCH",
            "MusicVideo" => "MUSIC VIDEO SEARCH",
            _ => "KARAOKE SEARCH"
        };
        _searchResults.Clear();
        _selectedSong = null;
        _searchAdapter.NotifyDataSetChanged();
        SetStatus($"{_modeLabel.Text} ready");
    }

    private void ShowAddSingerDialog()
    {
        var input = new EditText(this) { Hint = "Singer name" };
        input.SetSingleLine(true);
        new AlertDialog.Builder(this)
            .SetTitle("Add singer")
            .SetView(input)
            .SetNegativeButton("Cancel", (_, _) => { })
            .SetPositiveButton("Add", async (_, _) =>
            {
                var name = input.Text?.Trim() ?? string.Empty;
                if (name.Length == 0)
                {
                    SetStatus("Singer name is required.");
                    return;
                }
                await AddSingerAsync(name);
            })
            .Show();
    }

    private async Task AddSingerAsync(string name)
    {
        if (_singers is null) return;
        try
        {
            var id = await _singers.UpsertSingerAsync(name);
            var existing = _rotation.FirstOrDefault(x => x.SingerId == id);
            if (existing is not null)
            {
                _selectedSinger = existing;
                _rotationList.SetSelection(_rotation.IndexOf(existing));
                SetStatus($"{name} is already in this rotation.");
                return;
            }

            var singer = new SingerQueueEntry { SingerId = id, SingerName = name };
            _rotation.Add(singer);
            _selectedSinger = singer;
            RefreshRotation();
            PersistShowState();
            _rotationList.SetSelection(_rotation.Count - 1);
            SetStatus($"Added singer: {name}");
        }
        catch (Exception ex)
        {
            SetStatus("Could not add singer: " + ex.Message);
        }
    }

    private void AddSelectedSongToSinger()
    {
        if (_selectedSinger is null)
        {
            SetStatus("Select a singer first.");
            return;
        }
        if (_selectedSong is null)
        {
            SetStatus("Select a search result first.");
            return;
        }
        if (!string.Equals(_selectedSong.MediaKind, "Karaoke", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("Singer requests currently accept Karaoke results only.");
            return;
        }

        _selectedSinger.Songs.Add(new SingerSongEntry
        {
            SongId = _selectedSong.Id,
            Artist = _selectedSong.Artist,
            SongTitle = _selectedSong.Title,
            FilePath = _selectedSong.FilePath,
            KeyChange = _selectedSong.PreferredKey,
            CdgSyncSeconds = _selectedSong.CdgSyncSeconds,
            DurationSeconds = _selectedSong.DurationSeconds
        });
        RefreshRotation();
        PersistShowState();
        SetStatus($"Added {_selectedSong.Title} to {_selectedSinger.SingerName}");
    }

    private void ToggleHold()
    {
        if (_selectedSinger is null) { SetStatus("Select a singer first."); return; }
        _selectedSinger.IsHeld = !_selectedSinger.IsHeld;
        RefreshRotation();
        PersistShowState();
        SetStatus(_selectedSinger.IsHeld ? $"{_selectedSinger.SingerName} is on HOLD" : $"{_selectedSinger.SingerName} returned to rotation");
    }

    private void MoveSinger(int delta)
    {
        if (_selectedSinger is null) { SetStatus("Select a singer first."); return; }
        var index = _rotation.IndexOf(_selectedSinger);
        var target = Math.Clamp(index + delta, 0, _rotation.Count - 1);
        if (index < 0 || target == index) return;
        _rotation.RemoveAt(index);
        _rotation.Insert(target, _selectedSinger);
        RefreshRotation();
        PersistShowState();
        _rotationList.SetSelection(target);
    }

    private void RemoveSinger()
    {
        if (_selectedSinger is null) { SetStatus("Select a singer first."); return; }
        var name = _selectedSinger.SingerName;
        _rotation.Remove(_selectedSinger);
        _selectedSinger = null;
        RefreshRotation();
        PersistShowState();
        SetStatus($"Removed {name} from this show. Saved singer/history data was not deleted.");
    }

    private void RefreshRotation()
    {
        var active = _rotation.Where(x => !x.IsHeld).ToArray();
        var total = active.Length;
        var nextPlayable = active.FirstOrDefault(x => x.Songs.Count > 0);

        for (var i = 0; i < active.Length; i++)
            active[i].SetRotationStanding(i + 1, total, ReferenceEquals(active[i], nextPlayable));
        foreach (var held in _rotation.Where(x => x.IsHeld))
            held.SetRotationStanding(null, total, false);

        _rotationAdapter.NotifyDataSetChanged();
    }

    private async Task RestoreShowStateAsync()
    {
        if (_showState is null) return;
        var restored = await _showState.LoadAsync();
        _rotation.Clear();
        _rotation.AddRange(restored);
        RefreshRotation();
        if (_rotation.Count > 0)
            SetStatus($"Restored {_rotation.Count:N0} singer(s) from the previous Android show.");
    }

    private void PersistShowState()
    {
        if (_showState is null) return;
        _ = _showState.SaveAsync(_rotation);
    }

    private async Task PlaySelectedAsync()
    {
        if (_selectedSong is null)
        {
            SetStatus("Select a Music, Music Video or directly playable Karaoke result first.");
            return;
        }
        if (_mediaRoots is null || _mediaPlayback is null) return;

        var sourcePath = _selectedSong.FilePath;
        var extension = System.IO.Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is ".zip" or ".cdg")
        {
            SetStatus("This is a CD+G karaoke track. Audio/CD+G playback is the next Android playback milestone.");
            return;
        }

        var uri = await _mediaRoots.ResolveAsync(sourcePath);
        if (uri is null)
        {
            var rootStatus = _mediaRoots.DescribeMappings();
            SetStatus(rootStatus.Contains("Unavailable / USB drive disconnected", StringComparison.OrdinalIgnoreCase)
                ? "Mapped USB/OTG storage is currently unavailable. Reconnect the drive, then try again."
                : $"Media file is not mapped on Android: {sourcePath}. Use MAP USB / MEDIA ROOT.");
            return;
        }

        try
        {
            if (IsVideoExtension(extension))
            {
                _mediaPlayback.Stop();
                _videoView.Visibility = ViewStates.Visible;
                _videoView.SetVideoURI(uri);
                _videoView.Start();
                SetStatus($"Playing video: {_selectedSong.Artist} — {_selectedSong.Title}");
            }
            else
            {
                try { _videoView.StopPlayback(); } catch { }
                _videoView.Visibility = ViewStates.Gone;
                await _mediaPlayback.PlayAsync(uri);
                SetStatus($"Playing: {_selectedSong.Artist} — {_selectedSong.Title}");
            }
        }
        catch (Exception ex)
        {
            SetStatus("Playback failed: " + ex.Message);
        }
    }

    private void TogglePlaybackPause()
    {
        try
        {
            if (_videoView.Visibility == ViewStates.Visible)
            {
                if (_videoView.IsPlaying) _videoView.Pause();
                else _videoView.Start();
                SetStatus(_videoView.IsPlaying ? "Video resumed" : "Video paused");
                return;
            }

            _mediaPlayback?.TogglePause();
            SetStatus(_mediaPlayback?.Status ?? "Stopped");
        }
        catch (Exception ex)
        {
            SetStatus("Playback control failed: " + ex.Message);
        }
    }

    private void StopPlayback()
    {
        try { _videoView.StopPlayback(); } catch { }
        _videoView.Visibility = ViewStates.Gone;
        _mediaPlayback?.Stop();
        SetStatus("Playback stopped");
    }

    private static bool IsVideoExtension(string extension)
        => extension is ".mp4" or ".m4v" or ".mkv" or ".avi" or ".wmv" or ".mov" or
            ".mpeg" or ".mpg" or ".vob" or ".ts" or ".m2ts" or ".webm" or ".divx";

    private void BeginMapMediaRoot()
    {
        var suggestion = _selectedSong is null
            ? string.Empty
            : AndroidMediaRootService.SuggestWindowsRoot(_selectedSong.FilePath);

        var input = new EditText(this) { Hint = @"Windows root, e.g. E:\Karaoke" };
        input.SetSingleLine(true);
        if (!string.IsNullOrWhiteSpace(suggestion)) input.Text = suggestion;

        new AlertDialog.Builder(this)
            .SetTitle("Map Windows root to Android / USB OTG")
            .SetMessage("Enter the Windows folder prefix stored in the imported Hazz database. Android will then open its folder picker. Choose the matching folder on internal storage, SD card, or the attached USB/OTG drive. Hazz will keep read access across restarts.")
            .SetView(input)
            .SetNegativeButton("Cancel", (_, _) => { })
            .SetPositiveButton("Choose Android Folder", (_, _) =>
            {
                var prefix = input.Text?.Trim() ?? string.Empty;
                if (prefix.Length == 0)
                {
                    SetStatus("Windows media root is required.");
                    return;
                }

                _pendingWindowsPrefix = prefix;
                var intent = new Intent(Intent.ActionOpenDocumentTree);
                intent.AddFlags(ActivityFlags.GrantReadUriPermission |
                                ActivityFlags.GrantWriteUriPermission |
                                ActivityFlags.GrantPersistableUriPermission |
                                ActivityFlags.GrantPrefixUriPermission);
                StartActivityForResult(intent, MapMediaRootRequest);
            })
            .Show();
    }

    private void ShowMediaRoots()
    {
        var roots = _mediaRoots?.DescribeMappings() ?? "No media roots mapped";
        new AlertDialog.Builder(this)
            .SetTitle("Android / USB OTG media roots")
            .SetMessage(roots)
            .SetNegativeButton("Close", (_, _) => { })
            .SetPositiveButton("Clear All", async (_, _) =>
            {
                if (_mediaRoots is null) return;
                await _mediaRoots.ClearAsync();
                SetStatus("All Android media-root mappings cleared.");
            })
            .Show();
    }

    private void BeginDatabaseImport()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        StartActivityForResult(intent, ImportDatabaseRequest);
    }

#pragma warning disable CS0672
    protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data)
#pragma warning restore CS0672
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (resultCode != Result.Ok || data?.Data is null) return;

        if (requestCode == MapMediaRootRequest)
        {
            try
            {
                if (_mediaRoots is null || string.IsNullOrWhiteSpace(_pendingWindowsPrefix)) return;
                var flags = data.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
                ContentResolver?.TakePersistableUriPermission(data.Data, flags);
                await _mediaRoots.AddOrReplaceAsync(_pendingWindowsPrefix, data.Data);
                var mapped = _pendingWindowsPrefix;
                SetStatus($"Mapped {mapped} to the selected Android/USB OTG folder. The mapping will be reused after restart.");
                _pendingWindowsPrefix = null;
            }
            catch (Exception ex)
            {
                SetStatus("Media-root mapping failed: " + ex.Message);
            }
            return;
        }

        if (requestCode != ImportDatabaseRequest) return;

        try
        {
            SetStatus("Importing Hazz database…");
            var temp = DatabasePath + ".import";
            await using (var input = ContentResolver!.OpenInputStream(data.Data)
                ?? throw new IOException("Android could not open the selected file."))
            await using (var output = File.Create(temp))
                await input.CopyToAsync(output);

            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                var target = DatabasePath + suffix;
                if (File.Exists(target)) File.Delete(target);
            }
            File.Move(temp, DatabasePath, true);
            await OpenDatabaseAsync();
            await RefreshLibraryStatusAsync();
            SetStatus("Hazz database imported. Windows media paths will need Android/USB remapping before playback.");
        }
        catch (Exception ex)
        {
            SetStatus("Database import failed: " + ex.Message);
        }
    }

    protected override void OnPause()
    {
        PersistShowState();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        try { _mediaPlayback?.Dispose(); } catch { }
        base.OnDestroy();
    }

    private void HideKeyboard()
    {
        var manager = GetSystemService(InputMethodService) as InputMethodManager;
        manager?.HideSoftInputFromWindow(_searchBox.WindowToken, HideSoftInputFlags.None);
    }

    private void SetStatus(string text)
    {
        if (_status is not null) _status.Text = text;
    }

    private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);

    private sealed class SearchResultAdapter : BaseAdapter<SongRecord>
    {
        private readonly MainActivity _activity;
        private readonly IList<SongRecord> _items;

        public SearchResultAdapter(MainActivity activity, IList<SongRecord> items)
        {
            _activity = activity;
            _items = items;
        }

        public override int Count => _items.Count;
        public override SongRecord this[int position] => _items[position];
        public override long GetItemId(int position) => _items[position].Id;

        public override View GetView(int position, View? convertView, ViewGroup? parent)
        {
            var song = _items[position];
            var row = new LinearLayout(_activity) { Orientation = Orientation.Vertical };
            row.SetPadding(_activity.Dp(8), _activity.Dp(6), _activity.Dp(8), _activity.Dp(6));
            row.SetBackgroundColor(position % 2 == 0 ? Color.Rgb(20, 28, 36) : Color.Rgb(27, 37, 48));

            var title = new TextView(_activity)
            {
                Text = $"{song.Artist} — {song.Title}",
                TextSize = 15
            };
            title.SetTextColor(Color.White);
            title.SetTypeface(null, global::Android.Graphics.TypefaceStyle.Bold);
            row.AddView(title);

            var details = string.Join(" • ", new[] { song.Manufacturer, song.DiscId, song.DurationText, song.Format }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            if (details.Length > 0)
            {
                var meta = new TextView(_activity) { Text = details, TextSize = 11 };
                meta.SetTextColor(Color.Rgb(166, 181, 196));
                row.AddView(meta);
            }
            return row;
        }
    }

    private sealed class SingerRotationAdapter : BaseAdapter<SingerQueueEntry>
    {
        private readonly MainActivity _activity;
        private readonly IList<SingerQueueEntry> _items;

        public SingerRotationAdapter(MainActivity activity, IList<SingerQueueEntry> items)
        {
            _activity = activity;
            _items = items;
        }

        public override int Count => _items.Count;
        public override SingerQueueEntry this[int position] => _items[position];
        public override long GetItemId(int position) => position;

        public override View GetView(int position, View? convertView, ViewGroup? parent)
        {
            var singer = _items[position];
            var row = new LinearLayout(_activity)
            {
                Orientation = Orientation.Vertical
            };
            row.SetMinimumHeight(_activity.Dp(58));
            row.SetPadding(_activity.Dp(8), _activity.Dp(6), _activity.Dp(8), _activity.Dp(6));

            if (singer.IsNextSinger)
                row.SetBackgroundColor(Color.Rgb(82, 69, 18));
            else if (singer.IsHeld)
                row.SetBackgroundColor(Color.Rgb(45, 45, 48));
            else
                row.SetBackgroundColor(position % 2 == 0 ? Color.Rgb(21, 30, 39) : Color.Rgb(27, 37, 48));

            var top = new TextView(_activity)
            {
                Text = $"{singer.RotationBadgeText}   {singer.SingerName}",
                TextSize = 16
            };
            top.SetTextColor(singer.IsNextSinger ? Color.Rgb(255, 215, 77) : Color.White);
            top.SetTypeface(null, global::Android.Graphics.TypefaceStyle.Bold);
            row.AddView(top);

            var next = singer.NextSong is null
                ? "No song selected"
                : $"{singer.NextSongTitle} — {singer.NextArtist}   •   {singer.SongCountText}";
            var bottom = new TextView(_activity) { Text = next, TextSize = 12 };
            bottom.SetTextColor(Color.Rgb(173, 188, 202));
            row.AddView(bottom);
            return row;
        }
    }

}
