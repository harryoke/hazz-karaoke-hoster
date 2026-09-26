using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HazzKaraokeHoster.Core;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;
using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace HazzKaraokeHoster.App;

internal sealed class LibraryHealthIssue
{
    public required long SongId { get; init; }
    public required string Type { get; init; }
    public required string Severity { get; init; }
    public required string Artist { get; init; }
    public required string Title { get; init; }
    public required string Manufacturer { get; init; }
    public required string Disc { get; init; }
    public required string FilePath { get; init; }
    public required string Details { get; init; }
}

internal sealed class LibraryHealthWindow : Window
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".aif", ".aiff" };

    private readonly HazzDatabase _database;
    private readonly ILibraryRepository _library;
    private readonly ObservableCollection<LibraryHealthIssue> _issues = new();
    private readonly DataGrid _grid;
    private readonly TextBlock _status;
    private readonly ProgressBar _progress;
    private readonly Button _scanButton;
    private readonly Button _cancelButton;
    private CancellationTokenSource? _scanCts;

    public LibraryHealthWindow(Window owner, HazzDatabase database, ILibraryRepository library)
    {
        Owner = owner;
        _database = database;
        _library = library;
        Title = "Hazz Library Health Centre";
        Width = 1220;
        Height = 720;
        MinWidth = 850;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Thickness(12) };
        Content = root;

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(header, Dock.Top);
        header.Children.Add(new TextBlock
        {
            Text = "LIBRARY HEALTH CENTRE",
            FontSize = 24,
            FontWeight = FontWeights.Bold
        });
        header.Children.Add(new TextBlock
        {
            Text = "Scans the Hazz karaoke index for missing files, broken MP3+CDG pairs, damaged/invalid ZIP packages, likely duplicate copies, missing metadata and missing duration. Nothing is changed automatically.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 8)
        });

        var commands = new WrapPanel();
        _scanButton = Button("SCAN KARAOKE LIBRARY", (_, _) => _ = ScanAsync());
        _cancelButton = Button("CANCEL SCAN", (_, _) => _scanCts?.Cancel());
        _cancelButton.IsEnabled = false;
        var relink = Button("RELINK SELECTED", async (_, _) => await RelinkSelectedAsync());
        var duration = Button("READ DURATION", async (_, _) => await ProbeDurationSelectedAsync());
        var delete = Button("REMOVE FROM DATABASE", async (_, _) => await DeleteSelectedAsync());
        var folder = Button("OPEN FOLDER", (_, _) => OpenSelectedFolder());
        commands.Children.Add(_scanButton);
        commands.Children.Add(_cancelButton);
        commands.Children.Add(relink);
        commands.Children.Add(duration);
        commands.Children.Add(delete);
        commands.Children.Add(folder);
        header.Children.Add(commands);

        _progress = new ProgressBar { Minimum = 0, Maximum = 1, Height = 16, Margin = new Thickness(0, 8, 0, 4) };
        _status = new TextBlock { Text = "Ready to scan.", TextWrapping = TextWrapping.Wrap };
        header.Children.Add(_progress);
        header.Children.Add(_status);
        root.Children.Add(header);

        _grid = new DataGrid
        {
            ItemsSource = _issues,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            CanUserAddRows = false,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
        };
        Column("Severity", nameof(LibraryHealthIssue.Severity), 75);
        Column("Problem", nameof(LibraryHealthIssue.Type), 150);
        Column("Artist", nameof(LibraryHealthIssue.Artist), 145);
        Column("Title", nameof(LibraryHealthIssue.Title), 170);
        Column("Manufacturer", nameof(LibraryHealthIssue.Manufacturer), 105);
        Column("Disc", nameof(LibraryHealthIssue.Disc), 110);
        Column("Details", nameof(LibraryHealthIssue.Details), 280);
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "File",
            Binding = new Binding(nameof(LibraryHealthIssue.FilePath)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        root.Children.Add(_grid);

        Closed += (_, _) => _scanCts?.Cancel();
    }

    private static Button Button(string text, RoutedEventHandler click)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 5), MinHeight = 31 };
        button.Click += click;
        return button;
    }

    private void Column(string header, string property, double width)
        => _grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(property), Width = width });

    private async Task ScanAsync()
    {
        if (_scanCts is not null) return;
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;
        _scanButton.IsEnabled = false;
        _cancelButton.IsEnabled = true;
        _issues.Clear();
        _progress.Value = 0;
        var duplicateFirst = new Dictionary<string, SongRecord>(StringComparer.OrdinalIgnoreCase);
        long scanned = 0;
        long total = 0;

        try
        {
            for (var offset = 0; ; offset += 1000)
            {
                token.ThrowIfCancellationRequested();
                var page = await _library.BrowseAsync("Karaoke", null, "artist", false, offset, 1000, token);
                total = page.TotalCount;
                if (total > 0) _progress.Maximum = total;
                if (page.Items.Count == 0) break;

                var pageIssues = await Task.Run(() => AnalyzePage(page.Items, duplicateFirst, token), token);
                foreach (var issue in pageIssues) _issues.Add(issue);
                scanned += page.Items.Count;
                _progress.Value = Math.Min(scanned, Math.Max(1, total));
                _status.Text = $"Scanning… {scanned:N0} / {total:N0} tracks • {_issues.Count:N0} issues found";

                if (!page.HasNext) break;
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            }

            _status.Text = $"Scan complete: {scanned:N0} karaoke tracks checked • {_issues.Count:N0} issues found. Select a row to repair it. No media files were changed.";
        }
        catch (OperationCanceledException)
        {
            _status.Text = $"Scan cancelled after {scanned:N0} tracks • {_issues.Count:N0} issues currently listed.";
        }
        catch (Exception ex)
        {
            App.WriteDiagnostic("LIBRARY HEALTH SCAN", ex.ToString());
            _status.Text = "Health scan failed: " + ex.Message;
        }
        finally
        {
            _scanCts.Dispose();
            _scanCts = null;
            _scanButton.IsEnabled = true;
            _cancelButton.IsEnabled = false;
        }
    }

    private static List<LibraryHealthIssue> AnalyzePage(
        IReadOnlyList<SongRecord> songs,
        Dictionary<string, SongRecord> duplicateFirst,
        CancellationToken token)
    {
        var issues = new List<LibraryHealthIssue>();
        foreach (var song in songs)
        {
            token.ThrowIfCancellationRequested();
            var exists = SafeFileExists(song.FilePath);
            if (!exists)
            {
                issues.Add(Issue(song, "Missing file", "ERROR", "Indexed path does not exist. Use RELINK SELECTED or remove the stale database entry."));
            }
            else
            {
                var ext = Path.GetExtension(song.FilePath);
                if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var zipProblem = ValidateKaraokeZip(song.FilePath);
                    if (zipProblem is not null) issues.Add(Issue(song, "Bad ZIP", "ERROR", zipProblem));
                }
                else if (ext.Equals(".cdg", StringComparison.OrdinalIgnoreCase))
                {
                    if (!HasSiblingAudio(song.FilePath)) issues.Add(Issue(song, "Broken MP3+CDG pair", "ERROR", "CDG graphics file has no matching audio file beside it."));
                }
                else if (AudioExtensions.Contains(ext) && !HasSiblingCdg(song.FilePath))
                {
                    if (string.Equals(song.Format, "CDG", StringComparison.OrdinalIgnoreCase) || ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                        issues.Add(Issue(song, "Broken MP3+CDG pair", "ERROR", "Audio file has no matching same-name .cdg graphics file beside it."));
                }
            }

            if (string.IsNullOrWhiteSpace(song.Artist) || string.IsNullOrWhiteSpace(song.Title))
                issues.Add(Issue(song, "Missing metadata", "WARNING", "Artist and/or title is blank in the Hazz database."));
            if (song.DurationSeconds is not double duration || !double.IsFinite(duration) || duration <= 0)
                issues.Add(Issue(song, "Missing duration", "INFO", "Track length has not been stored yet. READ DURATION can probe this track."));

            var duplicateKey = DuplicateKey(song);
            if (duplicateKey.Length > 0)
            {
                if (duplicateFirst.TryGetValue(duplicateKey, out var first) && !PathEquals(first.FilePath, song.FilePath))
                {
                    issues.Add(Issue(song, "Possible duplicate copy", "WARNING",
                        $"Same artist/title/release/variant as: {Path.GetFileName(first.FilePath)}. Backing-vocal and duet/solo qualifiers are kept separate."));
                }
                else duplicateFirst[duplicateKey] = song;
            }
        }
        return issues;
    }

    private static LibraryHealthIssue Issue(SongRecord song, string type, string severity, string details) => new()
    {
        SongId = song.Id,
        Type = type,
        Severity = severity,
        Artist = song.Artist,
        Title = song.Title,
        Manufacturer = song.Manufacturer,
        Disc = song.DiscId,
        FilePath = song.FilePath,
        Details = details
    };

    private async Task RelinkSelectedAsync()
    {
        if (_grid.SelectedItem is not LibraryHealthIssue issue)
        {
            _status.Text = "Select a library issue first.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = $"Relink {issue.Artist} — {issue.Title}",
            Filter = "Karaoke/media files|*.zip;*.cdg;*.mp3;*.wav;*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.m4v;*.webm|All files|*.*",
            FileName = Path.GetFileName(issue.FilePath)
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var newPath = Path.GetFullPath(dialog.FileName);
            var info = new FileInfo(newPath);
            await using var connection = new SqliteConnection(_database.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
UPDATE songs
SET file_path=$path, file_size=$size, format=$format, last_seen_utc=CURRENT_TIMESTAMP
WHERE id=$id;
""";
            command.Parameters.AddWithValue("$path", newPath);
            command.Parameters.AddWithValue("$size", info.Exists ? info.Length : 0L);
            command.Parameters.AddWithValue("$format", FormatFromPath(newPath));
            command.Parameters.AddWithValue("$id", issue.SongId);
            var changed = await command.ExecuteNonQueryAsync();
            _status.Text = changed == 1
                ? $"Relinked database entry to {newPath}. Original media files were not changed. Run SCAN again to refresh all health results."
                : "That database row no longer exists.";
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            _status.Text = "That file is already indexed as another library row. Hazz did not change either entry.";
        }
        catch (Exception ex)
        {
            App.WriteDiagnostic("LIBRARY HEALTH RELINK", ex.ToString());
            _status.Text = "Relink failed: " + ex.Message;
        }
    }

    private async Task ProbeDurationSelectedAsync()
    {
        if (_grid.SelectedItem is not LibraryHealthIssue issue)
        {
            _status.Text = "Select a library issue first.";
            return;
        }
        if (!File.Exists(issue.FilePath))
        {
            _status.Text = "That file is missing. Relink it before reading its duration.";
            return;
        }

        _status.Text = "Reading duration…";
        var seconds = await MediaDurationProbe.TryReadSecondsAsync(issue.FilePath);
        if (seconds is not double value || value <= 0)
        {
            _status.Text = "Hazz could not read a valid duration from that file.";
            return;
        }
        await _library.SaveDurationAsync(issue.SongId, value);
        foreach (var row in _issues.Where(x => x.SongId == issue.SongId && x.Type == "Missing duration").ToArray())
            _issues.Remove(row);
        _status.Text = $"Duration stored: {TimeSpan.FromSeconds(value):m\\:ss}.";
    }

    private async Task DeleteSelectedAsync()
    {
        if (_grid.SelectedItem is not LibraryHealthIssue issue)
        {
            _status.Text = "Select a library issue first.";
            return;
        }
        var answer = MessageBox.Show(this,
            $"Remove this indexed track from the Hazz database?\n\n{issue.Artist} — {issue.Title}\n{issue.FilePath}\n\nThe actual media file will NOT be deleted.",
            "Remove Database Entry", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        var deleted = await _library.DeleteSongsAsync(new[] { issue.SongId });
        if (deleted > 0)
        {
            foreach (var row in _issues.Where(x => x.SongId == issue.SongId).ToArray()) _issues.Remove(row);
            _status.Text = "Database entry removed. Media file left untouched.";
        }
        else _status.Text = "That database entry was already gone.";
    }

    private void OpenSelectedFolder()
    {
        if (_grid.SelectedItem is not LibraryHealthIssue issue) return;
        try
        {
            var folder = File.Exists(issue.FilePath)
                ? Path.GetDirectoryName(issue.FilePath)
                : Path.GetDirectoryName(Path.GetFullPath(issue.FilePath));
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
            else _status.Text = "The indexed folder no longer exists.";
        }
        catch (Exception ex) { _status.Text = "Could not open folder: " + ex.Message; }
    }

    private static string? ValidateKaraokeZip(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var entries = zip.Entries.Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToArray();
            if (entries.Length == 0) return "ZIP opens but contains no files.";
            var cdg = entries.Where(x => Path.GetExtension(x.Name).Equals(".cdg", StringComparison.OrdinalIgnoreCase)).ToArray();
            var audio = entries.Where(x => AudioExtensions.Contains(Path.GetExtension(x.Name))).ToArray();
            if (cdg.Length == 0) return "ZIP contains no CDG graphics file.";
            if (audio.Length == 0) return "ZIP contains no karaoke audio file.";
            var paired = cdg.Any(c => audio.Any(a =>
                Path.GetFileNameWithoutExtension(a.Name).Equals(Path.GetFileNameWithoutExtension(c.Name), StringComparison.OrdinalIgnoreCase)));
            return paired ? null : "ZIP has CDG and audio files, but no same-name audio/CDG pair.";
        }
        catch (InvalidDataException ex) { return "ZIP is invalid/corrupt: " + ex.Message; }
        catch (IOException ex) { return "ZIP could not be read: " + ex.Message; }
        catch (UnauthorizedAccessException ex) { return "ZIP access denied: " + ex.Message; }
    }

    private static bool HasSiblingAudio(string cdgPath)
    {
        var basePath = Path.Combine(Path.GetDirectoryName(cdgPath) ?? string.Empty, Path.GetFileNameWithoutExtension(cdgPath));
        return AudioExtensions.Any(ext => File.Exists(basePath + ext) || File.Exists(basePath + ext.ToUpperInvariant()));
    }

    private static bool HasSiblingCdg(string audioPath)
    {
        var basePath = Path.Combine(Path.GetDirectoryName(audioPath) ?? string.Empty, Path.GetFileNameWithoutExtension(audioPath));
        return File.Exists(basePath + ".cdg") || File.Exists(basePath + ".CDG");
    }

    private static string DuplicateKey(SongRecord song)
    {
        var artist = Normalize(song.Artist);
        var title = Normalize(song.Title);
        if (title.Length == 0) return string.Empty;
        var variant = VariantSignature(song.Title + " " + Path.GetFileNameWithoutExtension(song.FilePath));
        return string.Join('|', artist, title, Normalize(song.Manufacturer), Normalize(song.DiscId), Normalize(song.Format), variant);
    }

    private static string VariantSignature(string text)
    {
        text = text.ToLowerInvariant();
        var values = new List<string>();
        // Keep the user's important karaoke variants separate: Wobgv = without backing vocals; wbgv = with backing vocals.
        if (text.Contains("wobgv", StringComparison.Ordinal)) values.Add("without-bgv");
        else if (text.Contains("wbgv", StringComparison.Ordinal)) values.Add("with-bgv");
        if (text.Contains("without backing vocal", StringComparison.Ordinal) || text.Contains("no backing vocal", StringComparison.Ordinal)) values.Add("without-bgv");
        if (text.Contains("with backing vocal", StringComparison.Ordinal)) values.Add("with-bgv");
        if (text.Contains("duet", StringComparison.Ordinal)) values.Add("duet");
        if (text.Contains("solo", StringComparison.Ordinal)) values.Add("solo");
        return string.Join(',', values.Distinct(StringComparer.Ordinal));
    }

    private static string Normalize(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        var sb = new StringBuilder(input.Length);
        var space = false;
        foreach (var ch in input)
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); space = false; }
            else if (!space) { sb.Append(' '); space = true; }
        }
        return sb.ToString().Trim();
    }

    private static bool SafeFileExists(string? path)
    {
        try { return !string.IsNullOrWhiteSpace(path) && File.Exists(Path.GetFullPath(path)); }
        catch { return false; }
    }

    private static bool PathEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
    }

    private static string FormatFromPath(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        return ext switch
        {
            "ZIP" => "ZIP",
            "CDG" => "CDG",
            "MP3" => "MP3",
            "MP4" or "MKV" or "AVI" or "MOV" or "WMV" or "M4V" or "WEBM" => "VIDEO",
            _ => ext
        };
    }
}
