using System.Windows;

namespace HazzKaraokeHoster.App;

public partial class AudienceWindow
{
    private Size _previewSize = new(1280, 720);
    public FrameworkElement PreviewScene => Root;
    public FrameworkElement PreviewOverlays => AudienceOverlays;
    public (AudienceVideoSurface? Surface, bool Playing) PreviewVideo => _karaokeActive
        ? (AudienceMedia.Source is null ? null : AudienceMedia, _videoPlaying && !_videoWaiting)
        : (MusicVideoMedia.Source is null ? null : MusicVideoMedia, _musicVideoPlaying);
    public void SetPreviewRunning(bool running)
    {
        if (running)
        {
            if (!IsVisible)
            {
                var size = _previewSize;
                Root.Width = size.Width; Root.Height = size.Height;
                Root.Measure(size); Root.Arrange(new Rect(size)); Root.UpdateLayout();
                LayoutScrollers(); FitSingerPanel();
            }
            else if (Root.ActualWidth > 0 && Root.ActualHeight > 0) _previewSize = new(Root.ActualWidth, Root.ActualHeight);
            _ticker.Start();
        }
        else if (!IsVisible) _ticker.Stop();
    }
}
