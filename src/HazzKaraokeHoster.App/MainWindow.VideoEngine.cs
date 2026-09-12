using System.Windows;
using System.Windows.Controls;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private bool _useLibVlcAudienceVideo;
    private bool _smoothCdgPicture = true;

    private void ApplyCdgSmoothing(bool smooth)
    {
        _smoothCdgPicture = smooth;
        SmoothCdgMenu.IsChecked = smooth;
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(CdgPreview, smooth
            ? System.Windows.Media.BitmapScalingMode.HighQuality : System.Windows.Media.BitmapScalingMode.NearestNeighbor);
        _audience?.SetCdgSmoothing(smooth);
    }

    private void SmoothCdg_Click(object sender, RoutedEventArgs e)
    {
        ApplyCdgSmoothing(SmoothCdgMenu.IsChecked);
        SaveMainLayout();
    }

    private void ApplyAudienceVideoEngine(bool useVlc)
    {
        _useLibVlcAudienceVideo = useVlc;
        WindowsVideoEngineMenu.IsChecked = !useVlc;
        VlcVideoEngineMenu.IsChecked = useVlc;
        _audience?.SetVideoEnginePreference(useVlc);
    }

    private void AudienceVideoEngine_Click(object sender, RoutedEventArgs e)
    {
        ApplyAudienceVideoEngine((sender as MenuItem)?.Tag?.ToString() == "VLC");
        SaveMainLayout();
    }
}
