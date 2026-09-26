namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        InitializeV18Improvements();
    }
}
