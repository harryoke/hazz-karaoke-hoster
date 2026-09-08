using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using NAudio.CoreAudioApi;
namespace HazzKaraokeHoster.App;
public partial class MainWindow
{
    private sealed class SoundRoutes { public string? Deck1 { get; set; } public string? Deck2 { get; set; } public string? Quick { get; set; } public string? Karaoke { get; set; } }
    private SoundRoutes _soundRoutes = new();
    private string SoundRoutesPath => Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!, "sound-devices.json");
    private void LoadSoundRoutes()
    {
        try { if (File.Exists(SoundRoutesPath)) _soundRoutes = JsonSerializer.Deserialize<SoundRoutes>(File.ReadAllText(SoundRoutesPath)) ?? new(); }
        catch (Exception ex) { App.WriteDiagnostic("SOUND DEVICES", ex.ToString()); }
        LoadNormalization();
        ApplySoundRoutes();
        foreach (var player in new[] { DeckAMedia, DeckBMedia, QuickMusicMedia, StandbyMusicMedia })
            player.RoutingFailed += ex => {
                if (ReferenceEquals(player, StandbyMusicMedia)) { _cueReady = false; _cueStarted = DateTime.MaxValue; StandbyMusicMedia.Close(); }
                NextCueStatus.Text = "Audio output problem: " + ex.Message;
                App.WriteDiagnostic("AUDIO OUTPUT", ex.ToString());
            };
    }
    private void ApplySoundRoutes()
    {
        DeckAMedia.OutputDeviceId = _soundRoutes.Deck1; DeckBMedia.OutputDeviceId = _soundRoutes.Deck2;
        QuickMusicMedia.OutputDeviceId = _soundRoutes.Quick; _pitchAudio.OutputDeviceId = _soundRoutes.Karaoke;
    }
    private void PlayerSoundDevices_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMusicDeck != MusicDeckId.None || _quickSearchMusicActive || _karaokePlaying || _karaokePaused)
        { MessageBox.Show(this, "Stop the players before changing sound devices.", "Player Sound Devices"); return; }
        using var enumerator = new MMDeviceEnumerator();
        var devices = new List<KeyValuePair<string,string>> { new("", "Windows default output") };
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        { devices.Add(new(device.ID, device.FriendlyName)); device.Dispose(); }
        var window = new Window { Title = "Player Sound Devices", Owner = this, Width = 520, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(18) }; window.Content = panel;
        var selections = new List<ComboBox>();
        foreach (var row in new[] { ("Deck 1", _soundRoutes.Deck1), ("Deck 2", _soundRoutes.Deck2), ("Space-bar quick-play", _soundRoutes.Quick), ("Karaoke", _soundRoutes.Karaoke) })
        {
            panel.Children.Add(new TextBlock { Text = row.Item1, Margin = new Thickness(0,8,0,4) });
            var choices = devices.ToList();
            if (!string.IsNullOrEmpty(row.Item2) && !choices.Any(x => x.Key == row.Item2)) choices.Add(new(row.Item2, "Disconnected saved device"));
            var combo = new ComboBox { ItemsSource = choices, DisplayMemberPath = "Value", SelectedValuePath = "Key", SelectedValue = row.Item2 ?? "", MinHeight = 30 };
            panel.Children.Add(combo); selections.Add(combo);
        }
        panel.Children.Add(new TextBlock { Text = "Saved outputs are used on the next track load. A disconnected selected output is reported instead of silently using another device.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,12) });
        var save = new Button { Content = "SAVE", MinHeight = 32 }; panel.Children.Add(save);
        save.Click += (_, _) => { window.DialogResult = true; };
        if (window.ShowDialog() != true) return;
        _soundRoutes = new() { Deck1 = selections[0].SelectedValue as string, Deck2 = selections[1].SelectedValue as string, Quick = selections[2].SelectedValue as string, Karaoke = selections[3].SelectedValue as string };
        File.WriteAllText(SoundRoutesPath, JsonSerializer.Serialize(_soundRoutes));
        DeckAMedia.Close(); DeckBMedia.Close(); QuickMusicMedia.Close(); StandbyMusicMedia.Close();
        _cueGeneration++; _cuePath = null; _cueReady = false;
        LoadNormalization();
        ApplySoundRoutes();
    }
}
