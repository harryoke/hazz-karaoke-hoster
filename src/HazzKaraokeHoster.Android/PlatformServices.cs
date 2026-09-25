using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.Android;

internal interface IAndroidMediaPlaybackService
{
    bool IsReady { get; }
    string Status { get; }
    Task PrepareAsync(SongRecord song, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

internal interface IAndroidCdgRenderer
{
    bool IsReady { get; }
    string Status { get; }
}

internal interface IAndroidStorageService
{
    string Status { get; }
}

internal interface IAndroidSecondaryDisplayService
{
    bool HasExternalDisplay { get; }
    string Status { get; }
}

/// <summary>
/// v0.1 platform boundary. The host UI and shared database/rotation/search code do not
/// depend directly on a particular Android media/CD+G/HDMI implementation.
/// </summary>
internal sealed class AndroidPlatformServices :
    IAndroidMediaPlaybackService,
    IAndroidCdgRenderer,
    IAndroidStorageService,
    IAndroidSecondaryDisplayService
{
    public bool IsReady => false;
    public bool HasExternalDisplay => false;

    string IAndroidMediaPlaybackService.Status => "Android media engine: next milestone";
    string IAndroidCdgRenderer.Status => "CD+G renderer: next milestone";
    string IAndroidStorageService.Status => "Storage Access Framework: database import enabled in v0.1";
    string IAndroidSecondaryDisplayService.Status => "HDMI/secondary display: next milestone";

    public Task PrepareAsync(SongRecord song, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException("Playback is not enabled in Android v0.1 yet."));

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
