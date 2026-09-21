using System.Windows.Controls;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

// Video and timing stay with WPF; explicitly routed audio uses a stable Windows endpoint ID.
public class RoutedMusicElement : MediaElement
{
    public RoutedMusicElement()
    {
        LoadedBehavior = MediaState.Manual; UnloadedBehavior = MediaState.Manual;
        MediaEnded += (_, _) => { Tempo = 1; _output?.Stop(); };
    }
    public string? OutputDeviceId { get; set; }
    private MediaFoundationReader? _reader;
    private WasapiOut? _output;
    private MMDevice? _device;
    private VolumeSampleProvider? _gain;
    private HazzKaraokeHoster.Playback.TempoSampleProvider? _tempoAudio;
    private double _tempo = 1;
    public double Tempo
    {
        get => _tempo;
        set
        {
            _tempo = double.IsFinite(value) ? Math.Clamp(value, 0.75, 1.25) : 1;
            _tempoAudio?.Configure(_tempo);
            SpeedRatio = _tempo;
        }
    }
    private Uri? _audioSource;
    private bool _muted;
    private double _volume = 0.5;
    private double _attenuation = 1;
    // Independent of deck faders, so temporary FX ducking cannot restore stale volumes.
    public double Attenuation
    {
        get => _attenuation;
        set { _attenuation = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1; base.Volume = _volume * _attenuation; UpdateGain(); }
    }
    public event Action<Exception>? RoutingFailed;
    public new double Volume { get => _volume; set { _volume = value; base.Volume = value * _attenuation; UpdateGain(); } }
    public new bool IsMuted { get => _muted; set { _muted = value; base.IsMuted = _output is not null || value; UpdateGain(); } }
    public new TimeSpan Position { get => base.Position; set { base.Position = value; if (_reader is not null) _tempoAudio?.Seek(() => _reader.CurrentTime = value); } }
    private void UpdateGain() { if (_gain is not null) _gain.Volume = _muted ? 0 : (float)Math.Clamp(_volume * _attenuation, 0, 1); }
    public new void Play()
    {

        try
        {
            if (_output is null || _audioSource != Source)
            {
                CloseAudio();
                using var enumerator = new MMDeviceEnumerator();
                _device = string.IsNullOrWhiteSpace(OutputDeviceId) ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : enumerator.GetDevice(OutputDeviceId);
                if (_device.State != DeviceState.Active) throw new InvalidOperationException("Selected audio device is disconnected");
                _reader = new MediaFoundationReader(Source.LocalPath);
                _tempoAudio = new HazzKaraokeHoster.Playback.TempoSampleProvider(_reader.ToSampleProvider());
                _tempoAudio.Configure(_tempo);
                _gain = new VolumeSampleProvider(new HazzKaraokeHoster.Playback.NormalizingSampleProvider(_tempoAudio));
                _output = new WasapiOut(_device, AudioClientShareMode.Shared, true, 120);
                _output.Init(_gain);
                var captured = _output;
                _output.PlaybackStopped += (_, e) => { if (e.Exception is not null) Dispatcher.BeginInvoke(() => { if (ReferenceEquals(captured, _output)) { base.Pause(); RoutingFailed?.Invoke(e.Exception); } }); };
                _audioSource = Source;
            }
            UpdateGain();
            base.IsMuted = true;
            var position = base.Position;
            _tempoAudio!.Seek(() => _reader!.CurrentTime = position);
            SpeedRatio = _tempo;
            _output!.Play();
            base.Play();
        }
        catch (Exception ex) { base.Pause(); CloseAudio(); base.IsMuted = true; RoutingFailed?.Invoke(ex); }
    }
    public new void Pause() { _output?.Pause(); base.Pause(); }
    public new void Stop() { _output?.Stop(); base.Stop(); if (_reader is not null) _tempoAudio?.Seek(() => _reader.CurrentTime = TimeSpan.Zero); Tempo = 1; }
    public new void Close() { CloseAudio(); base.Close(); }
    private void CloseAudio()
    {
        var output = _output; _output = null;
        output?.Stop(); output?.Dispose(); _reader?.Dispose(); _reader = null;
        _device?.Dispose(); _device = null; _gain = null; _audioSource = null; _tempoAudio = null;
    }
}
