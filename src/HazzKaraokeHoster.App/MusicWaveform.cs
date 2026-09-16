using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NAudio.Wave;

namespace HazzKaraokeHoster.App;

/// <summary>A bounded, background-decoded overview. Never opens an audio output device.</summary>
public sealed class MusicWaveform : FrameworkElement
{
    public static readonly DependencyProperty FilePathProperty = DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(MusicWaveform), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(MusicWaveform), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public string? FilePath { get => (string?)GetValue(FilePathProperty); set => SetValue(FilePathProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public event Action<double>? SeekRequested;
    private float[]? _peaks;
    private string _message = "Waveform appears when a track is loaded";
    private CancellationTokenSource? _decode;
    private static readonly SemaphoreSlim DecodeGate = new(1);
    private static readonly Dictionary<string, float[]> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> CacheOrder = new();

    public MusicWaveform()
    {
        Height = 48; MinHeight = 32; Cursor = Cursors.Hand;
        ToolTip = "Real audio waveform. Click to seek; the position slider also supports keyboard seeking.";
        Unloaded += (_, _) => _decode?.Cancel();
        Loaded += (_, _) => { if (_peaks is null) LoadWaveform(); };
        MouseLeftButtonDown += (_, e) =>
        {
            if (ActualWidth > 0 && _peaks is not null)
                SeekRequested?.Invoke(Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1));
            e.Handled = true;
        };
    }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MusicWaveform)d).LoadWaveform();
    private async void LoadWaveform()
    {
        _decode?.Cancel();
        var cts = _decode = new CancellationTokenSource();
        var path = FilePath;
        _peaks = null;
        _message = string.IsNullOrEmpty(path) ? "Waveform appears when a track is loaded" : "Building waveform…";
        InvalidateVisual();
        if (string.IsNullOrEmpty(path)) { _decode = null; cts.Dispose(); return; }
        try
        {
            var peaks = await Task.Run(() => ReadPeaksAsync(path, cts.Token));
            if (!cts.IsCancellationRequested && ReferenceEquals(_decode, cts)) { _peaks = peaks; _message = ""; }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (ReferenceEquals(_decode, cts)) _message = "Waveform unavailable • use the position slider";
            System.Diagnostics.Debug.WriteLine($"Waveform: {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_decode, cts)) { _decode = null; InvalidateVisual(); }
            cts.Dispose();
        }
    }
    internal static async Task<float[]> ReadPeaksAsync(string path, CancellationToken token)
    {
        await DecodeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var info = new FileInfo(path);
            var key = path + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            using var reader = new AudioFileReader(path);
            if (reader.TotalTime.TotalHours > 2) throw new InvalidDataException("Waveform overview is limited to two hours.");
            var peaks = new float[1024];
            long sample = 0;
            var samplesPerBucket = Math.Max(1, (long)Math.Ceiling(reader.Length / 4d / peaks.Length));
            var buffer = new float[16384];
            int count, blocks = 0;
            while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                for (int i = 0; i < count; i++, sample++)
                {
                    int bucket = (int)Math.Min(peaks.Length - 1, sample / samplesPerBucket);
                    var value = Math.Abs(buffer[i]);
                    if (float.IsFinite(value)) peaks[bucket] = Math.Max(peaks[bucket], Math.Min(1, value));
                }
                // Yield regularly: overview generation must not monopolise a laptop CPU during a show.
                if (++blocks % 16 == 0) await Task.Delay(2, token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            Cache[key] = peaks; CacheOrder.Enqueue(key);
            while (CacheOrder.Count > 16) Cache.Remove(CacheOrder.Dequeue());
            return peaks;
        }
        finally { DecodeGate.Release(); }
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(8, 22, 29)), null, new Rect(RenderSize), 5, 5);
        if (_peaks is null)
        {
            var text = new FormattedText(_message, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.LightSlateGray, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
            dc.DrawText(text, new Point(8, Math.Max(0, (ActualHeight - text.Height) / 2))); dc.Pop(); return;
        }
        var progress = double.IsFinite(Progress) ? Math.Clamp(Progress, 0, 1) : 0;
        var played = new Pen(Brushes.Turquoise, 1.5);
        var ahead = new Pen(Brushes.SlateGray, 1.5);
        int bars = Math.Min(_peaks.Length, Math.Max(1, (int)(ActualWidth / 3)));
        for (int b = 0; b < bars; b++)
        {
            int from = b * _peaks.Length / bars, to = (b + 1) * _peaks.Length / bars;
            float peak = 0; for (int i = from; i < to; i++) peak = Math.Max(peak, _peaks[i]);
            double x = (b + .5) * ActualWidth / bars, h = Math.Max(1, peak * (ActualHeight - 8) / 2);
            dc.DrawLine(x <= progress * ActualWidth ? played : ahead, new Point(x, ActualHeight / 2 - h), new Point(x, ActualHeight / 2 + h));
        }
        dc.DrawLine(new Pen(Brushes.White, 1), new Point(progress * ActualWidth, 2), new Point(progress * ActualWidth, ActualHeight - 2));
    }
}
