using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HazzKaraokeHoster.App;

// A library-side tag: original media files are never renamed or modified.
internal static class BrokenMediaRegistry
{
    private static readonly Dictionary<string, string> Broken = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<Control> Rows = new();
    private static readonly HashSet<Control> Highlighted = new();
    private static readonly SemaphoreSlim Writes = new(1, 1);
    private static string _storePath = string.Empty;

    public static async Task InitializeAsync(string storePath)
    {
        _storePath = storePath;
        foreach (var type in new[] { typeof(DataGridRow), typeof(ListBoxItem) })
        {
            var style = new Style(type);
            style.Setters.Add(new EventSetter(FrameworkElement.LoadedEvent, new RoutedEventHandler(RowLoaded)));
            System.Windows.Application.Current.Resources[type] = style;
        }
        try
        {
            var saved = await Task.Run(() => File.Exists(storePath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(storePath)) : null);
            if (saved is not null) foreach (var pair in saved) Broken.TryAdd(pair.Key, pair.Value);
            foreach (var row in Rows) Paint(row);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { App.WriteDiagnostic("BROKEN FILE TAGS", ex.Message); }
    }

    public static void Mark(string? path, string reason)
        => _ = MarkAsync(path, reason);

    private static async Task MarkAsync(string? path, string reason)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            path = Path.GetFullPath(path);
            Broken[path] = reason;
            foreach (var row in Rows) Paint(row);
            var snapshot = JsonSerializer.Serialize(Broken);
            await Writes.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
                    File.WriteAllText(_storePath + ".tmp", snapshot);
                    File.Move(_storePath + ".tmp", _storePath, overwrite: true);
                });
            }
            finally { Writes.Release(); }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { App.WriteDiagnostic("BROKEN FILE TAG SAVE", ex.ToString()); }
    }

    private static void RowLoaded(object sender, RoutedEventArgs e)
    {
        var row = (Control)sender;
        if (!Rows.Add(row)) return;
        row.DataContextChanged += ContextChanged;
        row.Unloaded += RowUnloaded;
        if (row.DataContext is INotifyPropertyChanged item) item.PropertyChanged += ItemChanged;
        Paint(row);
    }

    private static void ContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldItem) oldItem.PropertyChanged -= ItemChanged;
        if (e.NewValue is INotifyPropertyChanged newItem) newItem.PropertyChanged += ItemChanged;
        Paint((Control)sender);
    }
    private static void ItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var row in Rows) if (ReferenceEquals(row.DataContext, sender)) Paint(row);
    }
    private static void RowUnloaded(object sender, RoutedEventArgs e)
    {
        var row = (Control)sender;
        Rows.Remove(row);
        row.DataContextChanged -= ContextChanged;
        row.Unloaded -= RowUnloaded;
        if (row.DataContext is INotifyPropertyChanged item) item.PropertyChanged -= ItemChanged;
        Clear(row);
    }

    private static void Clear(Control row)
    {
        if (!Highlighted.Remove(row)) return;
        row.ClearValue(Control.BackgroundProperty);
        row.ClearValue(Control.ForegroundProperty);
        row.ClearValue(FrameworkElement.ToolTipProperty);
    }

    private static void Paint(Control row)
    {
        var item = row.DataContext;
        var path = item?.GetType().GetProperty("FilePath")?.GetValue(item) as string;
        if (path is null && item is HazzKaraokeHoster.Core.Models.SingerQueueEntry singer) path = singer.NextSong?.FilePath;
        if (path is not null && Broken.TryGetValue(path, out var reason))
        {
            Highlighted.Add(row);
            row.Background = new SolidColorBrush(Color.FromRgb(135, 30, 40));
            row.Foreground = Brushes.White;
            row.ToolTip = "BROKEN — playback failed\n" + reason + "\n" + path;
        }
        else Clear(row);
    }
}

