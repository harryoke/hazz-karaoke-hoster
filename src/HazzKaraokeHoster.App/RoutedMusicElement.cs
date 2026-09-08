using System.Windows.Controls;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

// Video and timing stay with WPF; explicitly routed audio uses a stable Windows endpoint ID.
public class RoutedMusicElement : MediaElement
{
    public RoutedMusicElement() { LoadedBehavior = MediaState.Manual; UnloadedBehavior = MediaState.Manual; }
    public string? OutputDeviceId { get; set; }
    private MediaFoundationReader? _reader;
    private WasapiOut? _output;
    private MMDevice? _device;
    private VolumeSampleProvider? _gain;
    private Uri? _audioSource;
    private bool _muted;
    private double _volume = 0.5;
    public event Action<Exception>? RoutingFailed;
    public new double Volume { get => _volume; set { _volume = value; base.Volume = value; UpdateGain(); } }
    public new bool IsMuted { get => _muted; set { _muted = value; base.IsMuted = _output is not null || value; UpdateGain(); } }
    public new TimeSpan Position { get => base.Position; set { base.Position = value; if (_reader is not null) _reader.CurrentTime = value; } }
    private void UpdateGain() { if (_gain is not null) _gain.Volume = _muted ? 0 : (float)Math.Clamp(_volume, 0, 1); }
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
                _gain = new VolumeSampleProvider(new HazzKaraokeHoster.Playback.NormalizingSampleProvider(_reader.ToSampleProvider()));
                _output = new WasapiOut(_device, AudioClientShareMode.Shared, true, 120);
                _output.Init(_gain);
                var captured = _output;
                _output.PlaybackStopped += (_, e) => { if (e.Exception is not null) Dispatcher.BeginInvoke(() => { if (ReferenceEquals(captured, _output)) { base.Pause(); RoutingFailed?.Invoke(e.Exception); } }); };
                _audioSource = Source;
            }
            UpdateGain();
            base.IsMuted = true;
            _reader!.CurrentTime = base.Position;
            _output!.Play();
            base.Play();
        }
        catch (Exception ex) { base.Pause(); CloseAudio(); base.IsMuted = true; RoutingFailed?.Invoke(ex); }
    }
    public new void Pause() { _output?.Pause(); base.Pause(); }
    public new void Stop() { _output?.Stop(); base.Stop(); if (_reader is not null) _reader.CurrentTime = TimeSpan.Zero; }
    public new void Close() { CloseAudio(); base.Close(); }
    private void CloseAudio()
    {
        var output = _output; _output = null;
        output?.Stop(); output?.Dispose(); _reader?.Dispose(); _reader = null;
        _device?.Dispose(); _device = null; _gain = null; _audioSource = null;
    }
}
