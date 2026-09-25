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

    private readonly List<SingerQueueEntry> _rotation = new();
    private IReadOnlyList<SongRecord> _searchResults = Array.Empty<SongRecord>();
    private SingerQueueEntry? _selectedSinger;
    private SongRecord? _selectedSong;
    private string _searchKind = "Karaoke";

    private HazzDatabase? _database;
    private ILibraryRepository? _library;
    private ISingerRepository? _singers;

    private TextView _status = null!;
    private TextView _modeLabel = null!;
    private EditText _searchBox = null!;
    private ListView _rotationList = null!;
    private ListView _searchList = null!;
    private SingerRotationAdapter _rotationAdapter = null!;
    private ArrayAdapter<string> _searchAdapter = null!;

    private string DatabasePath => Path.Combine(FilesDir!.AbsolutePath, "hazz-hoster.db");

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.SetSoftInputMode(SoftInput.AdjustResize);
        SetContentView(BuildUi());

        try
        {
            await OpenDatabaseAsync();
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
            Orientation = Orientation.Vertical,
            SetPadding = { }
        };
        root.SetPadding(Dp(10), Dp(8), Dp(10), Dp(8));
        root.SetBackgroundColor(Color.Rgb(11, 15, 20));

        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal, Gravity = GravityFlags.CenterVertical };
        var title = MakeText("HAZZ KARAOKE HOSTER • ANDROID v0.1", 20, true, Color.White);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
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
            SingleLine = true,
            TextSize = 16
        };
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
        _searchAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleListItemActivated1, new List<string>());
        _searchList.Adapter = _searchAdapter;
        _searchList.ItemClick += (_, e) =>
        {
            if (e.Position < 0 || e.Position >= _searchResults.Count) return;
            _selectedSong = _searchResults[e.Position];
            SetStatus($"Selected: {_selectedSong.Artist} — {_selectedSong.Title}");
        };
        panel.AddView(_searchList, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        var footer = MakeText(
            "v0.1: shared Hazz SQLite/search/singer engine is live. Android playback, CD+G, Storage Access Framework media roots and HDMI audience output are the next platform layer.",
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
            AllCaps = false,
            MinHeight = Dp(42)
        };
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
        if (bold) view.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
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
            _searchResults = await _library.SearchByKindAsync(query, _searchKind, 250);
            _selectedSong = null;

            _searchAdapter.Clear();
            foreach (var song in _searchResults)
            {
                var details = string.Join(" • ", new[] { song.Manufacturer, song.DiscId, song.DurationText }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
                _searchAdapter.Add($"{song.Artist} — {song.Title}" + (details.Length > 0 ? $"\n{details}" : string.Empty));
            }
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
        _searchResults = Array.Empty<SongRecord>();
        _selectedSong = null;
        _searchAdapter.Clear();
        _searchAdapter.NotifyDataSetChanged();
        SetStatus($"{_modeLabel.Text} ready");
    }

    private void ShowAddSingerDialog()
    {
        var input = new EditText(this) { Hint = "Singer name", SingleLine = true };
        var dialog = new AlertDialog.Builder(this)
            .SetTitle("Add singer")
            .SetView(input)
            .SetNegativeButton("Cancel", (_, _) => { })
            .SetPositiveButton("Add", null)
            .Create();

        dialog.SetOnShowListener(new DialogShowListener(() =>
        {
            var positive = dialog.GetButton((int)DialogButtonType.Positive);
            positive.Click += async (_, _) =>
            {
                var name = input.Text?.Trim() ?? string.Empty;
                if (name.Length == 0)
                {
                    input.Error = "Singer name is required";
                    return;
                }
                await AddSingerAsync(name);
                dialog.Dismiss();
            };
        }));
        dialog.Show();
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
        SetStatus($"Added {_selectedSong.Title} to {_selectedSinger.SingerName}");
    }

    private void ToggleHold()
    {
        if (_selectedSinger is null) { SetStatus("Select a singer first."); return; }
        _selectedSinger.IsHeld = !_selectedSinger.IsHeld;
        RefreshRotation();
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
        _rotationList.SetSelection(target);
    }

    private void RemoveSinger()
    {
        if (_selectedSinger is null) { SetStatus("Select a singer first."); return; }
        var name = _selectedSinger.SingerName;
        _rotation.Remove(_selectedSinger);
        _selectedSinger = null;
        RefreshRotation();
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
        if (requestCode != ImportDatabaseRequest || resultCode != Result.Ok || data?.Data is null) return;

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
                Orientation = Orientation.Vertical,
                MinimumHeight = _activity.Dp(58)
            };
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
            top.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
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

    private sealed class DialogShowListener(Action onShow) : Java.Lang.Object, Android.Content.IDialogInterfaceOnShowListener
    {
        public void OnShow(Android.Content.IDialogInterface? dialog) => onShow();
    }
}
