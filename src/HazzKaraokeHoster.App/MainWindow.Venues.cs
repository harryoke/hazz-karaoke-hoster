using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using HazzKaraokeHoster.Playback;
using HazzKaraokeHoster.Data;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private sealed class VenueProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public UiLayoutSettings Layout { get; set; } = new();
        public SoundRoutes Routes { get; set; } = new();
        public bool Normalize { get; set; }
        public double TargetDb { get; set; } = -18;
        public double Deck1Volume { get; set; } = 0.85;
        public double Deck2Volume { get; set; } = 0.85;
        public bool AutoCrossfade { get; set; } = true;
        public double CrossfadeSeconds { get; set; } = 4;
        public int DisplayIndex { get; set; } = -1;
        public Guid? SingerSnapshot { get; set; }
        public Guid? LastAutomaticSnapshot { get; set; }
        public List<LiveShowSingerSnapshot> SingerRoster { get; set; } = new();
        public Dictionary<string, string> SingerGroups { get; set; } = new();
    }

    private string VenueProfilesPath => Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!, "venue-profiles.json");

    private async void VenueProfiles_Click(object sender, RoutedEventArgs e)
    {
        if (_venueDialogOpen || _venueClosing) return;
        _venueDialogOpen = true;
        try { ShowVenueProfiles(); }
        catch (Exception ex) { App.WriteDiagnostic("VENUE PROFILES", ex.ToString()); MessageBox.Show(this, ex.Message, "Venue Profiles"); }
        finally { _venueDialogOpen = false; }
    }

    private void ShowVenueProfiles()
    {
        var profiles = File.Exists(VenueProfilesPath)
            ? JsonSerializer.Deserialize<List<VenueProfile>>(File.ReadAllText(VenueProfilesPath)) ?? new()
            : new List<VenueProfile>();
        var window = new Window { Owner = this, Title = "Venue Profiles", Width = 680, Height = 680, MinWidth = 400, MinHeight = 360, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var busy = false;
        window.Closing += (_, e) => { if (busy) e.Cancel = true; };
        var panel = new StackPanel { Margin = new Thickness(18) };
        window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "Save the current setup for each venue, then select a profile and apply it before your show.", TextWrapping = TextWrapping.Wrap });
        var list = new ListBox { Height = 160, Margin = new Thickness(0,12,0,12), DisplayMemberPath = "Name" };
        panel.Children.Add(list);
        panel.Children.Add(new TextBlock { Text = "Venue name" });
        var name = new TextBox { MaxLength = 80, MinHeight = 30 }; panel.Children.Add(name);
        var buttons = new WrapPanel { Margin = new Thickness(0,12,0,12) }; panel.Children.Add(buttons);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap }; panel.Children.Add(status);
        status.Text = _activeSingerVenue is Guid active ? "Automatic singer saving: " + (profiles.FirstOrDefault(x => x.Id == active)?.Name ?? "unknown venue") : "No active singer venue. Save or load a singer list to enable automatic saving.";
        panel.Children.Add(new TextBlock { Text = "SAVE CURRENT / APPLY manage settings. SAVE SINGER LIST saves now. The active singer venue also saves before changing lists and at shutdown; there is no timed saving. Starting blank or restoring a backup disconnects the active venue until you save or load a named list. The music library stays shared.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,0) });
        void Refresh(Guid? selected = null) { list.ItemsSource = profiles.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList(); list.SelectedItem = profiles.FirstOrDefault(x => x.Id == selected); }
        void Persist()
        {
            var temp = VenueProfilesPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, VenueProfilesPath, true);
        }
        string ValidName()
        {
            var value = name.Text.Trim();
            if (value.Length == 0) throw new InvalidOperationException("Enter a venue name first.");
            return value;
        }
        void Button(string label, Action action)
        {
            var button = new Button { Content = label, MinHeight = 32, Margin = new Thickness(0,0,6,6), Padding = new Thickness(10,4,10,4) };
            button.Click += (_, _) => { try { action(); } catch (Exception ex) { status.Text = ex.Message; App.WriteDiagnostic("VENUE PROFILE ACTION", ex.ToString()); } };
            buttons.Children.Add(button);
        }
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is VenueProfile profile) name.Text = profile.Name; };
        Button("SAVE CURRENT", () =>
        {
            var title = ValidName();
            var existing = profiles.FirstOrDefault(x => string.Equals(x.Name, title, StringComparison.OrdinalIgnoreCase));
            if (existing is not null && MessageBox.Show(window, $"Replace the saved settings for '{title}' with the current setup?", "Update Venue", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            SaveMainLayout();
            var profile = new VenueProfile { Id = existing?.Id ?? Guid.NewGuid(), Name = title, Layout = UiLayoutSettingsStore.Load(), Routes = JsonSerializer.Deserialize<SoundRoutes>(JsonSerializer.Serialize(_soundRoutes))!, Normalize = AudioNormalization.Enabled, TargetDb = AudioNormalization.TargetDb, Deck1Volume = DeckAVolume.Value, Deck2Volume = DeckBVolume.Value, AutoCrossfade = AutoCrossfadeCheck.IsChecked == true, CrossfadeSeconds = CrossfadeSecondsSlider.Value, DisplayIndex = DisplayCombo.SelectedIndex };
            profile.SingerSnapshot = existing?.SingerSnapshot;
            profile.LastAutomaticSnapshot = existing?.LastAutomaticSnapshot;
            profile.SingerRoster = existing?.SingerRoster ?? new();
            profile.SingerGroups = existing?.SingerGroups ?? new();
            if (existing is not null) profiles.Remove(existing);
            profiles.Add(profile); Persist(); Refresh(profile.Id); status.Text = "Saved " + title;
        });
        Button("APPLY", () =>
        {
            if (list.SelectedItem is not VenueProfile profile) throw new InvalidOperationException("Select a venue first.");
            if (_activeMusicDeck != MusicDeckId.None || _quickSearchMusicActive || _quickSearchMusicFadeInActive || _karaokePlaying || _karaokePaused || _crossfadeActive || _musicFadeOutForKaraoke)
                throw new InvalidOperationException("Stop all players before applying a venue profile.");
            UiLayoutSettingsStore.Save(profile.Layout);
            RestoreMainLayout();
            DeckAMedia.Close(); DeckBMedia.Close(); QuickMusicMedia.Close(); StandbyMusicMedia.Close();
            _cueGeneration++; _cuePath = null; _cueReady = false;
            _soundRoutes = JsonSerializer.Deserialize<SoundRoutes>(JsonSerializer.Serialize(profile.Routes))!;
            File.WriteAllText(SoundRoutesPath, JsonSerializer.Serialize(_soundRoutes));
            ApplySoundRoutes();
            _pitchAudio.Dispose();
            KaraokeMedia.Stop();
            KaraokeMedia.Source = null;
            AudioNormalization.Enabled = profile.Normalize;
            AudioNormalization.TargetDb = profile.TargetDb;
            File.WriteAllText(NormalizationPath, JsonSerializer.Serialize(new NormalizationSettings { Enabled = profile.Normalize, TargetDb = profile.TargetDb }));
            DeckAVolume.Value = Math.Clamp(profile.Deck1Volume, 0, 1); DeckBVolume.Value = Math.Clamp(profile.Deck2Volume, 0, 1);
            AutoCrossfadeCheck.IsChecked = profile.AutoCrossfade;
            CrossfadeSecondsSlider.Value = Math.Clamp(profile.CrossfadeSeconds, CrossfadeSecondsSlider.Minimum, CrossfadeSecondsSlider.Maximum);
            if (profile.DisplayIndex >= 0 && profile.DisplayIndex < DisplayCombo.Items.Count) DisplayCombo.SelectedIndex = profile.DisplayIndex;
            SaveMainLayout();
            var missing = new[] { profile.Layout.AudienceLogoImagePath, profile.Layout.AudienceBackgroundImagePath, profile.Layout.AudienceBackgroundFolderPath }.Where(x => !string.IsNullOrWhiteSpace(x) && !File.Exists(x) && !Directory.Exists(x)).ToArray();
            status.Text = "Applied " + profile.Name + ". Reload any already-loaded karaoke track before playing." + (missing.Length > 0 ? " Missing artwork: " + string.Join(", ", missing) : "");
        });
        Button("RENAME", () =>
        {
            if (list.SelectedItem is not VenueProfile profile) throw new InvalidOperationException("Select a venue first.");
            var title = ValidName();
            if (profiles.Any(x => x.Id != profile.Id && string.Equals(x.Name, title, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("That venue name already exists.");
            profile.Name = title; Persist(); Refresh(profile.Id); status.Text = "Renamed " + title;
        });
        Button("DELETE", () =>
        {
            if (list.SelectedItem is not VenueProfile profile) throw new InvalidOperationException("Select a venue first.");
            if (MessageBox.Show(window, $"Delete saved profile '{profile.Name}'? Current settings and media remain unchanged.", "Delete Venue", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            if (_activeSingerVenue == profile.Id) SetActiveSingerVenue(null);
            profiles.Remove(profile); Persist(); Refresh(); status.Text = "Profile deleted.";
        });
        var history = new CheckBox { Content = "Load saved song history (untick to start history fresh)", IsChecked = true, Margin = new Thickness(0,12,0,8) };
        panel.Children.Add(history);
        var singerButtons = new WrapPanel(); panel.Children.Add(singerButtons);
        var store = new VenueSingerStore(_db);
        var folder = Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!, "venue-singers");
        string SnapshotPath(Guid id) => Path.Combine(folder, id.ToString("N") + ".db");
        void EnsureStopped()
        {
            if (_activeMusicDeck != MusicDeckId.None || _quickSearchMusicActive || _quickSearchMusicFadeInActive || _karaokePlaying || _karaokePaused || _crossfadeActive || _musicFadeOutForKaraoke)
                throw new InvalidOperationException("Stop all players before changing singer lists.");
        }
        void SingerButton(string label, Func<Task> action)
        {
            var button = new Button { Content = label, MinHeight = 32, Margin = new Thickness(0,0,6,6), Padding = new Thickness(10,4,10,4) };
            button.Click += async (_, _) =>
            {
                if (busy) return;
                busy = true; panel.IsEnabled = false;
                try
                {
                    EnsureStopped();
                    var selected = (list.SelectedItem as VenueProfile)?.Id;
                    await SaveActiveVenueAsync();
                    profiles = File.Exists(VenueProfilesPath) ? JsonSerializer.Deserialize<List<VenueProfile>>(File.ReadAllText(VenueProfilesPath)) ?? new() : new();
                    Refresh(selected);
                    await action();
                }
                catch (Exception ex) { status.Text = ex.Message; App.WriteDiagnostic("VENUE SINGERS", ex.ToString()); }
                finally { busy = false; panel.IsEnabled = true; }
            };
            singerButtons.Children.Add(button);
        }
        SingerButton("SAVE SINGER LIST", async () =>
        {
            if (list.SelectedItem is not VenueProfile profile) throw new InvalidOperationException("Select a venue, or create it with SAVE CURRENT first.");
            if (profile.SingerSnapshot is not null && MessageBox.Show(window, "Update this venue's saved singers and history from the current list?", "Save Venue Singers", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            var id = Guid.NewGuid();
            var roster = CaptureVenueRoster();
            await store.SaveAsync(SnapshotPath(id));
            var oldId = profile.SingerSnapshot; var oldRoster = profile.SingerRoster;
            var oldGroups = profile.SingerGroups;
            profile.SingerSnapshot = id; profile.SingerRoster = roster;
            profile.SingerGroups = _fairTurns.ToDictionary(x => x.Key, x => x.Value.Group);
            try { Persist(); }
            catch { profile.SingerSnapshot = oldId; profile.SingerRoster = oldRoster; profile.SingerGroups = oldGroups; throw; }
            SetActiveSingerVenue(profile.Id);
            status.Text = "Saved singers for " + profile.Name + ". Automatic saving is now active.";
        });
        async Task ReplaceSingers(VenueProfile? profile)
        {
            var path = profile is null ? null : profile.SingerSnapshot is Guid id ? SnapshotPath(id) : throw new InvalidOperationException("This profile has no singer list. Use SAVE SINGER LIST first.");
            if (path is not null && !File.Exists(path)) throw new FileNotFoundException("The saved singer file is missing.", path);
            if (MessageBox.Show(window, "Replace the current dropdown names, history and singer queue? A singer backup is created first. Save the current venue first if you want to load it by name later.", "Change Venue Singers", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var backup = Path.Combine(folder, "backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".db");
            await store.SaveAsync(backup);
            File.WriteAllText(backup + ".queue.json", JsonSerializer.Serialize(CaptureVenueRoster()));
            SetActiveSingerVenue(null);
            await store.ReplaceAsync(path, history.IsChecked == true);
            RestoreVenueRoster(profile?.SingerRoster ?? new());
            if (profile is not null)
                foreach (var singer in _queue)
                    if (profile.SingerGroups.TryGetValue(FairSingerKey(singer), out var group)) FairRecord(singer).Group = group;
            MarkLiveShowStateDirty(); SaveLiveShowStateNow(false);
            if (profile is not null) SetActiveSingerVenue(profile.Id);
            await RefreshSavedSingerNamesAsync();
            status.Text = (profile is null ? "Started a blank singer list." : "Loaded singers for " + profile.Name + (history.IsChecked == true ? " with history." : " with fresh history.")) + " Backup: " + backup;
        }
        SingerButton("LOAD SINGER LIST", async () =>
        {
            if (list.SelectedItem is not VenueProfile profile) throw new InvalidOperationException("Select a venue first.");
            await ReplaceSingers(profile);
        });
        SingerButton("START BLANK SINGER LIST", () => ReplaceSingers(null));
        SingerButton("USE WITHOUT VENUE", () =>
        {
            SetActiveSingerVenue(null);
            status.Text = "No active venue. Current names and history continue saving in the main database.";
            return Task.CompletedTask;
        });
        SingerButton("KEEP SELECTED SINGERS", async () =>
        {
            var names = await Task.Run(() => _singers.GetSingersAsync(int.MaxValue));
            if (names.Count >= 100000) throw new InvalidOperationException("This list is too large for selective removal. No singers have been changed.");
            var choose = new Window { Owner = window, Title = "Select singers to keep — Ctrl/Shift for multiple", Width = 440, Height = 500, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var dock = new DockPanel { Margin = new Thickness(12) }; choose.Content = dock;
            var accept = new Button { Content = "KEEP SELECTED — REMOVE OTHERS", MinHeight = 36 };
            DockPanel.SetDock(accept, Dock.Bottom); dock.Children.Add(accept);
            var selection = new ListBox { ItemsSource = names, DisplayMemberPath = "DisplayName", SelectionMode = SelectionMode.Extended };
            VirtualizingPanel.SetIsVirtualizing(selection, true); VirtualizingPanel.SetVirtualizationMode(selection, VirtualizationMode.Recycling);
            dock.Children.Add(selection); accept.Click += (_, _) => choose.DialogResult = true;
            if (choose.ShowDialog() != true) return;
            var keep = selection.SelectedItems.Cast<Singer>().Select(x => x.Id).ToHashSet();
            if (MessageBox.Show(window, $"Keep {keep.Count} singers and delete all other current singer names and their history? A backup is saved first.", "Keep Singers", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            var backup = Path.Combine(folder, "backups", Guid.NewGuid().ToString("N") + ".db");
            await store.SaveAsync(backup);
            var roster = CaptureVenueRoster(); File.WriteAllText(backup + ".queue.json", JsonSerializer.Serialize(roster));
            await store.KeepAsync(keep);
            RestoreVenueRoster(roster.Where(x => x.SingerId is long id && keep.Contains(id)).ToList());
            await RefreshSavedSingerNamesAsync(); status.Text = "Kept selected singers. Save this list to the desired venue. Backup: " + backup;
        });
        SingerButton("CLEAR HISTORY ONLY", async () =>
        {
            if (MessageBox.Show(window, "Delete all current permanent singer history, keeping names and the current queue? A backup is saved first.", "Clear History", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            var backup = Path.Combine(folder, "backups", Guid.NewGuid().ToString("N") + ".db");
            await store.SaveAsync(backup); await store.KeepAsync(Array.Empty<long>(), true);
            status.Text = "History cleared; saved venue snapshots are unchanged. Backup: " + backup;
        });
        SingerButton("RESTORE SINGER BACKUP", async () =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Restore singer backup", Filter = "Singer snapshot (*.db)|*.db", InitialDirectory = Path.Combine(folder, "backups") };
            if (dialog.ShowDialog(window) != true) return;
            var rosterPath = dialog.FileName + ".queue.json";
            var roster = File.Exists(rosterPath) ? JsonSerializer.Deserialize<List<LiveShowSingerSnapshot>>(File.ReadAllText(rosterPath)) ?? new() : new List<LiveShowSingerSnapshot>();
            if (MessageBox.Show(window, "Restore the selected singer backup, replacing current singers and history? Current singers will be backed up first.", "Restore Backup", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            var backup = Path.Combine(folder, "backups", Guid.NewGuid().ToString("N") + ".db");
            await store.SaveAsync(backup); File.WriteAllText(backup + ".queue.json", JsonSerializer.Serialize(CaptureVenueRoster()));
            SetActiveSingerVenue(null);
            await store.ReplaceAsync(dialog.FileName, true); RestoreVenueRoster(roster); await RefreshSavedSingerNamesAsync(); status.Text = "Singer backup restored. Save to a venue to enable automatic saving.";
        });
        Refresh(); window.ShowDialog();
    }

    private List<LiveShowSingerSnapshot> CaptureVenueRoster() => _queue.Select(s => new LiveShowSingerSnapshot
    {
        QueueId = s.Id, SingerId = s.SingerId, SingerName = s.SingerName, IsHeld = s.IsHeld,
        Songs = s.Songs.Select(x => new LiveShowSongSnapshot { QueueSongId = x.Id, SongId = x.SongId, SongTitle = x.SongTitle, Artist = x.Artist, FilePath = x.FilePath, KeyChange = x.KeyChange, CdgSyncSeconds = x.CdgSyncSeconds }).ToList()
    }).ToList();

    private void RestoreVenueRoster(List<LiveShowSingerSnapshot> roster)
    {
        _pitchAudio.Dispose(); KaraokeMedia.Stop(); KaraokeMedia.Source = null;
        _karaokePackage?.Dispose(); _karaokePackage = null; _cdgDecoder = null; _cdgBitmap = null; CdgPreview.Source = null;
        _karaokePresentationActive = false; _audience?.SetKaraokeActive(false); _audience?.ClearKaraokeVisual();
        KaraokeNowText.Text = "No karaoke track loaded";
        _activeSinger = null; _activeSingerSong = null; _activeSingerSongCheckedOut = false; _currentKaraokeRecord = null;
        _kamikazeSinger = null; _kamikazeAssignedSong = null; _kamikazeBannerRequested = false;
        _audience?.HideKamikazeBanner();
        _fairTurns.Clear(); _fairSequence = 0; _rotationRound = new();
        _queue.Clear();
        foreach (var saved in roster)
        {
            var singer = new SingerQueueEntry { Id = saved.QueueId, SingerId = saved.SingerId, SingerName = saved.SingerName, IsHeld = saved.IsHeld };
            foreach (var x in saved.Songs) singer.Songs.Add(new SingerSongEntry { Id = x.QueueSongId, SongId = x.SongId, SongTitle = x.SongTitle, Artist = x.Artist, FilePath = x.FilePath, KeyChange = x.KeyChange, CdgSyncSeconds = x.CdgSyncSeconds });
            _queue.Add(singer);
        }
        SingerNameBox.Text = ""; ManualSongBox.Text = "";
        _showStartedUtc = DateTimeOffset.UtcNow;
        QueueList.SelectedItem = _queue.FirstOrDefault();
        UpdateAudienceNext(); MarkLiveShowStateDirty(); SaveLiveShowStateNow(false);
    }
}
