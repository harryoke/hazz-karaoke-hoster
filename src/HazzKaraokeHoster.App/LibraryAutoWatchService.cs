using System.Collections.Concurrent;
using HazzKaraokeHoster.Core.Interfaces;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

/// <summary>
/// Watches configured library roots for newly copied/renamed files. FileSystemWatcher is used as
/// a live convenience layer; the normal Rescan Watched Folders command remains the recovery path
/// if Windows reports a watcher buffer overflow or files were added while Hazz was not running.
/// </summary>
public sealed class LibraryAutoWatchService : IDisposable
{
    private sealed record PendingFile(string Path, LibraryImportMode Mode, DateTime QueuedUtc);

    private readonly ILibraryImportService _importer;
    private readonly CancellationToken _lifetimeToken;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, PendingFile> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _pumpGate = new(1, 1);
    private Timer? _timer;
    private bool _disposed;
    private long _lastBacklogWarningTicks;
    private const int MaximumPendingFiles = 10_000;

    public event EventHandler<string>? TrackIndexed;
    public event EventHandler<string>? Warning;

    public LibraryAutoWatchService(ILibraryImportService importer, CancellationToken lifetimeToken)
    {
        _importer = importer;
        _lifetimeToken = lifetimeToken;
    }

    public void Start(IEnumerable<LibraryRootRecord> roots)
    {
        StopWatchers();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root.Path)) continue;
            try
            {
                var mode = string.Equals(root.MediaKind, "Music", StringComparison.OrdinalIgnoreCase)
                    ? LibraryImportMode.Music
                    : string.Equals(root.MediaKind, "Auto", StringComparison.OrdinalIgnoreCase)
                        ? LibraryImportMode.Auto : LibraryImportMode.Karaoke;
                var watcher = new FileSystemWatcher(root.Path)
                {
                    IncludeSubdirectories = root.IncludeSubfolders,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size,
                    InternalBufferSize = 64 * 1024,
                    EnableRaisingEvents = true
                };
                watcher.Created += (_, e) => Queue(e.FullPath, mode);
                watcher.Renamed += (_, e) => Queue(e.FullPath, mode);
                watcher.Changed += (_, e) => Queue(e.FullPath, mode);
                watcher.Error += (_, e) => Warning?.Invoke(this,
                    $"Library watcher warning for {root.Path}: {e.GetException()?.Message ?? "Windows reported a watcher error"}. Use LIBRARY > Rescan Watched Folders to catch up.");
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                Warning?.Invoke(this, $"Could not watch {root.Path}: {ex.Message}");
            }
        }

        _timer ??= new Timer(_ => _ = PumpAsync(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private void Queue(string path, LibraryImportMode mode)
    {
        if (_disposed || string.IsNullOrWhiteSpace(path) || !Path.HasExtension(path)) return;
        if (_pending.Count >= MaximumPendingFiles && !_pending.ContainsKey(path))
        {
            var now = DateTime.UtcNow.Ticks;
            var last = Interlocked.Read(ref _lastBacklogWarningTicks);
            if (now - last >= TimeSpan.FromMinutes(1).Ticks &&
                Interlocked.CompareExchange(ref _lastBacklogWarningTicks, now, last) == last)
            {
                Warning?.Invoke(this,
                    "Library watcher backlog reached its safe limit. New events are paused to protect live playback; use LIBRARY > Rescan Watched Folders after the file copy finishes.");
            }
            return;
        }
        _pending[path] = new PendingFile(path, mode, DateTime.UtcNow);
    }

    private async Task PumpAsync()
    {
        if (_disposed || _lifetimeToken.IsCancellationRequested || !await _pumpGate.WaitAsync(0)) return;
        try
        {
            var cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(1.5);
            var ready = _pending.Values.Where(x => x.QueuedUtc <= cutoff).Take(64).ToArray();
            foreach (var pending in ready)
            {
                if (!_pending.TryRemove(pending.Path, out _)) continue;
                try
                {
                    var result = await _importer.IndexFileAsync(pending.Path, pending.Mode, _lifetimeToken);
                    if (result.Outcome == SingleFileIndexOutcome.Indexed)
                        TrackIndexed?.Invoke(this, pending.Path);
                    else if (result.Outcome == SingleFileIndexOutcome.Error)
                        Warning?.Invoke(this, $"Could not auto-index {pending.Path}: {result.Message}");
                }
                catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    Warning?.Invoke(this, $"Could not auto-index {pending.Path}: {ex.Message}");
                }
            }
        }
        catch (ObjectDisposedException) when (_disposed) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            App.WriteDiagnostic("LIBRARY WATCHER", ex.ToString());
        }
        finally
        {
            if (!_disposed) _pumpGate.Release();
        }
    }

    private void StopWatchers()
    {
        foreach (var watcher in _watchers)
        {
            try { watcher.EnableRaisingEvents = false; watcher.Dispose(); } catch { }
        }
        _watchers.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatchers();
        _timer?.Dispose();
        _timer = null;
        // A timer callback may still be unwinding. Leaving this tiny semaphore for the GC
        // avoids a dispose/release race during shutdown.
    }
}
