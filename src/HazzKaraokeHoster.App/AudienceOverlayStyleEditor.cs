using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HazzKaraokeHoster.App;

internal sealed class AudienceOverlayStyleEditor : Expander
{
    private sealed record StyleFields(ComboBox Font, Slider Size, TextBlock SizeText, TextBox Color);

    private static readonly string[] FontNames = Fonts.SystemFontFamilies
        .Select(x => x.Source)
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .Take(500)
        .ToArray();

    private readonly TextBox _callHeading;
    private readonly TextBox _callPrompt;
    private readonly StyleFields _callHeadingStyle;
    private readonly StyleFields _callNameStyle;
    private readonly StyleFields _callSongStyle;
    private readonly StyleFields _callPromptStyle;
    private readonly TextBox _callBackground;
    private readonly TextBox _callBorder;
    private readonly ComboBox _callPosition;

    private readonly TextBox _nowHeading;
    private readonly StyleFields _nowHeadingStyle;
    private readonly StyleFields _nowNameStyle;
    private readonly StyleFields _nowSongStyle;
    private readonly TextBox _nowBackground;
    private readonly TextBox _nowBorder;
    private readonly ComboBox _nowPosition;

    private readonly TextBox _queueTemplate;
    private readonly TextBox _queueEmpty;
    private readonly StyleFields _queueStyle;
    private readonly TextBox _queueBackground;
    private readonly TextBox _queueBorder;
    private readonly ComboBox _queuePosition;

    private readonly StyleFields _venueStyle;
    private readonly TextBox _venueBackground;
    private readonly TextBox _venueBorder;
    private readonly ComboBox _venuePosition;

    private readonly StyleFields _announcementStyle;
    private readonly TextBox _announcementBackground;
    private readonly TextBox _announcementBorder;
    private readonly ComboBox _announcementPosition;

    private bool _loading = true;

    internal event EventHandler? SettingsChanged;

    internal AudienceOverlayStyleEditor(AudienceEnhancementSettings settings)
    {
        Header = "EDIT NEW AUDIENCE TEXT / FONT / COLOUR / POSITION";
        IsExpanded = false;
        Margin = new Thickness(0, 6, 0, 4);
        Foreground = Brushes.White;

        var root = new StackPanel { Margin = new Thickness(8, 6, 8, 8) };
        Content = root;
        root.Children.Add(new TextBlock
        {
            Text = "Text, fonts, sizes, colours and positions are saved and applied live. Use PICK for colours; the existing transparency is preserved when a new RGB colour is chosen.",
            Foreground = Brushes.LightGray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var call = AddSection(root, "NEXT SINGER CALL-UP");
        _callHeading = AddText(call, "Heading text", settings.SingerCallUpHeadingText, 260);
        _callPrompt = AddText(call, "Bottom message", settings.SingerCallUpPromptText, 360);
        _callPosition = AddPosition(call, "Position", settings.SingerCallUpPosition);
        _callHeadingStyle = AddStyle(call, "Heading", settings.SingerCallUpHeadingStyle, 12, 96);
        _callNameStyle = AddStyle(call, "Singer name", settings.SingerCallUpNameStyle, 16, 140);
        _callSongStyle = AddStyle(call, "Song / artist", settings.SingerCallUpSongStyle, 12, 96);
        _callPromptStyle = AddStyle(call, "Bottom message", settings.SingerCallUpPromptStyle, 10, 72);
        _callBackground = AddColor(call, "Panel background", settings.SingerCallUpBackgroundColor);
        _callBorder = AddColor(call, "Panel border", settings.SingerCallUpBorderColor);

        var now = AddSection(root, "NOW SINGING");
        _nowHeading = AddText(now, "Heading text", settings.NowSingingHeadingText, 260);
        _nowPosition = AddPosition(now, "Position", settings.NowSingingPosition);
        _nowHeadingStyle = AddStyle(now, "Heading", settings.NowSingingHeadingStyle, 10, 72);
        _nowNameStyle = AddStyle(now, "Singer name", settings.NowSingingNameStyle, 12, 110);
        _nowSongStyle = AddStyle(now, "Song / artist", settings.NowSingingSongStyle, 10, 80);
        _nowBackground = AddColor(now, "Panel background", settings.NowSingingBackgroundColor);
        _nowBorder = AddColor(now, "Panel border", settings.NowSingingBorderColor);

        var queue = AddSection(root, "QUEUE / WAIT STATUS");
        _queueTemplate = AddText(queue, "Message template", settings.QueueStatusTemplate, 520);
        _queueTemplate.ToolTip = "Tokens: {active}, {singerWord}, {hold}, {holdPart}, {playable}, {time}, {timePart}";
        _queueEmpty = AddText(queue, "Empty queue message", settings.QueueEmptyText, 360);
        _queuePosition = AddPosition(queue, "Position", settings.QueueStatusPosition);
        _queueStyle = AddStyle(queue, "Queue text", settings.QueueStatusStyle, 10, 64);
        _queueBackground = AddColor(queue, "Panel background", settings.QueueStatusBackgroundColor);
        _queueBorder = AddColor(queue, "Panel border", settings.QueueStatusBorderColor);

        var venue = AddSection(root, "VENUE HEADER");
        venue.Children.Add(new TextBlock
        {
            Text = "The venue/event text itself is entered in the Venue header box above.",
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 4)
        });
        _venuePosition = AddPosition(venue, "Position", settings.VenueHeaderPosition);
        _venueStyle = AddStyle(venue, "Venue text", settings.VenueHeaderStyle, 10, 80);
        _venueBackground = AddColor(venue, "Panel background", settings.VenueHeaderBackgroundColor);
        _venueBorder = AddColor(venue, "Panel border", settings.VenueHeaderBorderColor);

        var announcement = AddSection(root, "TEMPORARY ANNOUNCEMENT");
        announcement.Children.Add(new TextBlock
        {
            Text = "The announcement message itself is typed in the Audience announcement box above.",
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 4)
        });
        _announcementPosition = AddPosition(announcement, "Position", settings.AnnouncementPosition);
        _announcementStyle = AddStyle(announcement, "Announcement text", settings.AnnouncementStyle, 14, 120);
        _announcementBackground = AddColor(announcement, "Panel background", settings.AnnouncementBackgroundColor);
        _announcementBorder = AddColor(announcement, "Panel border", settings.AnnouncementBorderColor);

        _loading = false;
    }

    internal void WriteTo(AudienceEnhancementSettings settings)
    {
        settings.SingerCallUpHeadingText = _callHeading.Text;
        settings.SingerCallUpPromptText = _callPrompt.Text;
        settings.SingerCallUpPosition = _callPosition.SelectedItem?.ToString() ?? "Center";
        settings.SingerCallUpHeadingStyle = ReadStyle(_callHeadingStyle);
        settings.SingerCallUpNameStyle = ReadStyle(_callNameStyle);
        settings.SingerCallUpSongStyle = ReadStyle(_callSongStyle);
        settings.SingerCallUpPromptStyle = ReadStyle(_callPromptStyle);
        settings.SingerCallUpBackgroundColor = _callBackground.Text;
        settings.SingerCallUpBorderColor = _callBorder.Text;

        settings.NowSingingHeadingText = _nowHeading.Text;
        settings.NowSingingPosition = _nowPosition.SelectedItem?.ToString() ?? "Top Left";
        settings.NowSingingHeadingStyle = ReadStyle(_nowHeadingStyle);
        settings.NowSingingNameStyle = ReadStyle(_nowNameStyle);
        settings.NowSingingSongStyle = ReadStyle(_nowSongStyle);
        settings.NowSingingBackgroundColor = _nowBackground.Text;
        settings.NowSingingBorderColor = _nowBorder.Text;

        settings.QueueStatusTemplate = _queueTemplate.Text;
        settings.QueueEmptyText = _queueEmpty.Text;
        settings.QueueStatusPosition = _queuePosition.SelectedItem?.ToString() ?? "Top Left";
        settings.QueueStatusStyle = ReadStyle(_queueStyle);
        settings.QueueStatusBackgroundColor = _queueBackground.Text;
        settings.QueueStatusBorderColor = _queueBorder.Text;

        settings.VenueHeaderPosition = _venuePosition.SelectedItem?.ToString() ?? "Top Center";
        settings.VenueHeaderStyle = ReadStyle(_venueStyle);
        settings.VenueHeaderBackgroundColor = _venueBackground.Text;
        settings.VenueHeaderBorderColor = _venueBorder.Text;

        settings.AnnouncementPosition = _announcementPosition.SelectedItem?.ToString() ?? "Center";
        settings.AnnouncementStyle = ReadStyle(_announcementStyle);
        settings.AnnouncementBackgroundColor = _announcementBackground.Text;
        settings.AnnouncementBorderColor = _announcementBorder.Text;
    }

    private static StackPanel AddSection(Panel root, string title)
    {
        var group = new GroupBox
        {
            Header = title,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 3, 0, 6),
            Padding = new Thickness(7)
        };
        var panel = new StackPanel();
        group.Content = panel;
        root.Children.Add(group);
        return panel;
    }

    private TextBox AddText(Panel host, string label, string value, double width)
    {
        var row = Row();
        row.Children.Add(Caption(label, 132));
        var box = new TextBox { Width = width, Text = value ?? string.Empty, Margin = new Thickness(3, 1, 3, 1) };
        box.TextChanged += (_, _) => Changed();
        row.Children.Add(box);
        host.Children.Add(row);
        return box;
    }

    private ComboBox AddPosition(Panel host, string label, string value)
    {
        var row = Row();
        row.Children.Add(Caption(label, 132));
        var combo = new ComboBox
        {
            Width = 150,
            ItemsSource = AudienceEnhancementSettingsStore.OverlayPositions,
            SelectedItem = AudienceEnhancementSettingsStore.OverlayPositions.FirstOrDefault(x =>
                string.Equals(x, value, StringComparison.OrdinalIgnoreCase)) ?? "Center",
            Margin = new Thickness(3, 1, 3, 1),
            ToolTip = "Position on the audience display. Top and bottom positions keep clear of the existing scroller bars."
        };
        combo.SelectionChanged += (_, _) => Changed();
        row.Children.Add(combo);
        host.Children.Add(row);
        return combo;
    }

    private StyleFields AddStyle(Panel host, string label, AudienceOverlayTextStyle style, double min, double max)
    {
        var row = Row();
        row.Children.Add(Caption(label, 132));
        var font = new ComboBox
        {
            Width = 180,
            ItemsSource = FontNames,
            SelectedItem = FontNames.FirstOrDefault(x => string.Equals(x, style.FontFamily, StringComparison.OrdinalIgnoreCase))
                ?? FontNames.FirstOrDefault(),
            Margin = new Thickness(3, 1, 3, 1),
            ToolTip = "Font"
        };
        font.SelectionChanged += (_, _) => Changed();
        row.Children.Add(font);

        var size = new Slider
        {
            Width = 120,
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(style.FontSize, min, max),
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Margin = new Thickness(7, 1, 3, 1),
            ToolTip = "Font size"
        };
        var sizeText = Caption($"{size.Value:0}", 38);
        size.ValueChanged += (_, _) => { sizeText.Text = $"{size.Value:0}"; Changed(); };
        row.Children.Add(size);
        row.Children.Add(sizeText);

        row.Children.Add(Caption("Text", 32));
        var color = ColorBox(style.TextColor);
        row.Children.Add(color);
        row.Children.Add(ColorPickButton(color));
        host.Children.Add(row);
        return new StyleFields(font, size, sizeText, color);
    }

    private TextBox AddColor(Panel host, string label, string value)
    {
        var row = Row();
        row.Children.Add(Caption(label, 132));
        var box = ColorBox(value);
        row.Children.Add(box);
        row.Children.Add(ColorPickButton(box));
        host.Children.Add(row);
        return box;
    }

    private TextBox ColorBox(string value)
    {
        var box = new TextBox
        {
            Width = 105,
            Text = value,
            Margin = new Thickness(3, 1, 3, 1),
            ToolTip = "Colour: #RRGGBB or #AARRGGBB"
        };
        box.TextChanged += (_, _) => Changed();
        return box;
    }

    private Button ColorPickButton(TextBox target)
    {
        var button = new Button
        {
            Content = "PICK",
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(2, 1, 3, 1),
            ToolTip = "Choose a colour. If the current value contains transparency, its alpha value is preserved."
        };
        button.Click += (_, _) => PickColor(target);
        return button;
    }

    private void PickColor(TextBox target)
    {
        var (alpha, red, green, blue) = ParseArgb(target.Text);
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(red, green, blue)
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        target.Text = $"#{alpha:X2}{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
    }

    private static (byte Alpha, byte Red, byte Green, byte Blue) ParseArgb(string? value)
    {
        var hex = (value ?? string.Empty).Trim().TrimStart('#');
        try
        {
            if (hex.Length == 8)
                return (Convert.ToByte(hex[..2], 16), Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16), Convert.ToByte(hex.Substring(6, 2), 16));
            if (hex.Length == 6)
                return (255, Convert.ToByte(hex[..2], 16), Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16));
        }
        catch { }
        return (255, 255, 255, 255);
    }

    private static StackPanel Row() => new()
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 1, 0, 1)
    };

    private static TextBlock Caption(string text, double width) => new()
    {
        Text = text,
        Width = width,
        Foreground = Brushes.White,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static AudienceOverlayTextStyle ReadStyle(StyleFields fields) => new()
    {
        FontFamily = fields.Font.SelectedItem?.ToString() ?? "Segoe UI",
        FontSize = fields.Size.Value,
        TextColor = fields.Color.Text
    };

    private void Changed()
    {
        if (!_loading) SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
