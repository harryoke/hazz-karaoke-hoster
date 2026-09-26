using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HazzKaraokeHoster.Data;

namespace HazzKaraokeHoster.App;
public partial class MainWindow
{
    private void LibraryHealth_Click(object sender, RoutedEventArgs e)
    {
        var window = new Window { Owner = this, Title = "Library Health Centre", Width = Math.Min(1050,SystemParameters.WorkArea.Width-30), Height = Math.Min(650,SystemParameters.WorkArea.Height-40), MinWidth = 650, MinHeight = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var service = new LibraryHealthService(_db);
        var panel = new DockPanel { Margin = new Thickness(12) }; window.Content = panel;
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); panel.Children.Add(header);
        var status = new TextBlock { Text = "Scan checks indexed files only. No files are deleted or rewritten. ZIP checks can take time; use outside a live show.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,8) }; header.Children.Add(status);
        var buttons = new WrapPanel(); header.Children.Add(buttons);
        Button Button(string text) { var b = new Button { Content = text, Padding = new Thickness(10,6,10,6), Margin = new Thickness(3) }; buttons.Children.Add(b); return b; }
        var scan = Button("SCAN LIBRARY"); var cancel = Button("CANCEL SCAN"); var preview = Button("PREVIEW RECONNECT"); var apply = Button("CONFIRM RECONNECT"); var remove = Button("REMOVE SELECTED DATABASE ENTRIES");
        cancel.IsEnabled = preview.IsEnabled = apply.IsEnabled = remove.IsEnabled = false;
        var roots = new WrapPanel(); header.Children.Add(roots);
        roots.Children.Add(new TextBlock { Text = "Old folder / drive:", VerticalAlignment = VerticalAlignment.Center });
        var oldRoot = new TextBox { Width = 260, Margin = new Thickness(5) }; roots.Children.Add(oldRoot);
        roots.Children.Add(new TextBlock { Text = "New folder / drive:", VerticalAlignment = VerticalAlignment.Center });
        var newRoot = new TextBox { Width = 260, Margin = new Thickness(5) }; roots.Children.Add(newRoot);
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Extended, EnableRowVirtualization = true, EnableColumnVirtualization = true };
        VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling);
        panel.Children.Add(grid);
        void Columns(params (string Header,string Property)[] columns)
        { grid.Columns.Clear(); foreach (var c in columns) grid.Columns.Add(new DataGridTextColumn { Header = c.Header, Binding = new Binding(c.Property), Width = new DataGridLength(1,DataGridLengthUnitType.Star), MinWidth = 90 }); }
        bool applying = false;
        window.Closing += (_,args) => { if (applying) args.Cancel = true; };
        List<LibraryHealthIssue> issues = new(); List<LibraryRelink> repairs = new(); CancellationTokenSource? operation = null;
        void Findings() { Columns(("Problem","Problem"),("Artist","Artist"),("Title","Title"),("Path","Path"),("Details","Detail")); grid.ItemsSource = issues; }
        void Busy(bool busy) { scan.IsEnabled = !busy; cancel.IsEnabled = busy; preview.IsEnabled = remove.IsEnabled = !busy && issues.Count > 0; apply.IsEnabled = !busy && repairs.Count > 0; }
        void Invalidate() { repairs.Clear(); apply.IsEnabled = false; }
        oldRoot.TextChanged += (_,_) => Invalidate(); newRoot.TextChanged += (_,_) => Invalidate();
        window.Closed += (_,_) => { cts.Cancel(); operation?.Cancel(); };
        cancel.Click += (_,_) => operation?.Cancel();
        scan.Click += async (_,_) =>
        {
            operation = CancellationTokenSource.CreateLinkedTokenSource(cts.Token); var token = operation.Token;
            Invalidate(); Busy(true); var progress = new Progress<string>(s => status.Text = s);
            try { issues = await Task.Run(() => service.ScanAsync(progress, token), token); Findings(); }
            catch (OperationCanceledException) { status.Text = "Scan cancelled. Nothing changed."; }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { operation.Dispose(); operation = null; Busy(false); }
        };
        preview.Click += async (_,_) =>
        {
            var from = oldRoot.Text.Trim(); var to = newRoot.Text.Trim();
            operation = CancellationTokenSource.CreateLinkedTokenSource(cts.Token); var token = operation.Token; Busy(true);
            try
            {
                repairs = await Task.Run(() => issues.Where(i => i.Problem == "Missing file").DistinctBy(i => i.SongId).Select(i =>
                {
                    token.ThrowIfCancellationRequested(); var target = LibraryHealthService.MappedPath(i.Path, from, to);
                    return target is not null && File.Exists(target) && (i.Size <= 0 || new FileInfo(target).Length == i.Size) ? new LibraryRelink(i.SongId, i.Path, target) : null;
                }).OfType<LibraryRelink>().ToList(), token);
                Columns(("Old path","OldPath"),("Proposed new path","NewPath")); grid.ItemsSource = repairs;
                status.Text = $"{repairs.Count:N0} reconnections proposed, matching relative path and recorded size. Review every row. No changes until CONFIRM. Different-size or missing targets are excluded. To return to findings, scan again.";
            }
            catch (OperationCanceledException) { status.Text = "Preview cancelled."; }
            catch (Exception ex) { Invalidate(); status.Text = ex.Message; }
            finally { operation.Dispose(); operation = null; Busy(false); remove.IsEnabled = false; }
        };
        apply.Click += async (_,_) =>
        {
            if (repairs.Count == 0) return;
            if (_karaokePlaying || _karaokePaused || _activeMusicDeck != MusicDeckId.None || _deck1Paused || _deck2Paused || _quickSearchMusicActive)
            { status.Text = "Stop all players before applying repairs."; return; }
            if (MessageBox.Show(window, $"Reconnect these {repairs.Count:N0} entries? Library IDs, virtual-folder membership and database history are preserved. Original files are untouched.", "Confirm reviewed repairs", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            Busy(true); cancel.IsEnabled = false; applying = true; var batch = repairs.ToArray();
            try
            {
                await Task.Run(() => service.ApplyRelinksAsync(batch, cts.Token), cts.Token);
                var preferenceWarning = false;
                var map = batch.ToDictionary(r => r.OldPath,r => r.NewPath,StringComparer.OrdinalIgnoreCase);
                foreach (var item in DeckAPlaylist.Items.OfType<MusicQueueItem>().Concat(DeckBPlaylist.Items.OfType<MusicQueueItem>()))
                    if (map.TryGetValue(item.FilePath,out var destination)) item.RelinkPath(destination);
                foreach (var song in _queue.SelectMany(s => s.Songs))
                    if (map.TryGetValue(song.FilePath,out var destination)) song.FilePath = destination;
                try { MusicFavourites.Default.CopyPaths(map); TempoPreferences.Default.CopyPaths(map); }
                catch (Exception ex) { App.WriteDiagnostic("RELINK PREFERENCES", ex.ToString()); preferenceWarning = true; }
                foreach (var r in batch)
                {
                    if (_cdgSongPresentation.TryGetValue(r.OldPath,out var presentation)) _cdgSongPresentation[r.NewPath] = presentation;
                    if (_savedAvSync.TryGetValue(r.OldPath,out var sync)) _savedAvSync[r.NewPath] = sync;
                }
                MarkLiveShowStateDirty(); MarkMusicDeckQueuesDirty(); SaveMainLayout(); _searchCts?.Cancel(); SearchGrid.ItemsSource = null;
                status.Text = $"Reconnected {batch.Length:N0} files. Rescan to verify. Reload any previously loaded deck track; old saved venue snapshots and external playlists retain their original paths.";
                if (preferenceWarning) status.Text += " Some separate preferences could not be copied; see Logs. Original preferences remain intact.";
                issues.Clear(); Invalidate(); Findings();
            }
            catch (Exception ex) { status.Text = "Repair: " + ex.Message + " Scan again before retrying."; }
            finally { applying = false; Busy(false); }
        };
        remove.Click += async (_,_) =>
        {
            var rows = grid.SelectedItems.OfType<LibraryHealthIssue>().DistinctBy(i => i.SongId).ToArray();
            if (rows.Length == 0) { status.Text = "Select the entries to remove first. Possible duplicates may be different versions."; return; }
            if (MessageBox.Show(window, $"Remove {rows.Length:N0} selected entries from the library and virtual folders? History text remains. No media files are deleted. Different recordings can have identical names: check the paths first.", "Confirm selected database removals", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            Busy(true);
            try { await Task.Run(() => _library.DeleteSongsAsync(rows.Select(r => r.SongId),cts.Token),cts.Token); var ids = rows.Select(r=>r.SongId).ToHashSet(); issues.RemoveAll(i=>ids.Contains(i.SongId)); Findings(); _searchCts?.Cancel(); RemoveSongsFromVisibleSearch(ids); status.Text = "Selected database entries removed. Files on disk unchanged."; }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { Busy(false); }
        };
        window.ShowDialog();
    }
}
