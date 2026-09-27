using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;
public partial class MainWindow
{
    private string _crossfadePreset = "Linear", _runningCrossfadePreset = "Linear";
    private void CrossfadePreset_Click(object sender, RoutedEventArgs e)
    {
        var window = new Window { Owner = this, Title = "Music crossfade presets", Width = 470, Height = Math.Min(440, SystemParameters.WorkArea.Height - 30), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(18) }; window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var choice = new ComboBox { ItemsSource = CrossfadeCurve.Names, SelectedItem = _crossfadePreset, MinHeight = 30 }; panel.Children.Add(choice);
        var graph = new Canvas { Width = 400, Height = 140, Background = Brushes.Black, Margin = new Thickness(0,12,0,12) }; panel.Children.Add(graph);
        var description = new TextBlock { TextWrapping = TextWrapping.Wrap }; panel.Children.Add(description);
        void Draw()
        {
            graph.Children.Clear();
            var outgoing = new Polyline { Stroke = Brushes.Orange, StrokeThickness = 2 };
            var incoming = new Polyline { Stroke = Brushes.DeepSkyBlue, StrokeThickness = 2 };
            for (int i = 0; i <= 100; i++) { var g = CrossfadeCurve.Gains(choice.SelectedItem as string, i / 100d); outgoing.Points.Add(new Point(i*4, 135-g.Outgoing*130)); incoming.Points.Add(new Point(i*4,135-g.Incoming*130)); }
            graph.Children.Add(outgoing); graph.Children.Add(incoming);
            description.Text = "Orange: outgoing track. Blue: incoming track. Applies to automatic deck-to-deck fades and FADE NOW. The TIME slider sets duration. Equal power and overlap can sound louder in the middle; leave headroom on your mixer. Karaoke handoff fades stay unchanged.";
        }
        choice.SelectionChanged += (_,_) => Draw(); Draw();
        var save = new Button { Content = "USE THIS PRESET", Margin = new Thickness(0,12,0,0), MinHeight = 32 }; panel.Children.Add(save);
        save.Click += (_,_) => { _crossfadePreset = CrossfadeCurve.Normalize(choice.SelectedItem as string); SaveMainLayout(); window.Close(); };
        window.ShowDialog();
    }
}
