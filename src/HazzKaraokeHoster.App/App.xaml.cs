using System.Windows;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

public partial class App : System.Windows.Application
{
    private static readonly object LogGate = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hazz Karaoke Hoster LibVLC Test", "Logs");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _ = BrokenMediaRegistry.InitializeAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hazz Karaoke Hoster LibVLC Test", "broken-media.json"));
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        CleanOldDiagnostics();
        WriteDiagnostic("START", "Hazz Karaoke Hoster started.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        WriteDiagnostic("EXIT", $"Hazz Karaoke Hoster exited with code {e.ApplicationExitCode}.");
        base.OnExit(e);
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteDiagnostic("UI ERROR", e.Exception.ToString());
        if (e.Exception is OutOfMemoryException)
            return; // Do not try to continue after a process-wide memory failure.

        try
        {
            MessageBox.Show(
                "Hazz caught a user-interface error and kept the show running where possible.\n\n" +
                "The details were written to the Hazz Karaoke Hoster Logs folder.\n\n" + e.Exception.Message,
                "Hazz Karaoke Hoster LibVLC Test",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            e.Handled = true;
        }
        catch
        {
            // If even the error dialog cannot be shown, allow normal WPF failure handling.
        }
    }

    private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        => WriteDiagnostic("FATAL", e.ExceptionObject?.ToString() ?? "Unknown fatal exception");

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteDiagnostic("BACKGROUND ERROR", e.Exception.ToString());
        e.SetObserved();
    }

    internal static void WriteDiagnostic(string category, string message)
    {
        try
        {
            lock (LogGate)
            {
                Directory.CreateDirectory(LogDirectory);
                var logPath = Path.Combine(LogDirectory, $"hazz-{DateTime.Now:yyyy-MM-dd}.log");
                if (File.Exists(logPath) && new FileInfo(logPath).Length > 8 * 1024 * 1024)
                    File.Move(logPath, logPath + ".previous", true);
                File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never interfere with playback or shutdown.
        }
    }

    private static void CleanOldDiagnostics()
    {
        try
        {
            if (!Directory.Exists(LogDirectory)) return;
            var cutoff = DateTime.UtcNow - TimeSpan.FromDays(21);
            foreach (var path in Directory.EnumerateFiles(LogDirectory, "hazz-*.log*"))
                if (File.GetLastWriteTimeUtc(path) < cutoff) File.Delete(path);
        }
        catch
        {
            // Log maintenance must never delay or prevent startup.
        }
    }
}
