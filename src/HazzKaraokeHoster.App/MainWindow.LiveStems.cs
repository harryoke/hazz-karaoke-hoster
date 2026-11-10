using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private Window? _liveStemsWindow;

    private void OpenLiveStems_Click(object sender, RoutedEventArgs e)
    {
        if (_liveStemsWindow is not null)
        {
            if (_liveStemsWindow.WindowState == WindowState.Minimized) _liveStemsWindow.WindowState = WindowState.Normal;
            _liveStemsWindow.Activate();
            return;
        }

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = "Play a track, then mute parts or select multiple Solo buttons to hear them together. The crossfader still mixes both decks.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10)
        });
        bool installed = LiveStemWorker.Available;
        if (!installed) panel.Children.Add(new TextBlock
        {
            Text = "Live Stems engine missing. Extract the optional engine ZIP into the folder containing Hazz Karaoke Hoster.exe and restart.",
            TextWrapping = TextWrapping.Wrap
        });

        var updates = new List<Action>();
        void AddDeck(string name, RoutedMusicElement media)
        {
            var group = new StackPanel { Margin = new Thickness(0, 10, 0, 8) };
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            group.Children.Add(status);
            var switches = new List<(int bit, Button mute, Button solo)>();
            var row = new WrapPanel();
            var labels = new[] { "Drums", "Bass", "Other", "Vocals" };
            foreach (int index in new[] { 3, 0, 1, 2 })
            {
                int bit = 1 << index;
                var part = new StackPanel { Margin = new Thickness(0, 0, 8, 8), Width = 92 };
                part.Children.Add(new TextBlock { Text = labels[index], HorizontalAlignment = HorizontalAlignment.Center });
                var mute = new Button { Margin = new Thickness(0, 3, 0, 0), ToolTip = "Mute " + labels[index] };
                var solo = new Button { Margin = new Thickness(0, 3, 0, 0), ToolTip = "Solo " + labels[index] };
                System.Windows.Automation.AutomationProperties.SetName(mute, "Mute " + labels[index] + " on " + name);
                System.Windows.Automation.AutomationProperties.SetName(solo, "Solo " + labels[index] + " on " + name);
                mute.Click += (_, _) => media.SetStemMix(media.StemMuted ^ bit, 0);
                solo.Click += (_, _) => media.SetStemMix(media.StemMuted, media.StemSolo ^ bit);
                switches.Add((bit, mute, solo));
                part.Children.Add(mute); part.Children.Add(solo); row.Children.Add(part);
            }
            var reset = new Button { Content = "Restore original", Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Center };
            reset.Click += (_, _) => media.SetStemMix(0, 0);
            row.Children.Add(reset); group.Children.Add(row); panel.Children.Add(group);
            updates.Add(() =>
            {
                bool enabled = installed && media.CanUseStems;
                if (enabled) media.WarmStems();
                status.Text = name + " • " + (installed ? media.StemStatus : "Engine not installed");
                foreach (var (bit, mute, solo) in switches)
                {
                    mute.IsEnabled = solo.IsEnabled = enabled;
                    mute.Content = (media.StemMuted & bit) != 0 ? "Muted" : "Mute";
                    solo.Content = (media.StemSolo & bit) != 0 ? "Solo on" : "Solo";
                }
                reset.IsEnabled = enabled && (media.StemMuted != 0 || media.StemSolo != 0);
            });
        }

        AddDeck("Deck 1", DeckAMedia);
        AddDeck("Deck 2", DeckBMedia);
        var window = _liveStemsWindow = new Window
        {
            Title = "Hazz • Live Stems", Width = 590, MinWidth = 430, MinHeight = 285,
            SizeToContent = SizeToContent.Height, Owner = this, ShowInTaskbar = false,
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        void Refresh() { foreach (var update in updates) update(); }
        timer.Tick += (_, _) => Refresh();
        window.Closed += (_, _) => { timer.Stop(); _liveStemsWindow = null; };
        Refresh(); timer.Start(); window.Show();
    }
}
