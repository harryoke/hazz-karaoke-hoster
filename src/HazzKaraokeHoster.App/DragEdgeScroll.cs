using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HazzKaraokeHoster.App;

// Shared by drop targets; moving to another list transfers the single timer.
internal static class DragEdgeScroll
{
    private static readonly DispatcherTimer Timer = new(DispatcherPriority.Background)
        { Interval = TimeSpan.FromMilliseconds(75) };
    private static ItemsControl? _target;
    private static ScrollViewer? _viewer;
    private static Window? _window;
    private static int _direction;

    static DragEdgeScroll() => Timer.Tick += (_, _) =>
    {
        // OLE can finish outside the host without routing Drop back to us.
        if (_target is not { IsVisible: true, IsLoaded: true } ||
            (GetAsyncKeyState(1) & 0x8000) == 0 && (GetAsyncKeyState(2) & 0x8000) == 0)
        {
            Stop();
            return;
        }
        if (_viewer is not null) ScrollOnce(_viewer, _direction);
    };

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    internal static void Update(ItemsControl target, DragEventArgs e)
    {
        if ((e.Effects & (DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link)) == 0)
        {
            Stop();
            return;
        }
        var viewer = FindViewer(target);
        if (viewer is null) { Stop(); return; }
        var point = e.GetPosition(viewer);
        var direction = Direction(point.Y, viewer.ActualHeight);
        if (point.X < 0 || point.X > viewer.ActualWidth || direction == 0)
        {
            Stop();
            return;
        }
        if (!ReferenceEquals(_target, target))
        {
            Stop();
            _target = target;
            _viewer = viewer;
            _window = Window.GetWindow(target);
            target.AddHandler(DragDrop.PreviewDropEvent, new DragEventHandler(OnDrop), true);
            target.AddHandler(DragDrop.DragLeaveEvent, new DragEventHandler(OnLeave), true);
            target.Unloaded += OnUnloaded;
            _window?.AddHandler(DragDrop.QueryContinueDragEvent, new QueryContinueDragEventHandler(OnContinue), true);
        }
        _direction = direction;
        Timer.Start();
    }

    internal static int Direction(double y, double height)
    {
        if (!double.IsFinite(y) || !double.IsFinite(height) || height <= 0 || y < 0 || y > height) return 0;
        var edge = Math.Min(48, height / 4);
        return y < edge ? -1 : y > height - edge ? 1 : 0;
    }

    internal static void ScrollOnce(ScrollViewer viewer, int direction)
    {
        // Line scrolling respects both pixel and virtualized item scrolling.
        if (direction < 0 && viewer.VerticalOffset > 0) viewer.LineUp();
        else if (direction > 0 && viewer.VerticalOffset < viewer.ScrollableHeight) viewer.LineDown();
    }

    internal static ScrollViewer? FindViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer) return viewer;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindViewer(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }

    private static void OnDrop(object sender, DragEventArgs e) => Stop();
    private static void OnUnloaded(object sender, RoutedEventArgs e) => Stop();
    private static void OnContinue(object sender, QueryContinueDragEventArgs e)
    {
        if (e.EscapePressed || e.Action != DragAction.Continue) Stop();
    }
    private static void OnLeave(object sender, DragEventArgs e)
    {
        if (_target is null) return;
        var point = e.GetPosition(_target);
        // Ignore transitions between rows inside the same target.
        if (point.X < 0 || point.Y < 0 || point.X >= _target.ActualWidth || point.Y >= _target.ActualHeight) Stop();
    }

    internal static void Stop()
    {
        Timer.Stop();
        if (_target is not null)
        {
            _target.RemoveHandler(DragDrop.PreviewDropEvent, new DragEventHandler(OnDrop));
            _target.RemoveHandler(DragDrop.DragLeaveEvent, new DragEventHandler(OnLeave));
            _target.Unloaded -= OnUnloaded;
        }
        _window?.RemoveHandler(DragDrop.QueryContinueDragEvent, new QueryContinueDragEventHandler(OnContinue));
        _target = null;
        _viewer = null;
        _window = null;
        _direction = 0;
    }
}
