using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace HazzKaraokeHoster.App;

// Draw retained vector outlines; moving the scroller reuses the drawing.
public sealed class StrokeTextBlock : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(StrokeTextBlock), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public TextWrapping TextWrapping { get; set; } = TextWrapping.NoWrap;
    public TextTrimming TextTrimming { get; set; }
    public TextAlignment TextAlignment { get; set; }
    public List<Run> Inlines { get; } = new();
    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Inlines.Count > 0 ? string.Concat(Inlines.Select(r => r.Text)) : Text;
        var width = TextWrapping == TextWrapping.Wrap ? Math.Max(1, availableSize.Width - Padding.Left - Padding.Right) : double.PositiveInfinity;
        var f = Format(text, Foreground, width);
        return new Size(Math.Ceiling(f.WidthIncludingTrailingWhitespace + Padding.Left + Padding.Right), Math.Ceiling(f.Height + Padding.Top + Padding.Bottom));
    }
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.RegisterAttached("Stroke", typeof(Brush), typeof(StrokeTextBlock), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeWidthProperty = DependencyProperty.RegisterAttached("StrokeWidth", typeof(double), typeof(StrokeTextBlock), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static void SetStroke(DependencyObject o, Brush value) => o.SetValue(StrokeProperty, value);
    public static Brush GetStroke(DependencyObject o) => (Brush)o.GetValue(StrokeProperty);
    public static void SetStrokeWidth(DependencyObject o, double value) => o.SetValue(StrokeWidthProperty, value);
    public static double GetStrokeWidth(DependencyObject o) => (double)o.GetValue(StrokeWidthProperty);

    protected override void OnRender(DrawingContext dc)
    {
        var runs = Inlines.OfType<Run>().ToArray();
        bool segmented = runs.Length > 0;
        if (segmented && TextWrapping == TextWrapping.NoWrap)
        {
            double x = Padding.Left;
            foreach (var run in runs)
            {
                var formatted = Format(run.Text, run.Foreground, double.PositiveInfinity);
                Draw(dc, formatted, new Point(x, Padding.Top), GetStroke(run), GetStrokeWidth(run));
                x += formatted.WidthIncludingTrailingWhitespace;
            }
        }
        else
        {
            var formatted = Format(Text, Foreground, Math.Max(1, ActualWidth - Padding.Left - Padding.Right));
            formatted.TextAlignment = TextAlignment;
            formatted.Trimming = TextTrimming;
            Draw(dc, formatted, new Point(Padding.Left, Padding.Top), GetStroke(this), GetStrokeWidth(this));
        }
    }
    private FormattedText Format(string text, Brush brush, double width)
    {
        var f = new FormattedText(text ?? string.Empty, CultureInfo.CurrentUICulture, FlowDirection,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        // A layout-rounded width can be fractionally smaller than the glyph
        // advance measured above. FormattedText would then wrap the last word,
        // and MaxLineCount=1 hides it. Untrimmed single-line text must retain
        // its full natural width, including a rounding allowance.
        if (double.IsFinite(width))
            f.MaxTextWidth = TextWrapping == TextWrapping.NoWrap && TextTrimming == TextTrimming.None
                ? Math.Max(width, Math.Ceiling(f.WidthIncludingTrailingWhitespace) + 1)
                : width;
        if (TextWrapping == TextWrapping.NoWrap) f.MaxLineCount = 1;
        return f;
    }
    private static void Draw(DrawingContext dc, FormattedText text, Point origin, Brush stroke, double width)
    {
        if (width > 0)
        {
            var pen = new Pen(stroke, width) { LineJoin = PenLineJoin.Round };
            dc.DrawGeometry(null, pen, text.BuildGeometry(origin));
        }
        dc.DrawText(text, origin);
    }
}
