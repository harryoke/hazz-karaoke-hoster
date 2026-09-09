using System.ComponentModel; using System.Windows; namespace ShutdownChecks; partial class MainWindow {
    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_venueCloseReady) return;
        e.Cancel = true;
        if (_venueClosing || _venueDialogOpen) return;
        var result = TestMessageBox.Show(
            "Close Hazz Karaoke Hoster?\n\nAny karaoke or music playback will stop.",
            "Confirm Hazz Shutdown",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;
        _venueClosing = true;
        IsEnabled = false;
        // A save can complete synchronously (especially with no active venue).
        // Always leave the original Closing event before closing or showing an error.
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        try
        {
            await SaveActiveVenueAsync();
            _venueCloseReady = true;
            Close();
        }
        catch (Exception ex)
        {
            _venueCloseReady = false;
            App.WriteDiagnostic("VENUE SHUTDOWN SAVE", ex.ToString());
            TestMessageBox.Show(this, "Venue saving failed. Hazz has stayed open so you can retry. Your current singers remain in the main database.\n\n" + ex.Message, "Venue Save Failed");
        }
        finally { _venueClosing = false; IsEnabled = true; }
    }


}
