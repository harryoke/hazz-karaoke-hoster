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
    private TempoSampleProvider? _pitch;
    private double _tempo = 1;
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
            _drift.Reset();
            if (_reader is null) return;
            var target = value < TimeSpan.Zero ? TimeSpan.Zero : value;
            if (_reader.TotalTime > TimeSpan.Zero && target > _reader.TotalTime) target = _reader.TotalTime;
            // Avoid reopening/seeking a freshly prepared video stream at 00:00.
            // Some Media Foundation video decoders make even a redundant seek slow.
            if (Math.Abs((_reader.CurrentTime - target).TotalMilliseconds) < 20) return;
            var resume = _output?.PlaybackState == PlaybackState.Playing;
            if (resume) _output?.Pause();
            _pitch?.Seek(() => _reader.CurrentTime = target);
            if (resume) _output?.Play();
        }
    }

    // Prepare an isolated pipeline. It is only handed to the deck after completion,
    // so background decoding never races UI controls on the current song.
    public static Task<RealtimePitchAudioPlayer> PrepareAsync(string path, string? outputDeviceId,
        CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            var player = new RealtimePitchAudioPlayer { OutputDeviceId = outputDeviceId };
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                player.TryLoad(path, out _);
                cancellationToken.ThrowIfCancellationRequested();
                return player;
            }
            catch { player.Dispose(); throw; }
        }, cancellationToken);

    public bool TryLoad(string path, out string? error)
    {
        DisposePipeline();
        LastLoadError = null;
        error = null;
        try
        {
            _reader = new MediaFoundationReader(path);
            var source = _reader.ToSampleProvider();
            _pitch = new TempoSampleProvider(source);
            _volume = new VolumeSampleProvider(new NormalizingSampleProvider(_pitch)) { Volume = 0.9f };
            if (string.IsNullOrWhiteSpace(OutputDeviceId)) _output = new WaveOutEvent { DesiredLatency = PlaybackBufferSettings.LatencyMilliseconds, NumberOfBuffers = 4 };
            else
            {
                using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                _device = enumerator.GetDevice(OutputDeviceId);
                if (_device.State != NAudio.CoreAudioApi.DeviceState.Active) throw new InvalidOperationException("Selected karaoke output is disconnected");
                _output = new WasapiOut(_device, NAudio.CoreAudioApi.AudioClientShareMode.Shared, true, PlaybackBufferSettings.LatencyMilliseconds);
            }
            _output.Init(_volume);
            _tempo = 1;
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
        _pitch?.Configure(_tempo, _semitones);
    }

    public void SetTempo(double tempo)
    {
        _tempo = double.IsFinite(tempo) ? Math.Clamp(tempo, 0.75, 1.25) : 1;
        _pitch?.Configure(_tempo, _semitones);
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

    public void Pause() { _drift.Reset(); _output?.Pause(); }

    public void Stop()
    {
        if (_output is null || _reader is null) return;
        _drift.Reset();
        _output.Stop();
        _pitch?.Seek(() => _reader.CurrentTime = TimeSpan.Zero);
    }

    private readonly KaraokeDriftFilter _drift = new();

    public void SyncTo(TimeSpan hostPosition)
    {
        if (!IsPlaying || hostPosition < TimeSpan.FromSeconds(2))
        {
            _drift.Reset();
            return;
        }

        if (_drift.NeedsCorrection((Position - hostPosition).TotalMilliseconds))
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
