using System.ComponentModel;
using NAudio.Wave;

namespace HazzKaraokeHoster.Playback;
public sealed record MusicEffectOptions(bool Enabled, string Mode, double Mix);
public sealed class MusicEffectsState : INotifyPropertyChanged
{
    public static readonly string[] Modes = ["Flanger", "Reverb", "Echo"];
    private MusicEffectOptions _options = new(false,"Flanger",.25);
    public MusicEffectOptions Options => Volatile.Read(ref _options);
    public bool Enabled { get=>Options.Enabled; set=>Set(Options with { Enabled=value }); }
    public string Mode { get=>Options.Mode; set=>Set(Options with { Mode=Modes.Contains(value)?value:Modes[0] }); }
    public double MixPercent { get=>Options.Mix*100; set=>Set(Options with { Mix=double.IsFinite(value)?Math.Clamp(value/100,0,.6):0 }); }
    private void Set(MusicEffectOptions value) { Volatile.Write(ref _options,value); PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null)); }
    public event PropertyChangedEventHandler? PropertyChanged;
}

// Delay storage belongs exclusively to the audio callback. UI changes publish immutable options.
public sealed class MusicEffectsSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly Func<MusicEffectOptions> _settings;
    private readonly float[] _delay;
    private readonly int _frames, _channels, _rate;
    private readonly double _ramp;
    private int _position, _reset, _seenReset;
    private string _mode="Flanger";
    private double _wet, _phase;
    public WaveFormat WaveFormat=>_source.WaveFormat;
    public MusicEffectsSampleProvider(ISampleProvider source,Func<MusicEffectOptions> settings)
    {
        _source=source; _settings=settings; _channels=WaveFormat.Channels; _rate=WaveFormat.SampleRate;
        _frames=Math.Max(2,_rate); _delay=new float[checked(_frames*_channels)]; _ramp=1d/Math.Max(1,_rate*.025);
    }
    public void Reset()=>Interlocked.Increment(ref _reset);
    private void Clear() { Array.Clear(_delay); _position=0; _phase=0; }
    private double Tap(double milliseconds,int channel)
    {
        var frame=_position-Math.Clamp(milliseconds*_rate/1000,1,_frames-1);
        if(frame<0)frame+=_frames;
        var i=(int)frame; var next=(i+1)%_frames; var part=frame-i;
        return _delay[i*_channels+channel]*(1-part)+_delay[next*_channels+channel]*part;
    }
    public int Read(float[] buffer,int offset,int count)
    {
        var read=_source.Read(buffer,offset,count);
        var reset=Volatile.Read(ref _reset);
        if(reset!=_seenReset) { _seenReset=reset; Clear(); _wet=0; }
        var settings=_settings();
        var desired=settings.Enabled?settings.Mix:0;
        // Exact dry bypass once the short ramp completes: no colouration or extra limiting.
        if(desired==0 && _wet==0)return read;
        for(int i=offset;i<offset+read;i+=_channels)
        {
            if(_mode!=settings.Mode && _wet==0) { Clear(); _mode=settings.Mode; }
            var target=_mode==settings.Mode?desired:0;
            _wet += Math.Clamp(target-_wet,-_ramp,_ramp);
            var flangeMs=3.5+3*Math.Sin(_phase);
            for(int ch=0;ch<_channels && i+ch<offset+read;ch++)
            {
                var dry=float.IsFinite(buffer[i+ch])?buffer[i+ch]:0f;
                double delayed,feedback,scale;
                if(_mode=="Echo") { delayed=Tap(320,ch); feedback=.4; scale=.6; }
                else if(_mode=="Reverb") { delayed=(Tap(29.7,ch)+Tap(37.1,ch)+Tap(41.1,ch)+Tap(43.7,ch))/4; feedback=.68; scale=.32; }
                else { delayed=Tap(flangeMs,ch); feedback=.25; scale=.75; }
                _delay[_position*_channels+ch]=(float)Math.Clamp(dry+feedback*delayed,-4,4);
                var mixed=dry*(1-_wet)+delayed*scale*_wet;
                buffer[i+ch]=_wet==0 && desired==0?dry:(float)Math.Clamp(mixed,-1,1);
            }
            _position=(_position+1)%_frames;
            _phase+=2*Math.PI*.25/_rate; if(_phase>=2*Math.PI)_phase-=2*Math.PI;
            if(_wet==0 && desired==0) { Clear(); break; }
        }
        return read;
    }
}
