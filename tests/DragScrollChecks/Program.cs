using System.Windows;
using System.Windows.Controls;
using HazzKaraokeHoster.App;

internal static class Checks
{
    [STAThread] static int Main()
    {
        try
        {
            if (DragEdgeScroll.Direction(5, 250) != -1 || DragEdgeScroll.Direction(245,250) != 1 || DragEdgeScroll.Direction(125,250) != 0 || DragEdgeScroll.Direction(-1,250) != 0 || DragEdgeScroll.Direction(251,250) != 0) throw new Exception("Edge direction failed");
            var app = new Application();
            foreach (ItemsControl list in new ItemsControl[] { new ListBox(), new DataGrid { AutoGenerateColumns = true }, new TreeView() })
            {
                list.Width=400; list.Height=250;
                list.ItemsSource=Enumerable.Range(1,500).Select(i=>new Row("Track or singer "+i)).ToArray();
                var window=new Window { Content=list, Width=420, Height=280 };
                void Layout() { list.ApplyTemplate(); list.Measure(new Size(400,250)); list.Arrange(new Rect(0,0,400,250)); list.UpdateLayout(); }
                Layout();
                var viewer=DragEdgeScroll.FindViewer(list) ?? throw new Exception("No list scroll viewer");
                if(viewer.ScrollableHeight<=0) throw new Exception("Test list did not overflow");
                for(int i=0;i<60;i++) { DragEdgeScroll.ScrollOnce(viewer,1);Layout(); }
                if(viewer.VerticalOffset<=0) throw new Exception("Downward scrolling failed");
                var offset=viewer.VerticalOffset;
                DragEdgeScroll.ScrollOnce(viewer,0);Layout();
                if(viewer.VerticalOffset!=offset) throw new Exception("Middle should not scroll");
                for(int i=0;i<65;i++) { DragEdgeScroll.ScrollOnce(viewer,-1);Layout(); }
                if(viewer.VerticalOffset!=0) throw new Exception("Upward scrolling failed");
                viewer.ScrollToBottom();Layout();
                for(int i=0;i<5;i++) { DragEdgeScroll.ScrollOnce(viewer,1);Layout(); }
                if(viewer.VerticalOffset>viewer.ScrollableHeight) throw new Exception("Scrolled beyond last row");
                if(list.Items.Count!=500) throw new Exception("Scrolling changed list contents");
                Console.WriteLine("PASS: "+list.GetType().Name+" scrolls both ways, stops centrally, clamps at ends and retains all 500 entries.");
                window.Content=null;
            }
            DragEdgeScroll.Stop();DragEdgeScroll.Stop();
            return 0;
        }
        catch(Exception e) { Console.WriteLine(e);return 1; }
    }
    public record Row(string Name);
}
