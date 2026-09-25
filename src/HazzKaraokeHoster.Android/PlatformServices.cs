using Android.Content;
using Android.Net;

namespace HazzKaraokeHoster.Android;

internal sealed class AndroidMediaPlaybackService : IDisposable
{
    private readonly Context _context;
    private global::Android.Media.MediaPlayer? _player;

    public AndroidMediaPlaybackService(Context context)
    {
        _context = context;
    }

    public bool IsPlaying
    {
        get
        {
            try { return _player?.IsPlaying == true; }
            catch { return false; }
        }
    }

    public string Status => _player is null ? "Stopped" : IsPlaying ? "Playing" : "Paused";

    public event EventHandler? Completed;

    public Task PlayAsync(Uri uri)
    {
        Stop();
        var player = global::Android.Media.MediaPlayer.Create(_context, uri)
            ?? throw new InvalidOperationException("Android could not open this media file.");
        _player = player;
        player.Completion += Player_Completion;
        player.Error += Player_Error;
        player.Start();
        return Task.CompletedTask;
    }

    public void TogglePause()
    {
        var player = _player;
        if (player is null) return;
        if (player.IsPlaying) player.Pause();
        else player.Start();
    }

    public void Stop()
    {
        var player = _player;
        _player = null;
        if (player is null) return;
        try
        {
            player.Completion -= Player_Completion;
            player.Error -= Player_Error;
            if (player.IsPlaying) player.Stop();
        }
        catch { }
        finally
        {
            try { player.Release(); } catch { }
            player.Dispose();
        }
    }

    private void Player_Completion(object? sender, EventArgs e)
    {
        Completed?.Invoke(this, EventArgs.Empty);
    }

    private void Player_Error(object? sender, global::Android.Media.MediaPlayer.ErrorEventArgs e)
    {
        e.Handled = true;
        Completed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();
}

internal interface IAndroidCdgRenderer
{
    bool IsReady { get; }
    string Status { get; }
}

internal interface IAndroidSecondaryDisplayService
{
    bool HasExternalDisplay { get; }
    string Status { get; }
}

internal sealed class AndroidPlatformStatus : IAndroidCdgRenderer, IAndroidSecondaryDisplayService
{
    public bool IsReady => false;
    public bool HasExternalDisplay => false;

    string IAndroidCdgRenderer.Status => "CD+G graphics renderer: next milestone";
    string IAndroidSecondaryDisplayService.Status => "HDMI/secondary display: next milestone";
}
