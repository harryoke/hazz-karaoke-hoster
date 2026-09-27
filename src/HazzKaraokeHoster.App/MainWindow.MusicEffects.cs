using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HazzKaraokeHoster.Playback;
namespace HazzKaraokeHoster.App;
public partial class MainWindow
{
    private Window? _musicEffectsWindow;
    private bool _musicFxIndicatorsConnected;
    private void MusicEffects_Click(object sender,RoutedEventArgs e)
    {
        if(!_musicFxIndicatorsConnected)
        {
            _musicFxIndicatorsConnected=true;
            void UpdateIndicator()
            {
                var active=DeckAMedia.Effects.Enabled || DeckBMedia.Effects.Enabled;
                MusicEffectsButton.Content=active?"MUSIC FX ON":"MUSIC FX";
                MusicEffectsButton.Background=active?System.Windows.Media.Brushes.DarkGreen:System.Windows.Media.Brushes.DarkSlateGray;
            }
            DeckAMedia.Effects.PropertyChanged+=(_,_)=>UpdateIndicator(); DeckBMedia.Effects.PropertyChanged+=(_,_)=>UpdateIndicator(); UpdateIndicator();
        }
        if(_musicEffectsWindow is not null) { _musicEffectsWindow.Activate(); return; }
        var window=_musicEffectsWindow=new Window { Owner=this,Title="Music effects",Width=500,Height=Math.Min(550,SystemParameters.WorkArea.Height-30),WindowStartupLocation=WindowStartupLocation.CenterOwner };
        window.Closed+=(_,_)=>_musicEffectsWindow=null;
        var panel=new StackPanel { Margin=new Thickness(16) }; window.Content=new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text="Effects change the music deck audio only, including music-video sound. Karaoke, search preview and soundbite pads are unaffected. One effect per deck. Start playback, then enable an effect. Stop/end switches it off for the next track.",TextWrapping=TextWrapping.Wrap });
        void Add(string title,MusicEffectsState state)
        {
            var section=new StackPanel { DataContext=state,Margin=new Thickness(0,16,0,0) }; panel.Children.Add(section);
            section.Children.Add(new TextBlock { Text=title,FontWeight=FontWeights.Bold });
            var enabled=new CheckBox { Content="EFFECT ON",Margin=new Thickness(0,8,0,8) }; enabled.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(MusicEffectsState.Enabled)) { Mode=BindingMode.TwoWay }); section.Children.Add(enabled);
            var mode=new ComboBox { ItemsSource=MusicEffectsState.Modes,MinHeight=30 }; mode.SetBinding(ComboBox.SelectedItemProperty,new Binding(nameof(MusicEffectsState.Mode)) { Mode=BindingMode.TwoWay }); section.Children.Add(mode);
            var level=new TextBlock(); level.SetBinding(TextBlock.TextProperty,new Binding(nameof(MusicEffectsState.MixPercent)) { StringFormat="Effect level: {0:0}% (0% = dry)" }); section.Children.Add(level);
            var amount=new Slider { Minimum=0,Maximum=60,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,6,0,6) }; amount.SetBinding(Slider.ValueProperty,new Binding(nameof(MusicEffectsState.MixPercent)) { Mode=BindingMode.TwoWay }); section.Children.Add(amount);
            var bypass=new Button { Content="BYPASS THIS DECK" }; bypass.Click+=(_,_)=>state.Enabled=false; section.Children.Add(bypass);
        }
        Add("DECK 1",DeckAMedia.Effects); Add("DECK 2 — inactive in single-deck mode",DeckBMedia.Effects);
        var all=new Button { Content="BYPASS BOTH DECKS",Margin=new Thickness(0,16,0,0) }; all.Click+=(_,_)=> { DeckAMedia.Effects.Enabled=false; DeckBMedia.Effects.Enabled=false; }; panel.Children.Add(all);
        window.Show();
    }
}
