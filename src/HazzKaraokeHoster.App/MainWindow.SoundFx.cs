using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private SoundFxSettings _soundFx = new();
    private CancellationTokenSource? _soundFxCts;
    private RoutedMusicElement? _soundFxPlayer;
    private int _soundFxPlaying = -1;
    private bool _soundFxClosing;
    private string SoundFxPath => Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!, "sound-fx.json");

    private void InitializeSoundFx()
    {
        try { if(File.Exists(SoundFxPath)) _soundFx=JsonSerializer.Deserialize<SoundFxSettings>(File.ReadAllText(SoundFxPath)) ?? new(); }
        catch(Exception ex) { App.WriteDiagnostic("SOUND FX SETTINGS",ex.ToString()); SearchStatus.Text="Sound FX settings could not be read. Original file kept; edit a pad to configure."; }
        _soundFx.Normalize(); RefreshSoundFxButtons();
    }
    private void RefreshSoundFxButtons()
    {
        if(SoundFxButtons is null) return;
        foreach(var button in SoundFxButtons.Children.OfType<Button>().Where(b=>b.Tag is not null))
        {
            var index=int.Parse(button.Tag.ToString()!);var pad=_soundFx.Pads[index];
            Color colour;
            try { colour=(Color)ColorConverter.ConvertFromString(pad.Colour); } catch { colour=Color.FromRgb(39,97,128); }
            colour.A=255;
            if(_soundFxPlaying==index) colour=Color.FromRgb((byte)(colour.R+(255-colour.R)*0.4),(byte)(colour.G+(255-colour.G)*0.4),(byte)(colour.B+(255-colour.B)*0.4));
            button.Background=new SolidColorBrush(colour);
            button.Foreground=(colour.R*0.299+colour.G*0.587+colour.B*0.114)>155 ? Brushes.Black : Brushes.White;
            button.Content=new TextBlock { Text=pad.Label,TextTrimming=TextTrimming.CharacterEllipsis };
            button.BorderBrush=_soundFxPlaying==index ? Brushes.White : Brushes.SlateGray;
            button.ToolTip=$"{index+1}: {pad.Label}\n{(string.IsNullOrWhiteSpace(pad.FilePath) ? "Not assigned" : pad.FilePath)}\nClick to play; right-click to edit."+(_soundFx.KamikazeSlot==index ? "\nKamikaze soundbite" : "");
        }
    }
    private async void SoundFx_Click(object sender,RoutedEventArgs e)
    {
        if(sender is not Button button || !int.TryParse(button.Tag?.ToString(),out var index)) return;
        if(string.IsNullOrWhiteSpace(_soundFx.Pads[index].FilePath)) { EditSoundFx(index);return; }
        await PlaySoundFxAsync(index,false);
    }
    private void SoundFx_Edit(object sender,MouseButtonEventArgs e)
    {
        if(sender is Button b && int.TryParse(b.Tag?.ToString(),out var index)) { e.Handled=true;EditSoundFx(index); }
    }
    private async void SoundFx_Stop(object sender,RoutedEventArgs e) => await StopSoundFxAsync();
    private void SetFxDuck(double factor)
    {
        DeckAMedia.Attenuation=DeckBMedia.Attenuation=QuickMusicMedia.Attenuation=factor;
    }
    private async Task FadeFxDuckAsync(double target,int milliseconds,CancellationToken token)
    {
        var start=DeckAMedia.Attenuation;var clock=System.Diagnostics.Stopwatch.StartNew();
        while(clock.ElapsedMilliseconds<milliseconds)
        {
            token.ThrowIfCancellationRequested();
            SetFxDuck(start+(target-start)*Math.Clamp(clock.Elapsed.TotalMilliseconds/milliseconds,0,1));
            await Task.Delay(20,token);
        }
        token.ThrowIfCancellationRequested();SetFxDuck(target);
    }
    private void CloseFxPlayer()
    {
        var player=_soundFxPlayer;_soundFxPlayer=null;
        if(player is not null) { player.Close();SoundFxPlayerHost.Children.Remove(player); }
        _soundFxPlaying=-1;
    }
    private async Task StopSoundFxAsync()
    {
        _soundFxCts?.Cancel();CloseFxPlayer();
        var owner=new CancellationTokenSource();_soundFxCts=owner;RefreshSoundFxButtons();
        try { await FadeFxDuckAsync(1,300,owner.Token); }
        catch(OperationCanceledException) { }
        finally { if(ReferenceEquals(_soundFxCts,owner)) _soundFxCts=null;owner.Dispose(); }
    }
    private async Task PlaySoundFxAsync(int index,bool kamikaze)
    {
        if(_soundFxClosing || index is <0 or >8) return;
        var pad=_soundFx.Pads[index];
        if(!File.Exists(pad.FilePath)) { SearchStatus.Text=$"Sound FX: file missing for {pad.Label}. Right-click the pad to choose another file.";return; }
        _soundFxCts?.Cancel();CloseFxPlayer();
        var owner=new CancellationTokenSource();_soundFxCts=owner;var token=owner.Token;
        var player=new RoutedMusicElement { Volume=pad.Volume,OutputDeviceId=_soundFx.OutputDeviceId,Width=1,Height=1,IsMuted=false };
        _soundFxPlayer=player;SoundFxPlayerHost.Children.Add(player);
        var opened=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ended=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        player.MediaOpened+=(_,_)=>opened.TrySetResult();
        player.MediaEnded+=(_,_)=>ended.TrySetResult();
        Exception? playbackError=null;
        void Failed(Exception ex) { playbackError=ex;opened.TrySetException(ex);ended.TrySetResult(); }
        player.MediaFailed+=(_,e)=>Failed(e.ErrorException);
        player.RoutingFailed+=Failed;
        try
        {
            player.Source=new Uri(Path.GetFullPath(pad.FilePath));
            // Prepare silently first so an unreadable file cannot hold music down.
            player.IsMuted=true;player.Play();
            await opened.Task.WaitAsync(TimeSpan.FromSeconds(8),token);
            token.ThrowIfCancellationRequested();player.Pause();player.Position=TimeSpan.Zero;
            if(ended.Task.IsCompleted) throw new InvalidOperationException("Soundbite could not be prepared.");
            await FadeFxDuckAsync(kamikaze || pad.DuckMusic ? 0 : 1,250,token);
            token.ThrowIfCancellationRequested();_soundFxPlaying=index;RefreshSoundFxButtons();
            player.IsMuted=false;player.Play();
            await ended.Task.WaitAsync(TimeSpan.FromMinutes(10),token);
            if(playbackError is not null) throw playbackError;
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) { App.WriteDiagnostic("SOUND FX",ex.ToString());SearchStatus.Text=$"Sound FX: {pad.Label} could not play. {ex.Message}"; }
        finally
        {
            if(ReferenceEquals(_soundFxCts,owner))
            {
                CloseFxPlayer();RefreshSoundFxButtons();
                try { await FadeFxDuckAsync(1,400,token); } catch(OperationCanceledException) { }
                if(ReferenceEquals(_soundFxCts,owner)) _soundFxCts=null;
            }
            owner.Dispose();
        }
    }
    private void DisposeSoundFx()
    {
        _soundFxClosing=true;_soundFxCts?.Cancel();CloseFxPlayer();SetFxDuck(1);
    }
    private void EditSoundFx(int index)
    {
        if(_soundFxClosing) return;
        var original=_soundFx.Pads[index];
        var window=new Window { Title=$"Sound FX {index+1} — edit",Owner=this,Width=520,Height=570,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brushes.White,Foreground=Brushes.Black };
        var panel=new StackPanel { Margin=new Thickness(18) };window.Content=new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        TextBox Entry(string title,string value) { panel.Children.Add(new TextBlock { Text=title,Margin=new Thickness(0,8,0,2) });var box=new TextBox { Text=value,Foreground=Brushes.Black,Background=Brushes.White };panel.Children.Add(box);return box; }
        var label=Entry("Button name (up to 40 characters)",original.Label);label.MaxLength=40;
        var file=Entry("Soundbite file",original.FilePath);
        var browse=new Button { Content="CHOOSE AUDIO FILE…" };panel.Children.Add(browse);
        browse.Click+=(_,_)=> { var dialog=new OpenFileDialog { Filter="Audio files|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac|All files|*.*" };if(dialog.ShowDialog(window)==true) file.Text=dialog.FileName; };
        var colour=Entry("Button colour (#RRGGBB)",original.Colour);
        var chooseColour=new Button { Content="CHOOSE COLOUR…" };panel.Children.Add(chooseColour);
        chooseColour.Click+=(_,_)=> { using var dialog=new System.Windows.Forms.ColorDialog();if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK) colour.Text=$"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"; };
        panel.Children.Add(new TextBlock { Text="Soundbite volume",Margin=new Thickness(0,8,0,2) });
        var volume=new Slider { Minimum=0,Maximum=1,Value=original.Volume,TickFrequency=0.05 };panel.Children.Add(volume);
        var duck=new CheckBox { Content="Fade music down while this soundbite plays",IsChecked=original.DuckMusic,Foreground=Brushes.Black };panel.Children.Add(duck);
        var link=new CheckBox { Content="Use this soundbite for Kamikaze announcements",IsChecked=_soundFx.KamikazeSlot==index,Foreground=Brushes.Black };panel.Children.Add(link);
        panel.Children.Add(new TextBlock { Text="Sound FX output (all nine pads)",Margin=new Thickness(0,8,0,2) });
        var devices=new List<KeyValuePair<string,string>> { new("","Windows default output") };
        try { using var enumerator=new MMDeviceEnumerator();foreach(var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active)) { devices.Add(new(device.ID,device.FriendlyName));device.Dispose(); } }
        catch(Exception ex) { App.WriteDiagnostic("SOUND FX DEVICES",ex.ToString()); }
        if(!string.IsNullOrEmpty(_soundFx.OutputDeviceId) && !devices.Any(d=>d.Key==_soundFx.OutputDeviceId)) devices.Add(new(_soundFx.OutputDeviceId,"Saved device (disconnected)"));
        var output=new ComboBox { ItemsSource=devices,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=_soundFx.OutputDeviceId ?? "",Foreground=Brushes.Black,Background=Brushes.White };panel.Children.Add(output);
        var status=new TextBlock { Foreground=Brushes.DarkRed,TextWrapping=TextWrapping.Wrap };panel.Children.Add(status);
        var save=new Button { Content="SAVE",Foreground=Brushes.White };panel.Children.Add(save);
        save.Click+=(_,_)=> {
            try {
                _=(Color)ColorConverter.ConvertFromString(colour.Text);
                if(!string.IsNullOrWhiteSpace(file.Text) && !File.Exists(file.Text)) throw new IOException("Choose an existing soundbite file, or leave the file blank to clear the pad.");
                if(link.IsChecked==true && string.IsNullOrWhiteSpace(file.Text)) throw new IOException("Choose a file before linking this pad to Kamikaze.");
                var next=JsonSerializer.Deserialize<SoundFxSettings>(JsonSerializer.Serialize(_soundFx))!;
                next.Pads[index]=new() { Label=label.Text,FilePath=file.Text.Trim(),Colour=colour.Text,Volume=volume.Value,DuckMusic=duck.IsChecked==true };
                next.KamikazeSlot=link.IsChecked==true ? index : next.KamikazeSlot==index ? -1 : next.KamikazeSlot;
                next.OutputDeviceId=output.SelectedValue as string;next.Normalize();
                Directory.CreateDirectory(Path.GetDirectoryName(SoundFxPath)!);
                File.WriteAllText(SoundFxPath+".tmp",JsonSerializer.Serialize(next,new JsonSerializerOptions { WriteIndented=true }));
                if(File.Exists(SoundFxPath)) File.Copy(SoundFxPath,SoundFxPath+".previous",true);
                File.Move(SoundFxPath+".tmp",SoundFxPath,true);
                _soundFx=next;RefreshSoundFxButtons();window.Close();
            } catch(Exception ex) { status.Text=ex.Message; }
        };
        window.ShowDialog();
    }
}
