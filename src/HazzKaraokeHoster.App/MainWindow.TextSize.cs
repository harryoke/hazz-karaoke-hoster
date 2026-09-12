using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private double _hostTextScale = 1;

    private void ApplyHostTextScale(double scale)
    {
        _hostTextScale = double.IsFinite(scale) ? Math.Clamp(scale, 1, 1.5) : 1;
        // Change shared resources once, including templates for rows that have
        // not been created yet. Do not walk or populate virtualized playlists.
        foreach (var key in Resources.Keys.OfType<string>().Where(k => k.StartsWith("HostFont", StringComparison.Ordinal)).ToArray())
            if (double.TryParse(key[8..].Replace('_', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
                Resources[key] = size * _hostTextScale;
        Resources["HostRowHeight"] = 32 * _hostTextScale;
        if (HostTextSizeMenu != null)
            foreach (var item in HostTextSizeMenu.Items.OfType<MenuItem>())
                item.IsChecked = double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && Math.Abs(value - _hostTextScale) < 0.001;
    }

    private void HostTextSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
        {
            ApplyHostTextScale(scale);
            SaveMainLayout();
        }
    }
}
