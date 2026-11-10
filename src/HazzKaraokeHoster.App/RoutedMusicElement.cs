using System.Windows.Controls;
using HazzKaraokeHoster.Playback;
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
        MediaEnded += (_, _) => { ResetStems(); Tempo = 1; _output?.Stop(); Effects.Enabled = false; _effectsAudio?.Reset(); };
    }
    public MusicEffectsState Effects { get; set; } = new();
    private MusicEffectsSampleProvider? _effectsAudio;
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
    private LiveStemSampleProvider? _liveStems;
    private CancellationTokenSource? _stemJob;
    private string? _stemError;
    internal int StemMuted { get; private set; }
    internal int StemSolo { get; private set; }
    internal bool CanUseStems => _liveStems is not null && _audioSource == Source &&
        _output?.PlaybackState is PlaybackState.Playing or PlaybackState.Paused;
    internal string StemStatus => !CanUseStems ? "Start an audio track on this deck"
        : _stemError ?? (_liveStems!.Mask == 15 ? "Original mix"
        : _liveStems.Mask == 0 ? "All stems muted"
        : _liveStems.Covered ? "Live stems active" : "Separating near playhead • original until ready");

    internal void SetStemMix(int muted, int solo)
    {
        if (!CanUseStems || _liveStems is not { } sink) return;
        StemMuted = muted & 15;
        StemSolo = solo & 15;
        sink.Mask = StemSolo != 0 ? StemSolo : 15 & ~StemMuted;
        if (sink.Mask is 0 or 15) { _stemError = null; return; }
        _stemError = null;
        WarmStems();
    }

    internal void WarmStems()
    {
        if (!CanUseStems || _liveStems is not { } sink || _stemJob is not null || _stemError is not null) return;
        if (_output?.PlaybackState != PlaybackState.Playing) return;
        var path = _audioSource!.LocalPath;
        var job = _stemJob = new CancellationTokenSource();
        var token = job.Token;
        _ = Task.Run(async () =>
        {
            string? error = null;
            try { await LiveStemWorker.RunAsync(path, sink, token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                error = "Stems unavailable • playing original";
                App.WriteDiagnostic("LIVE STEMS", ex.ToString());
            }
            try
            {
                if (!Dispatcher.HasShutdownStarted)
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (ReferenceEquals(_stemJob, job))
                        {
                            _stemJob = null;
                            _stemError = error;
                        }
                    });
            }
            catch (OperationCanceledException) when (Dispatcher.HasShutdownStarted) { }
            finally { job.Dispose(); }
        });
    }

    private void ResetStems()
    {
        var job = _stemJob;
        _stemJob = null;
        job?.Cancel();
        if (_liveStems is not null) _liveStems.Mask = 15;
        StemMuted = StemSolo = 0;
        _stemError = null;
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
    public new TimeSpan Position { get => base.Position; set { base.Position = value; if (_reader is not null) _tempoAudio?.Seek(() => _reader.CurrentTime = value); _effectsAudio?.Reset(); } }
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
                ISampleProvider samples = _reader.ToSampleProvider();
                if (MediaFileClassifier.Classify(Source.LocalPath) is HazzMediaKind.Audio or HazzMediaKind.Mp3Audio &&
                    samples.WaveFormat.Channels is 1 or 2)
                    samples = _liveStems = new LiveStemSampleProvider(_reader, samples);
                _tempoAudio = new HazzKaraokeHoster.Playback.TempoSampleProvider(samples);
                _tempoAudio.Configure(_tempo);
                _effectsAudio = new MusicEffectsSampleProvider(new NormalizingSampleProvider(_tempoAudio), () => Effects.Options);
                _gain = new VolumeSampleProvider(_effectsAudio);
                _output = new WasapiOut(_device, AudioClientShareMode.Shared, true, PlaybackBufferSettings.LatencyMilliseconds);
                _output.Init(_gain);
                var captured = _output;
                _output.PlaybackStopped += (_, e) => { if (e.Exception is not null) Dispatcher.BeginInvoke(() => { if (ReferenceEquals(captured, _output)) { base.Pause(); RoutingFailed?.Invoke(e.Exception); } }); };
                _audioSource = Source;
            }
            UpdateGain();
            base.IsMuted = true;
            var position = base.Position;
            _tempoAudio!.Seek(() => _reader!.CurrentTime = position);
            _effectsAudio?.Reset();
            SpeedRatio = _tempo;
            _output!.Play();
            if (_liveStems?.Mask is > 0 and < 15) WarmStems();
            base.Play();
        }
        catch (Exception ex) { base.Pause(); CloseAudio(); base.IsMuted = true; RoutingFailed?.Invoke(ex); }
    }
    public new void Pause() { _output?.Pause(); base.Pause(); }
    public new void Stop() { ResetStems(); Effects.Enabled = false; _effectsAudio?.Reset(); _output?.Stop(); base.Stop(); if (_reader is not null) _tempoAudio?.Seek(() => _reader.CurrentTime = TimeSpan.Zero); Tempo = 1; }
    public new void Close() { Effects.Enabled = false; CloseAudio(); base.Close(); }
    private void CloseAudio()
    {
        ResetStems();
        _liveStems = null;
        var output = _output; _output = null;
        output?.Stop(); output?.Dispose(); _reader?.Dispose(); _reader = null;
        _device?.Dispose(); _device = null; _gain = null; _audioSource = null; _tempoAudio = null; _effectsAudio = null;
    }
}
