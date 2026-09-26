using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

internal sealed class AlternativeVersionItem
{
    public required SongRecord Record { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsPreferred { get; init; }
    public bool Exists { get; init; }
    public string Marker => IsCurrent ? "CURRENT" : IsPreferred ? "REMEMBERED" : string.Empty;
    public string Manufacturer => Record.Manufacturer;
    public string Disc => Record.DiscId;
    public string Length => Record.DurationText;
    public string Format => Record.Format;
    public string Availability => Exists ? "OK" : "MISSING";
    public string File => System.IO.Path.GetFileName(Record.FilePath);
    public string Folder => System.IO.Path.GetDirectoryName(Record.FilePath) ?? string.Empty;
}

internal sealed class AlternativeVersionsWindow : Window
{
    private readonly DataGrid _grid;
    private readonly CheckBox _remember;
    private readonly TextBlock _status;

    public SongRecord? SelectedRecord { get; private set; }
    public bool RememberSelection => _remember.IsChecked == true;
    public bool ForgetPreference { get; private set; }

    public AlternativeVersionsWindow(
        Window owner,
        string singerName,
        string artist,
        string title,
        SongRecord? current,
        IEnumerable<SongRecord> versions,
        string? preferredPath)
    {
        Owner = owner;
        Title = "Find Alternative — " + (string.IsNullOrWhiteSpace(artist) ? title : $"{artist} — {title}");
        Width = 1050;
        Height = 620;
        MinWidth = 760;
        MinHeight = 430;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var unique = versions
            .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
            .GroupBy(x => System.IO.Path.GetFullPath(x.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
        if (current is not null && unique.All(x => !PathEquals(x.FilePath, current.FilePath)))
            unique.Insert(0, current);

        var rows = unique.Select(record => new AlternativeVersionItem
        {
            Record = record,
            IsCurrent = current is not null && PathEquals(record.FilePath, current.FilePath),
            IsPreferred = !string.IsNullOrWhiteSpace(preferredPath) && PathEquals(record.FilePath, preferredPath),
            Exists = File.Exists(record.FilePath)
        }).OrderByDescending(x => x.IsCurrent)
          .ThenByDescending(x => x.IsPreferred)
          .ThenBy(x => x.Manufacturer, StringComparer.OrdinalIgnoreCase)
          .ThenBy(x => x.Disc, StringComparer.OrdinalIgnoreCase)
          .ToList();

        var root = new DockPanel { Margin = new Thickness(12) };
        Content = root;

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(heading, Dock.Top);
        heading.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} — {title}",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        heading.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(singerName)
                ? "Choose the version to load. Double-click a row or select it and press USE SELECTED."
                : $"Singer: {singerName} • choose the version to load. CURRENT and REMEMBERED versions are marked.",
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        root.Children.Add(heading);

        var bottom = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(bottom, Dock.Bottom);
        _remember = new CheckBox
        {
            Content = string.IsNullOrWhiteSpace(singerName)
                ? "Remember this version"
                : $"Remember the selected version for {singerName} when this song is requested again",
            IsEnabled = !string.IsNullOrWhiteSpace(singerName),
            Margin = new Thickness(0, 0, 0, 7)
        };
        bottom.Children.Add(_remember);

        _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        bottom.Children.Add(_status);

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var forget = new Button { Content = "FORGET REMEMBERED VERSION", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(3), IsEnabled = !string.IsNullOrWhiteSpace(preferredPath) };
        var use = new Button { Content = "USE SELECTED", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(3), IsDefault = true };
        var cancel = new Button { Content = "CANCEL", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(3), IsCancel = true };
        forget.Click += (_, _) => { ForgetPreference = true; forget.IsEnabled = false; _status.Text = "Remembered version will be cleared when this window closes."; };
        use.Click += (_, _) => AcceptSelection();
        buttons.Children.Add(forget);
        buttons.Children.Add(use);
        buttons.Children.Add(cancel);
        bottom.Children.Add(buttons);
        root.Children.Add(bottom);

        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            ItemsSource = rows,
            CanUserAddRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
        };
        AddColumn("", nameof(AlternativeVersionItem.Marker), 105);
        AddColumn("Manufacturer", nameof(AlternativeVersionItem.Manufacturer), 125);
        AddColumn("Disc", nameof(AlternativeVersionItem.Disc), 135);
        AddColumn("Length", nameof(AlternativeVersionItem.Length), 75);
        AddColumn("Format", nameof(AlternativeVersionItem.Format), 80);
        AddColumn("File", nameof(AlternativeVersionItem.File), 230);
        AddColumn("Status", nameof(AlternativeVersionItem.Availability), 80);
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Folder",
            Binding = new Binding(nameof(AlternativeVersionItem.Folder)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        _grid.MouseDoubleClick += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left) AcceptSelection();
        };
        _grid.SelectionChanged += (_, _) => UpdateStatus();
        root.Children.Add(_grid);

        var preferred = rows.FirstOrDefault(x => x.IsPreferred && x.Exists)
            ?? rows.FirstOrDefault(x => x.IsCurrent && x.Exists)
            ?? rows.FirstOrDefault(x => x.Exists)
            ?? rows.FirstOrDefault();
        if (preferred is not null)
        {
            _grid.SelectedItem = preferred;
            _grid.ScrollIntoView(preferred);
        }
        UpdateStatus();
    }

    private void AddColumn(string header, string property, double width)
        => _grid.Columns.Add(new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(property),
            Width = width
        });

    private void UpdateStatus()
    {
        if (_grid.SelectedItem is not AlternativeVersionItem item)
        {
            _status.Text = "Select a version.";
            return;
        }
        _status.Text = item.Exists
            ? $"{item.Record.Manufacturer} {item.Record.DiscId} • {item.Record.Format} • {item.Record.DurationText}"
            : "This indexed version is missing from disk. Choose another version or repair it in Library Health Centre.";
    }

    private void AcceptSelection()
    {
        if (_grid.SelectedItem is not AlternativeVersionItem item) return;
        if (!item.Exists)
        {
            _status.Text = "That version is missing from disk and cannot be loaded.";
            return;
        }
        SelectedRecord = item.Record;
        DialogResult = true;
    }

    private static bool PathEquals(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        try { return string.Equals(System.IO.Path.GetFullPath(left), System.IO.Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
    }
}
