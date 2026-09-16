using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HazzKaraokeHoster.App;

public sealed class SingerPortrait : FrameworkElement
{
    public static readonly DependencyProperty SingerNameProperty = DependencyProperty.Register(nameof(SingerName), typeof(string), typeof(SingerPortrait), new PropertyMetadata("", Changed));
    public static readonly DependencyProperty VenueKeyProperty = DependencyProperty.Register(nameof(VenueKey), typeof(string), typeof(SingerPortrait), new PropertyMetadata("default", Changed));
    public static readonly DependencyProperty RevisionProperty = DependencyProperty.Register(nameof(Revision), typeof(int), typeof(SingerPortrait), new PropertyMetadata(0, Changed));
    public string SingerName { get => (string)GetValue(SingerNameProperty); set => SetValue(SingerNameProperty, value); }
    public string VenueKey { get => (string)GetValue(VenueKeyProperty); set => SetValue(VenueKeyProperty, value); }
    public int Revision { get => (int)GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
    private BitmapSource? _image;
    private int _request;
    private CancellationTokenSource? _pending;
    private static readonly SemaphoreSlim Gate = new(1);
    private static readonly Dictionary<string, BitmapSource?> Cache = new(StringComparer.Ordinal);
    public SingerPortrait()
    {
        Width = 42; Height = 42; Margin = new Thickness(5); IsHitTestVisible = false;
        Unloaded += (_, _) => _pending?.Cancel();
        Loaded += (_, _) => { if (_image is null) Refresh(); };
    }
    internal static string PhotoPath(string venue, string name)
    {
        var scope = Guid.TryParse(venue, out var id) ? id.ToString("N") : "default";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.Trim().ToUpperInvariant())));
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hazz Karaoke Hoster", "singer-photos", scope, key + ".jpg");
    }
    internal static void InvalidatePhotoCache() { lock (Cache) Cache.Clear(); }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((SingerPortrait)d).Refresh();
    private async void Refresh()
    {
        int request = ++_request;
        _pending?.Cancel();
        var pending = _pending = new CancellationTokenSource();
        _image = null; InvalidateVisual();
        var path = PhotoPath(VenueKey, SingerName);
        try
        {
            var image = await Task.Run(async () =>
            {
                await Gate.WaitAsync(pending.Token).ConfigureAwait(false);
                try
                {
                    pending.Token.ThrowIfCancellationRequested();
                    lock (Cache) if (Cache.TryGetValue(path, out var found)) return found;
                    BitmapSource? bitmap = null;
                    if (File.Exists(path))
                    {
                        using var stream = File.OpenRead(path);
                        var decoded = new BitmapImage(); decoded.BeginInit(); decoded.CacheOption = BitmapCacheOption.OnLoad;
                        decoded.DecodePixelWidth = 128; decoded.StreamSource = stream; decoded.EndInit(); decoded.Freeze(); bitmap = decoded;
                    }
                    lock (Cache) { if (Cache.Count >= 128) Cache.Clear(); Cache[path] = bitmap; }
                    return bitmap;
                }
                finally { Gate.Release(); }
            });
            if (request == _request && !pending.IsCancellationRequested) { _image = image; InvalidateVisual(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Singer photo: {ex.Message}"); }
        finally { if (ReferenceEquals(_pending, pending)) _pending = null; pending.Dispose(); }
    }
    protected override void OnRender(DrawingContext dc)
    {
        // The list thumbnail is deliberately square so faces and landscape
        // photos are not forced into a circular crop.
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        dc.DrawRectangle(Brushes.DarkSlateGray, null, new Rect(RenderSize));
        if (_image is not null)
        {
            var scale = Math.Max(ActualWidth / _image.PixelWidth, ActualHeight / _image.PixelHeight);
            var w = _image.PixelWidth * scale; var h = _image.PixelHeight * scale;
            dc.DrawImage(_image, new Rect((ActualWidth - w) / 2, (ActualHeight - h) / 2, w, h));
        }
        else
        {
            var initials = string.Concat(SingerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => StringInfo.GetNextTextElement(p))).ToUpperInvariant();
            var text = new FormattedText(initials, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 15, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point((ActualWidth - text.Width) / 2, (ActualHeight - text.Height) / 2));
        }
        dc.Pop();
    }
}
