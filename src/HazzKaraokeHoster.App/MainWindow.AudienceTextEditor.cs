using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private Dictionary<string, AudienceTextStyle> _audienceTextStyles = AudienceTextStyle.Defaults();
    private bool _callingNextSinger;

    private async void CallNextSinger_Click(object sender, RoutedEventArgs e)
    {
        if (_callingNextSinger) return;
        if (_karaokePlaying || _karaokePaused || _karaokePresentationActive)
        {
            SearchStatus.Text = "Finish or Fade Stop the current karaoke song before calling the next singer.";
            return;
        }
        _callingNextSinger = true;
        try
        {
            if (!await LoadNextSingerAsync() || _lifetime.IsCancellationRequested || _activeSinger is null) return;
            _kamikazeBannerRequested = false;
            var audience = EnsureAudienceWindow(show: false);
            audience.HideKamikazeBanner();
            audience.ApplyEnhancementSettings(ReadAudienceEnhancementControls());
            audience.SetEnhancementPlaybackActive(false);
            audience.ShowSingerCallUp(_activeSinger.SingerName,
                _activeSingerSong is null ? "" : $"{_activeSingerSong.SongTitle} — {_activeSingerSong.Artist}".Trim(' ', '—'),
                TimeSpan.FromSeconds(_singerCallUpSecondsSlider?.Value ?? 8), manual: true);
            SearchStatus.Text = $"{_activeSinger.SingerName} called • song loaded • press PLAY when ready";
        }
        catch (Exception ex) { App.WriteDiagnostic("CALL NEXT SINGER", ex.ToString()); SearchStatus.Text = "Call-up failed: " + ex.Message; }
        finally { _callingNextSinger = false; }
    }

    private void EditAudienceTextStyles(object sender, RoutedEventArgs e)
    {
        var draft = AudienceTextStyle.Normalize(System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, AudienceTextStyle>>(
            System.Text.Json.JsonSerializer.Serialize(_audienceTextStyles)));
        var panel = new StackPanel { Margin = new Thickness(16) };
        var selector = new ComboBox { ItemsSource = draft.Keys.ToArray(), SelectedIndex = 0, Margin = new Thickness(0, 5, 0, 12) };
        var text = new TextBox { TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MinHeight = 65, MaxLength = 500 };
        var font = new ComboBox { ItemsSource = Fonts.SystemFontFamilies.Select(x => x.Source).OrderBy(x => x).ToArray(), IsEditable = true };
        var size = new Slider { Minimum = 12, Maximum = 160, TickFrequency = 1, IsSnapToTickEnabled = true };
        var sizeLabel = new TextBlock();
        var colour = new TextBox();
        var preview = new TextBlock { Text = "Singer / song preview", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8), Foreground = Brushes.White };
        var picker = new Button { Content = "CHOOSE COLOUR", Margin = new Thickness(0, 5, 0, 5) };
        foreach (var element in new UIElement[] { new TextBlock { Text = "Choose the audience text to edit" }, selector,
            new TextBlock { Text = "Text: keep {singer}, {song}, {message}, {queue} or {venue} for live information.", TextWrapping = TextWrapping.Wrap }, text,
            new TextBlock { Text = "Font", Margin = new Thickness(0, 10, 0, 3) }, font, sizeLabel, size,
            new TextBlock { Text = "Colour (#RRGGBB or #AARRGGBB)" }, colour, picker,
            new Border { Background = Brushes.Black, Child = preview, MaxHeight = 180, ClipToBounds = true } }) panel.Children.Add(element);
        var buttons = new WrapPanel { Margin = new Thickness(16, 4, 16, 12) };
        var save = new Button { Content = "SAVE", Padding = new Thickness(18, 6, 18, 6) };
        var cancel = new Button { Content = "CANCEL", IsCancel = true, Margin = new Thickness(8, 0, 0, 0) };
        buttons.Children.Add(save); buttons.Children.Add(cancel);
        var root = new DockPanel(); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var window = new Window { Owner = this, Title = "Audience overlay text", Width = 580,
            Height = Math.Min(720, SystemParameters.WorkArea.Height - 40), MaxWidth = SystemParameters.WorkArea.Width - 40,
            Background = Brushes.White, Foreground = Brushes.Black, Content = root, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        string key = draft.Keys.First(); bool loading = false;
        void Read()
        {
            if (loading) return;
            var s = draft[key]; s.Text = text.Text; s.Font = font.Text; s.Size = size.Value; s.Colour = colour.Text;
        }
        void Load()
        {
            loading = true; var s = draft[key]; text.Text = s.Text; font.Text = s.Font; size.Value = s.Size; colour.Text = s.Colour; loading = false;
            Preview();
        }
        void Preview()
        {
            sizeLabel.Text = $"Text size: {size.Value:0} px";
            try { preview.FontFamily = new FontFamily(font.Text); preview.FontSize = size.Value; preview.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colour.Text)); } catch { }
        }
        selector.SelectionChanged += (_, _) => { Read(); key = (string)selector.SelectedItem; Load(); };
        text.TextChanged += (_, _) => Read();
        font.LostFocus += (_, _) => { Read(); Preview(); };
        size.ValueChanged += (_, _) => { Read(); Preview(); };
        colour.TextChanged += (_, _) => { Read(); Preview(); };
        picker.Click += (_, _) =>
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                colour.Text = $"#FF{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        };
        save.Click += (_, _) => { Read(); _audienceTextStyles = AudienceTextStyle.Normalize(draft); AudienceEnhancementControlChanged(); window.DialogResult = true; };
        Load(); window.ShowDialog();
    }
}
