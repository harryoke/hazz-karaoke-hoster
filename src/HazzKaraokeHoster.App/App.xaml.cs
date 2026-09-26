using System.Windows;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

public partial class App : System.Windows.Application
{
    private static readonly object LogGate = new();
    private bool _packageCheck;
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hazz Karaoke Hoster", "Logs");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Apply before StartupUri creates any WPF windows. This is opt-in and
        // affects this process only; it does not change Windows or saved preferences.
        var softwareRendering = e.Args.Any(arg => string.Equals(arg, "--software-rendering", StringComparison.OrdinalIgnoreCase));
        if (softwareRendering)
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var startupArgs = e.Args.Where(arg => !string.Equals(arg, "--software-rendering", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (startupArgs.Length == 2 && startupArgs[0] == "--package-check")
        {
            _packageCheck = true;
            var code = 0;
            try
            {
                var vlcPath = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
                if (!Directory.Exists(Path.Combine(vlcPath, "plugins")))
                    throw new DirectoryNotFoundException("Bundled VLC plugins missing: " + vlcPath);
                LibVLCSharp.Shared.Core.Initialize(vlcPath);
                using var vlc = new LibVLCSharp.Shared.LibVLC("--no-audio", "--no-video-title-show");
                using var player = new LibVLCSharp.Shared.MediaPlayer(vlc);
                using var database = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
                database.Open();
                using var command = database.CreateCommand();
                command.CommandText = "SELECT sqlite_version()";
                File.WriteAllText(startupArgs[1], "PASS: bundled VLC runtime/plugins and SQLite loaded.\n" +
                    "WPF rendering: " + System.Windows.Media.RenderOptions.ProcessRenderMode + "\nSQLite: " + command.ExecuteScalar() + "\nRuntime directory: " + AppContext.BaseDirectory);
            }
            catch (Exception ex) { code = 1; File.WriteAllText(startupArgs[1], ex.ToString()); }
            // Exit before WPF processes its queued StartupUri navigation. This diagnostic mode must never construct the live host.
            Environment.Exit(code);
            return;
        }

        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is MainWindow window) window.InitializeAudienceEnhancementsTest();
            }));

        base.OnStartup(e);
        _ = BrokenMediaRegistry.InitializeAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hazz Karaoke Hoster", "broken-media.json"));
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        CleanOldDiagnostics();
        WriteDiagnostic("START", "Hazz Karaoke Hoster started. WPF rendering: " +
            System.Windows.Media.RenderOptions.ProcessRenderMode + "; rendering tier: " +
            (System.Windows.Media.RenderCapability.Tier >> 16) + "; OS: " + Environment.OSVersion);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_packageCheck) WriteDiagnostic("EXIT", $"Hazz Karaoke Hoster exited with code {e.ApplicationExitCode}.");
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
                "Hazz Karaoke Hoster",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            e.Handled = true;
        }
        catch
        {
            // If even the error dialog cannot be shown, allow normal WPF failure handling.
        }
    }

    private static void CurrentDomain_UnhandledException(object? sender, UnhandledEventArgs e)
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
