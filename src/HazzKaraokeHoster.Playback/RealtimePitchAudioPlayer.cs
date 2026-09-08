using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace HazzKaraokeHoster.Playback;

/// <summary>
/// Karaoke-only audio path providing live semitone pitch changes without changing tempo.
/// Windows Media Foundation is used to decode the source, so installed media support is
/// retained where MediaFoundationReader can expose an audio stream.
/// </summary>
public sealed class RealtimePitchAudioPlayer : IDisposable
{
    private MediaFoundationReader? _reader;
    private IWavePlayer? _output;
    public string? OutputDeviceId { get; set; }
    private NAudio.CoreAudioApi.MMDevice? _device;
    private SmbPitchShiftingSampleProvider? _pitch;
    private VolumeSampleProvider? _volume;
    private int _semitones;

    public bool IsLoaded => _reader is not null && _output is not null && _pitch is not null;
    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;
    public string? LastLoadError { get; private set; }
    public int Semitones => _semitones;
    public TimeSpan TotalTime => _reader?.TotalTime ?? TimeSpan.Zero;

    public TimeSpan Position
    {
        get => _reader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_reader is null) return;
            var target = value < TimeSpan.Zero ? TimeSpan.Zero : value;
            if (_reader.TotalTime > TimeSpan.Zero && target > _reader.TotalTime) target = _reader.TotalTime;
            var resume = _output?.PlaybackState == PlaybackState.Playing;
            if (resume) _output?.Pause();
            _reader.CurrentTime = target;
            if (resume) _output?.Play();
        }
    }

    public bool TryLoad(string path, out string? error)
    {
        DisposePipeline();
        LastLoadError = null;
        error = null;
        try
        {
            _reader = new MediaFoundationReader(path);
            var source = _reader.ToSampleProvider();
            _pitch = new SmbPitchShiftingSampleProvider(source);
            _volume = new VolumeSampleProvider(new NormalizingSampleProvider(_pitch)) { Volume = 0.9f };
            if (string.IsNullOrWhiteSpace(OutputDeviceId)) _output = new WaveOutEvent { DesiredLatency = 120, NumberOfBuffers = 3 };
            else
            {
                using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                _device = enumerator.GetDevice(OutputDeviceId);
                if (_device.State != NAudio.CoreAudioApi.DeviceState.Active) throw new InvalidOperationException("Selected karaoke output is disconnected");
                _output = new WasapiOut(_device, NAudio.CoreAudioApi.AudioClientShareMode.Shared, true, 120);
            }
            _output.Init(_volume);
            SetSemitones(0);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            LastLoadError = ex.Message;
            DisposePipeline();
            return false;
        }
    }

    public void SetSemitones(int semitones)
    {
        _semitones = Math.Clamp(semitones, -6, 6);
        if (_pitch is not null) _pitch.PitchFactor = (float)Math.Pow(2.0, _semitones / 12.0);
    }

    public void SetVolume(double volume)
    {
        if (_volume is not null) _volume.Volume = (float)Math.Clamp(volume, 0.0, 1.0);
    }

    public void Play(TimeSpan? position = null)
    {
        if (!IsLoaded) return;
        if (position is TimeSpan pos) Position = pos;
        _output!.Play();
    }

    public void Pause() => _output?.Pause();

    public void Stop()
    {
        if (_output is null || _reader is null) return;
        _output.Stop();
        _reader.CurrentTime = TimeSpan.Zero;
    }

    public void SyncTo(TimeSpan hostPosition, double toleranceMilliseconds = 450)
    {
        if (!IsLoaded) return;
        if (Math.Abs((Position - hostPosition).TotalMilliseconds) <= toleranceMilliseconds) return;
        Position = hostPosition;
    }

    public void Dispose()
    {
        DisposePipeline();
        GC.SuppressFinalize(this);
    }

    private void DisposePipeline()
    {
        try { _output?.Stop(); } catch { }
        _output?.Dispose();
        _output = null; _device?.Dispose(); _device = null;
        _reader?.Dispose();
        _reader = null;
        _pitch = null;
        _volume = null;
    }
}
