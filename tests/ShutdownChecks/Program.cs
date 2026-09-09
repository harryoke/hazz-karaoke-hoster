using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
namespace ShutdownChecks;
static class App { public static void WriteDiagnostic(string a,string b) { } }
static class TestMessageBox
{
    public static int Errors;
    public static MessageBoxResult Show(string a,string b,MessageBoxButton c,MessageBoxImage d,MessageBoxResult e) => MessageBoxResult.Yes;
    public static void Show(Window w,string a,string b) { Errors++; }
}
partial class MainWindow : Window
{
    bool _venueCloseReady, _venueClosing, _venueDialogOpen;
    public bool Fail;
    public bool Delay;
    public TaskCompletionSource Done = new();
    Task SaveActiveVenueAsync() => Fail ? Task.FromException(new Exception("test failure")) : Delay ? Task.Delay(25) : Task.CompletedTask;
    public MainWindow() { Opacity=0; ShowInTaskbar=false; ShowActivated=false; Width=100; Height=100; Closing+=MainWindow_Closing; Closed+=(_,_)=>Done.SetResult(); }
}
static class Program
{
    [STAThread] static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                foreach(var delay in new[]{false,true})
                {
                    var w=new MainWindow { Delay=delay }; w.Show(); w.Close();
                    await w.Done.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    if(TestMessageBox.Errors!=0) throw new Exception("Unexpected save error");
                    Console.WriteLine("PASS close with " + (delay?"delayed":"immediate") + " save");
                }
                var retry=new MainWindow { Fail=true }; retry.Show(); retry.Close(); await Task.Delay(100);
                if(retry.Done.Task.IsCompleted || TestMessageBox.Errors!=1) throw new Exception("Failure did not stay open");
                retry.Fail=false; retry.Close(); await retry.Done.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Console.WriteLine("PASS failed save stays open and retry closes");
            }
            catch(Exception ex) { Console.WriteLine(ex); Environment.ExitCode=1; }
            finally { app.Shutdown(); }
        }));
        app.Run();
    }
}
