namespace HazzKaraokeHoster.App;

// A surface owns one queue. Native calls never overlap or run on the WPF dispatcher.
// Cleanup follows outstanding calls; cancellation must not dispose a decoder in use.
internal sealed class SerialMediaCommands
{
    private readonly object _gate = new();
    private Task _tail = Task.CompletedTask;
    public void Enqueue(Action action) => Enqueue(() => { action(); return Task.CompletedTask; });
    public void Enqueue(Func<Task> action)
    {
        lock (_gate)
        {
            var previous = _tail;
            _tail = Task.Run(async () =>
            {
                await previous.ConfigureAwait(false);
                try { await action().ConfigureAwait(false); }
                catch (Exception ex) { App.WriteDiagnostic("VIDEO WORKER", ex.ToString()); }
            });
        }
    }
    internal Task DrainAsync() { lock (_gate) return _tail; }
}
