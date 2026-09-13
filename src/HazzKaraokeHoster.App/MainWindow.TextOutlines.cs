using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private Dictionary<string, TextStrokeSettings> _audienceTextStrokes = new();
    private Window? _textOutlineWindow;
    private bool _restoringOutlineSettings;
    private void AudienceTextOutlines_Click(object sender, RoutedEventArgs e)
    {
        if (_textOutlineWindow != null) { _textOutlineWindow.Activate(); return; }
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "AUDIENCE TEXT OUTLINES", FontSize = 20, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = "Enable each outline, choose its colour and adjust the width. Changes appear live.", Margin = new Thickness(0, 8, 0, 12), TextWrapping = TextWrapping.Wrap });
        foreach (var (key, label) in new[] { ("Heading", "Next singers heading"), ("Position", "Position numbers"), ("Singer", "Singer names"), ("Song", "Song / artist"), ("Rotation", "Rotation scroller"), ("Venue", "Venue message scroller"), ("Kamikaze", "Kamikaze message") })
        {
            if (!_audienceTextStrokes.TryGetValue(key, out var value) || value == null)
                _audienceTextStrokes[key] = value = new TextStrokeSettings();
            var setting = value;
            var row = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
            var enabled = new CheckBox { Content = label, IsChecked = setting.Enabled, Width = 230, VerticalAlignment = VerticalAlignment.Center };
            enabled.Checked += (_, _) => { setting.Enabled = true; ApplyOverlaySettings(); };
            enabled.Unchecked += (_, _) => { setting.Enabled = false; ApplyOverlaySettings(); };
            var colour = new Button { Content = "COLOUR", Width = 100 };
            UpdateAudienceColorButton(colour, NormalizeColor(setting.Color, "#FF000000"));
            colour.Click += (_, _) =>
            {
                using var picker = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(NormalizeColor(setting.Color, "#FF000000")[3..].Insert(0, "#")) };
                if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                setting.Color = $"#FF{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
                UpdateAudienceColorButton(colour, setting.Color); ApplyOverlaySettings();
            };
            var width = new Slider { Minimum = 0.5, Maximum = 8, TickFrequency = 0.5, IsSnapToTickEnabled = true,
                Value = double.IsFinite(setting.Width) ? Math.Clamp(setting.Width, 0.5, 8) : 2, Width = 160, Margin = new Thickness(12, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            var amount = new TextBlock { Text = $"{width.Value:0.#} px", Width = 52, VerticalAlignment = VerticalAlignment.Center };
            width.ValueChanged += (_, _) => { setting.Width = width.Value; amount.Text = $"{width.Value:0.#} px"; ApplyOverlaySettings(); };
            row.Children.Add(enabled); row.Children.Add(colour); row.Children.Add(width); row.Children.Add(amount); panel.Children.Add(row);
        }
        var close = new Button { Content = "CLOSE", HorizontalAlignment = HorizontalAlignment.Right, Width = 100 };
        panel.Children.Add(close);
        var dialog = new Window { Title = "Audience Text Outlines", Owner = this, Width = 720, Height = 480,
            MaxHeight = SystemParameters.WorkArea.Height, Background = new SolidColorBrush(Color.FromRgb(14, 22, 30)), Foreground = Brushes.White,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        dialog.Resources.MergedDictionaries.Add(Resources);
        close.Click += (_, _) => dialog.Close();
        dialog.Closed += (_, _) => { _textOutlineWindow = null; if (!_restoringOutlineSettings) SaveMainLayout(); };
        _textOutlineWindow = dialog; dialog.Show();
    }
}
