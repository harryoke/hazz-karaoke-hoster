using System.Windows;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    static MainWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(MainWindow_AudienceEnhancementsLoaded));
    }

    private static void MainWindow_AudienceEnhancementsLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window)
            window.InitializeAudienceEnhancementsTest();
    }
}
