using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private double _karaokeTempo = 1;
    private Window? _tempoWindow;
    private void CloseTempoEditor() { _tempoWindow?.Close(); _tempoWindow = null; }
    private string? TempoSinger => _activeSinger is null ? null :
        (_activeSingerVenue?.ToString() ?? "default") + ":" + _activeSinger.SingerName.Trim();

    private double LoadTempo(string path, string? singer = null)
    {
        try { return TempoPreferences.Default.Load(path, singer); }
        catch (Exception ex) { SearchStatus.Text = "Could not read saved tempo: " + ex.Message; return 1; }
    }

    private void SetKaraokeTempo(double tempo)
    {
        _karaokeTempo = Math.Clamp(tempo, 0.75, 1.25);
        _pitchAudio.SetTempo(_karaokeTempo);
        KaraokeMedia.SpeedRatio = _karaokeTempo;
        _audience?.SetKaraokeTempo(_karaokeTempo);
    }

    private void Tempo_Click(object sender, RoutedEventArgs e)
    {
        if (_tempoWindow is not null) { _tempoWindow.Activate(); return; }
        var karaoke = _karaokePlaying || _karaokePaused;
        var music = karaoke ? null : _quickSearchMusicActive ? QuickMusicMedia :
            _activeMusicDeck != MusicDeckId.None ? MediaFor(_activeMusicDeck) :
            _deck1Paused ? DeckAMedia : _deck2Paused ? DeckBMedia : null;
        if (!karaoke && music is null) { SearchStatus.Text = "Start or pause a song, then choose TEMPO."; return; }
        var target = karaoke ? "Karaoke" : ReferenceEquals(music, QuickMusicMedia) ? "Quick music" : ReferenceEquals(music, DeckAMedia) ? "Deck 1" : "Deck 2";
        var path = music is null ? _karaokePackage?.SourcePath : music.Source?.LocalPath;
        if (path is null) { SearchStatus.Text = "Load a track on this deck before adjusting tempo."; return; }
        if (music is null && !_pitchAudio.IsLoaded)
        {
            SearchStatus.Text = "Tempo is unavailable for this file because its audio could not be decoded.";
            return;
        }
        // Capture identity: a queued transition must never apply this dialog to another song.
        var package = _karaokePackage;
        var source = music?.Source;
        var singer = music is null ? TempoSinger : null;
        bool IsCurrent() => music is null ? ReferenceEquals(package, _karaokePackage) : source == music.Source;
        var panel = new StackPanel { Margin = new Thickness(20) };
        var window = new Window { Owner = this, Title = target + " tempo", Width = 460,
            SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(18, 27, 35)), Foreground = Brushes.White, Content = panel };
        panel.Children.Add(new TextBlock { Text = Path.GetFileNameWithoutExtension(path), TextWrapping = TextWrapping.Wrap, FontSize = 18 });
        panel.Children.Add(new TextBlock { Text = "Slower ← 100% original tempo → Faster\nPitch/key stays unchanged.", Margin = new Thickness(0, 12, 0, 8) });
        var label = new TextBlock { FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(label);
        var slider = new Slider { Minimum = 75, Maximum = 125, TickFrequency = 1, IsSnapToTickEnabled = true,
            Value = (music?.Tempo ?? _karaokeTempo) * 100, Margin = new Thickness(0, 8, 0, 8) };
        label.Text = $"{slider.Value:0}%";
        panel.Children.Add(slider);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
        slider.ValueChanged += (_, _) =>
        {
            if (!IsCurrent()) { window.Close(); return; }
            label.Text = $"{slider.Value:0}%";
            if (music is null) SetKaraokeTempo(slider.Value / 100);
            else music.Tempo = slider.Value / 100;
            status.Text = "Applied to this playback. Choose Save to remember it.";
        };
        void Button(string text, Action action)
        {
            var button = new Button { Content = text, Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(10),
                Background = new SolidColorBrush(Color.FromRgb(38, 91, 126)), Foreground = Brushes.White };
            button.Click += (_, _) => action(); panel.Children.Add(button);
        }
        void Save(string? who)
        {
            if (!IsCurrent()) { window.Close(); return; }
            try { TempoPreferences.Default.Save(path, who, slider.Value / 100); status.Text = who is null ? "Saved as this track's default tempo." : "Saved for this singer and song at this venue."; }
            catch (Exception ex) { status.Text = "Tempo was not saved: " + ex.Message; }
        }
        Button("RESET TO 100%", () => slider.Value = 100);
        Button("SAVE FOR THIS TRACK", () => Save(null));
        if (singer is not null) Button("SAVE FOR THIS SINGER AND SONG", () => Save(singer));
        panel.Children.Add(status);
        panel.Children.Add(new TextBlock { Text = "Singer settings override track defaults. Reset and save to remember 100%. Original files are never changed.", TextWrapping = TextWrapping.Wrap });
        Button("CLOSE", window.Close);
        _tempoWindow = window;
        window.Closed += (_, _) => { if (ReferenceEquals(_tempoWindow, window)) _tempoWindow = null; };
        window.Show();
    }
}
