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
        var picker = new Button { Content = "CHOOSE TEXT COLOUR", Margin = new Thickness(0, 5, 0, 8) };

        var background = new TextBox();
        var backgroundPicker = new Button { Content = "CHOOSE BACKGROUND COLOUR", Margin = new Thickness(0, 5, 0, 5) };
        var opacity = new Slider { Minimum = 0, Maximum = 1, TickFrequency = 0.05, IsSnapToTickEnabled = true };
        var opacityLabel = new TextBlock();
        var borderColour = new TextBox();
        var borderPicker = new Button { Content = "CHOOSE BORDER COLOUR", Margin = new Thickness(0, 5, 0, 5) };
        var borderThickness = new Slider { Minimum = 0, Maximum = 12, TickFrequency = 0.5, IsSnapToTickEnabled = true };
        var borderLabel = new TextBlock();
        var cornerRadius = new Slider { Minimum = 0, Maximum = 40, TickFrequency = 1, IsSnapToTickEnabled = true };
        var cornerLabel = new TextBlock();
        var position = new ComboBox
        {
            ItemsSource = new[] { "Default", "Top Left", "Top Center", "Top Right", "Center Left", "Center", "Center Right", "Bottom Left", "Bottom Center", "Bottom Right" },
            Margin = new Thickness(0, 3, 0, 8)
        };

        var previewText = new TextBlock { Text = "Singer / song preview", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(14), Foreground = Brushes.White };
        var previewBorder = new Border { Background = Brushes.Black, Child = previewText, MaxHeight = 180, ClipToBounds = true, Margin = new Thickness(0, 8, 0, 0) };

        foreach (var element in new UIElement[]
        {
            new TextBlock { Text = "Choose the audience text to edit" }, selector,
            new TextBlock { Text = "Text: keep {singer}, {song}, {message}, {queue} or {venue} for live information.", TextWrapping = TextWrapping.Wrap }, text,
            new TextBlock { Text = "Font", Margin = new Thickness(0, 10, 0, 3) }, font, sizeLabel, size,
            new TextBlock { Text = "Text colour (#RRGGBB or #AARRGGBB)" }, colour, picker,
            new Separator { Margin = new Thickness(0, 8, 0, 8) },
            new TextBlock { Text = "OVERLAY PANEL APPEARANCE", FontWeight = FontWeights.Bold },
            new TextBlock { Text = "These controls style the whole selected overlay. Call-up singer/song/message share the Call-up panel; Now Singing fields share the Now Singing panel.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) },
            new TextBlock { Text = "Background colour" }, background, backgroundPicker,
            opacityLabel, opacity,
            new TextBlock { Text = "Border colour", Margin = new Thickness(0, 8, 0, 0) }, borderColour, borderPicker,
            borderLabel, borderThickness,
            cornerLabel, cornerRadius,
            new TextBlock { Text = "Screen position", Margin = new Thickness(0, 8, 0, 0) }, position,
            previewBorder
        }) panel.Children.Add(element);

        var buttons = new WrapPanel { Margin = new Thickness(16, 4, 16, 12) };
        var save = new Button { Content = "SAVE", Padding = new Thickness(18, 6, 18, 6) };
        var cancel = new Button { Content = "CANCEL", IsCancel = true, Margin = new Thickness(8, 0, 0, 0) };
        buttons.Children.Add(save); buttons.Children.Add(cancel);
        var root = new DockPanel(); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var window = new Window
        {
            Owner = this,
            Title = "Audience overlay text & appearance",
            Width = 620,
            Height = Math.Min(820, SystemParameters.WorkArea.Height - 40),
            MaxWidth = SystemParameters.WorkArea.Width - 40,
            Background = Brushes.White,
            Foreground = Brushes.Black,
            Content = root,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        string key = draft.Keys.First();
        bool loading = false;

        string PanelKey(string value) => value switch
        {
            var x when x.StartsWith("Call-up ", StringComparison.Ordinal) => "Call-up heading",
            var x when x.StartsWith("Now singing ", StringComparison.Ordinal) => "Now singing heading",
            "Announcement" => "Announcement",
            "Queue status" => "Queue status",
            "Venue header" => "Venue header",
            _ => value
        };

        AudienceTextStyle PanelStyle() => draft[PanelKey(key)];

        void Read()
        {
            if (loading) return;
            var s = draft[key];
            s.Text = text.Text;
            s.Font = font.Text;
            s.Size = size.Value;
            s.Colour = colour.Text;

            var p = PanelStyle();
            p.BackgroundColour = background.Text;
            p.BackgroundOpacity = opacity.Value;
            p.BorderColour = borderColour.Text;
            p.BorderThickness = borderThickness.Value;
            p.CornerRadius = cornerRadius.Value;
            p.Position = position.SelectedItem?.ToString() ?? "Default";
        }

        void Load()
        {
            loading = true;
            var s = draft[key];
            text.Text = s.Text;
            font.Text = s.Font;
            size.Value = s.Size;
            colour.Text = s.Colour;
            var p = PanelStyle();
            background.Text = p.BackgroundColour;
            opacity.Value = p.BackgroundOpacity;
            borderColour.Text = p.BorderColour;
            borderThickness.Value = p.BorderThickness;
            cornerRadius.Value = p.CornerRadius;
            position.SelectedItem = p.Position;
            loading = false;
            Preview();
        }

        void Preview()
        {
            sizeLabel.Text = $"Text size: {size.Value:0} px";
            opacityLabel.Text = $"Background opacity: {opacity.Value:P0}";
            borderLabel.Text = $"Border thickness: {borderThickness.Value:0.0} px";
            cornerLabel.Text = $"Corner radius: {cornerRadius.Value:0} px";
            try
            {
                previewText.FontFamily = new FontFamily(font.Text);
                previewText.FontSize = size.Value;
                previewText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colour.Text));
                var bg = (Color)ColorConverter.ConvertFromString(background.Text);
                bg.A = (byte)Math.Clamp((int)Math.Round(opacity.Value * 255), 0, 255);
                previewBorder.Background = new SolidColorBrush(bg);
                previewBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderColour.Text));
                previewBorder.BorderThickness = new Thickness(borderThickness.Value);
                previewBorder.CornerRadius = new CornerRadius(cornerRadius.Value);
            }
            catch { }
        }

        void PickColour(TextBox destination)
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                destination.Text = $"#FF{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }

        selector.SelectionChanged += (_, _) =>
        {
            if (selector.SelectedItem is not string selected) return;
            Read();
            key = selected;
            Load();
        };
        text.TextChanged += (_, _) => Read();
        font.LostFocus += (_, _) => { Read(); Preview(); };
        size.ValueChanged += (_, _) => { Read(); Preview(); };
        colour.TextChanged += (_, _) => { Read(); Preview(); };
        background.TextChanged += (_, _) => { Read(); Preview(); };
        opacity.ValueChanged += (_, _) => { Read(); Preview(); };
        borderColour.TextChanged += (_, _) => { Read(); Preview(); };
        borderThickness.ValueChanged += (_, _) => { Read(); Preview(); };
        cornerRadius.ValueChanged += (_, _) => { Read(); Preview(); };
        position.SelectionChanged += (_, _) => Read();
        picker.Click += (_, _) => PickColour(colour);
        backgroundPicker.Click += (_, _) => PickColour(background);
        borderPicker.Click += (_, _) => PickColour(borderColour);
        save.Click += (_, _) =>
        {
            Read();
            _audienceTextStyles = AudienceTextStyle.Normalize(draft);
            AudienceEnhancementControlChanged();
            window.DialogResult = true;
        };
        Load();
        window.ShowDialog();
    }
}
