using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using HazzKaraokeHoster.Playback;
namespace HazzKaraokeHoster.App;
public partial class MainWindow
{
    private sealed class NormalizationSettings { public bool Enabled { get; set; } public double TargetDb { get; set; } = -18; }
    private string NormalizationPath => Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!, "audio-normalization.json");
    private void LoadNormalization()
    {
        try
        {
            var settings = File.Exists(NormalizationPath) ? JsonSerializer.Deserialize<NormalizationSettings>(File.ReadAllText(NormalizationPath)) : null;
            AudioNormalization.Enabled = settings?.Enabled ?? false;
            AudioNormalization.TargetDb = settings?.TargetDb ?? -18;
        }
        catch (Exception ex) { App.WriteDiagnostic("NORMALIZATION SETTINGS", ex.ToString()); }
    }
    private void Normalization_Click(object sender, RoutedEventArgs e)
    {
        var window = new Window { Owner = this, Title = "Normalize All Audio", Width = 470, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(20) }; window.Content = panel;
        var enabled = new CheckBox { Content = "NORMALIZE ALL AUDIO", IsChecked = AudioNormalization.Enabled, Margin = new Thickness(0,0,0,16) };
        panel.Children.Add(enabled);
        var label = new TextBlock(); panel.Children.Add(label);
        var level = new Slider { Minimum = -24, Maximum = -12, TickFrequency = 1, IsSnapToTickEnabled = true, Value = AudioNormalization.TargetDb, Margin = new Thickness(0,12,0,12) }; panel.Children.Add(level);
        panel.Children.Add(new TextBlock { Text = "Quieter ← Target level → Louder\nRecommended: -18 dB RMS\n\nApplies live to both music decks, quick-play and karaoke. Gradual volume levelling with a per-player peak limit; original files are unchanged. Deck volumes and fades still work. This is RMS levelling, not measured LUFS normalization. Multiple outputs mixed together can still overload an external mixer.", TextWrapping = TextWrapping.Wrap });
        void Apply()
        {
            if (enabled.IsChecked == true && (_karaokePlaying || _karaokePaused) && !_pitchAudio.IsLoaded)
            {
                enabled.IsChecked = false;
                MessageBox.Show(window, "Stop and reload karaoke before enabling normalization. This track is using the fallback audio path.", "Normalize Audio");
            }
            AudioNormalization.Enabled = enabled.IsChecked == true;
            AudioNormalization.TargetDb = level.Value;
            label.Text = $"Target level: {AudioNormalization.TargetDb:0} dB RMS";
        }
        enabled.Checked += (_, _) => Apply(); enabled.Unchecked += (_, _) => Apply(); level.ValueChanged += (_, _) => Apply(); Apply();
        var close = new Button { Content = "SAVE AND CLOSE", MinHeight = 32, Margin = new Thickness(0,16,0,0) }; panel.Children.Add(close);
        close.Click += (_, _) => window.Close();
        window.Closed += (_, _) => { try { File.WriteAllText(NormalizationPath, JsonSerializer.Serialize(new NormalizationSettings { Enabled = AudioNormalization.Enabled, TargetDb = AudioNormalization.TargetDb })); } catch (Exception ex) { App.WriteDiagnostic("NORMALIZATION SAVE", ex.ToString()); } };
        window.ShowDialog();
    }
}
