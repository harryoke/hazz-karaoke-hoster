using System.Windows;
using System.Windows.Controls;
using HazzKaraokeHoster.Core.Models;
namespace HazzKaraokeHoster.App;

public sealed class FairTurnRecord
{
    public int LastRound { get; set; }
    public int EligibleRound { get; set; } = 1;
    public string Group { get; set; } = "";
    public int Turns { get; set; }
    public long Arrived { get; set; }
    public long LastTurn { get; set; }
}

public static class FairRotationPolicy
{
    public static SingerQueueEntry[] Order(IEnumerable<SingerQueueEntry> singers, Func<SingerQueueEntry, FairTurnRecord> record,
        string primary = "Fewest turns", string secondary = "Longest waiting", bool avoidConsecutive = false, long lastTurn = 0)
    {
        long Value(SingerQueueEntry singer, string rule)
        {
            var item = record(singer);
            return rule == "Fewest turns" ? item.Turns : rule == "Arrival order" ? item.Arrived : item.LastTurn == 0 ? item.Arrived : item.LastTurn;
        }
        return singers.OrderBy(x => x.IsHeld ? 2 : x.Songs.Count == 0 ? 1 : 0)
            .ThenBy(x => avoidConsecutive && lastTurn > 0 && record(x).LastTurn == lastTurn ? 1 : 0)
            .ThenBy(x => Value(x, primary)).ThenBy(x => Value(x, secondary))
            .ThenBy(x => record(x).Arrived).ToArray();
    }
}

public partial class MainWindow
{
    private bool _fairRotation;
    private bool _sortingFairRotation;
    private long _fairSequence;
    private string _fairPrimary = "Fewest turns";
    private string _fairSecondary = "Longest waiting";
    private bool _fairAvoidConsecutive;
    private RotationRoundState _rotationRound = new();
    private string _newcomerPlacement = "End of current round";
    private int _newcomerSpacing = 2;
    private Dictionary<string, FairTurnRecord> _fairTurns = new();
    private static string FairSingerKey(SingerQueueEntry singer) => singer.SingerId is long id ? "id:" + id : "name:" + singer.SingerName.Trim().ToUpperInvariant();
    private FairTurnRecord FairRecord(SingerQueueEntry singer)
    {
        var key = FairSingerKey(singer);
        if (!_fairTurns.TryGetValue(key, out var record))
            _fairTurns[key] = record = new FairTurnRecord { Arrived = ++_fairSequence,
                EligibleRound = _rotationRound.Round + (_rotationRound.Started && (_fairPrimary == "Closed rounds" || (RotationMethods.IsRoundBased(_fairPrimary) && _newcomerPlacement == "Next round" && _fairPrimary is not "Interleaved newcomers" and not "Newcomers at the end")) ? 1 : 0) };
        return record;
    }
    private void FairRotation_Click(object sender, RoutedEventArgs e)
    {
        _fairRotation = FairRotationMenuItem.IsChecked;
        SaveMainLayout();
        UpdateAudienceNext();
        MarkLiveShowStateDirty();
        QueueDragHint.Text = _fairRotation
            ? $"AUTO ROTATION: {_fairPrimary}, then {_fairSecondary}. Configure under Show > Rotation Settings."
            : "Manual rotation enabled — drag singers or use Move Up / Down.";
    }
    private void RecordFairTurn(SingerQueueEntry singer)
    {
        var record = FairRecord(singer);
        _rotationRound.ReturningSinceNew = record.Turns == 0 ? 0 : _rotationRound.ReturningSinceNew + 1;
        record.Turns++;
        record.LastTurn = ++_fairSequence;
        record.LastRound = _rotationRound.Round;
        if (_fairPrimary == "Group rotation" && !string.IsNullOrWhiteSpace(record.Group))
            foreach (var member in _fairTurns.Values.Where(x => string.Equals(x.Group.Trim(), record.Group.Trim(), StringComparison.OrdinalIgnoreCase))) member.LastRound = _rotationRound.Round;
    }
    private void ApplyFairRotation()
    {
        if (_sortingFairRotation || _restoringLiveShowState) return;
        // Track arrival even in manual mode, so enabling fair mode has useful history.
        foreach (var singer in _queue) FairRecord(singer);
        if (!_fairRotation) return;
        _sortingFairRotation = true;
        try
        {
            var active = _activeSingerSongCheckedOut ? _activeSinger : null;
            var positions = _queue.Select((singer, index) => (singer, index)).Where(x => !ReferenceEquals(x.singer, active)).ToArray();
            var ordered = RotationMethods.Order(positions.Select(x => x.singer).ToArray(), FairRecord, _rotationRound,
                _fairPrimary, _newcomerPlacement, _newcomerSpacing, _fairSecondary,
                _fairAvoidConsecutive, _fairTurns.Values.Select(x => x.LastTurn).DefaultIfEmpty().Max());
            for (var i = 0; i < ordered.Length; i++)
            {
                var current = _queue.IndexOf(ordered[i]);
                if (current != positions[i].index) _queue.Move(current, positions[i].index);
            }
        }
        finally { _sortingFairRotation = false; }
    }

    private void RotationSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new Window { Owner = this, Title = "Rotation Settings", Width = 590, Height = 700, MaxHeight = SystemParameters.WorkArea.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(20) }; window.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var enabled = new CheckBox { Content = "Enable software-managed rotation (off = original manual rotation)", IsChecked = _fairRotation, Margin = new Thickness(0,0,0,12) }; panel.Children.Add(enabled);
        var rules = new[] { "Fewest turns", "Longest waiting", "Arrival order" };
        panel.Children.Add(new TextBlock { Text = "Main rule" });
        var primary = new ComboBox { ItemsSource = RotationMethods.Names, SelectedItem = _fairPrimary, MinHeight = 30 }; panel.Children.Add(primary);
        var description = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,8) }; panel.Children.Add(description);
        var descriptions = new Dictionary<string,string> {
            ["Round robin"] = "One turn per singer per round, in arrival order. Choose how newcomers join below.",
            ["Closed rounds"] = "Freeze each round. New arrivals wait for the next round.",
            ["Newcomers at the end"] = "One turn per round; append newcomers to the current round.",
            ["Interleaved newcomers"] = "One turn per round; insert a first-time singer after N returning singers when available.",
            ["Group rotation"] = "One song per named group per round. Each ungrouped singer has their own slot. The longest-waiting ready member represents a group.",
            ["Longest waiting"] = "Oldest arrival or previous turn first, without round boundaries.",
            ["Fewest turns"] = "Lowest total number of started turns this show first; newcomers can catch up.",
            ["Arrival order"] = "Always sort by original arrival, even after performing. This is not round robin." };
        panel.Children.Add(new TextBlock { Text = "Newcomer placement (Round robin / Group rotation)" });
        var placement = new ComboBox { ItemsSource = new[] { "End of current round", "Next round", "Interleave" }, SelectedItem = _newcomerPlacement, MinHeight = 30 }; panel.Children.Add(placement);
        panel.Children.Add(new TextBlock { Text = "Returning singers between newcomers (1–10)" });
        var spacing = new Slider { Minimum = 1, Maximum = 10, Value = _newcomerSpacing, TickFrequency = 1, IsSnapToTickEnabled = true }; panel.Children.Add(spacing);
        var spacingText = new TextBlock(); panel.Children.Add(spacingText);
        spacing.ValueChanged += (_, _) => spacingText.Text = $"One newcomer after {spacing.Value:0} returning singer(s)";
        spacingText.Text = $"One newcomer after {spacing.Value:0} returning singer(s)";
        void Describe() { var method = primary.SelectedItem as string ?? "Round robin"; description.Text = descriptions[method]; placement.IsEnabled = method is "Round robin" or "Group rotation"; spacing.IsEnabled = method == "Interleaved newcomers" || placement.SelectedItem as string == "Interleave"; }
        primary.SelectionChanged += (_, _) => Describe(); placement.SelectionChanged += (_, _) => Describe(); Describe();
        panel.Children.Add(new TextBlock { Text = "When tied, use", Margin = new Thickness(0,12,0,0) });
        var secondary = new ComboBox { ItemsSource = rules, SelectedItem = _fairSecondary, MinHeight = 30 }; panel.Children.Add(secondary);
        primary.SelectionChanged += (_, _) => secondary.IsEnabled = !RotationMethods.IsRoundBased(primary.SelectedItem as string ?? "");
        secondary.IsEnabled = !RotationMethods.IsRoundBased(_fairPrimary);
        panel.Children.Add(new TextBlock { Text = "Group for the singer selected in the main queue (blank = individual)" });
        var groupSinger = QueueList.SelectedItem as SingerQueueEntry;
        var group = new TextBox { Text = groupSinger is null ? "" : FairRecord(groupSinger).Group, IsEnabled = groupSinger is not null, MaxLength = 80, MinHeight = 30 }; panel.Children.Add(group);
        panel.Children.Add(new TextBlock { Text = groupSinger is null ? "Select a singer before opening settings to assign a group." : "Selected singer: " + groupSinger.SingerName });
        var avoid = new CheckBox { Content = "Avoid consecutive turns when another singer is ready", IsChecked = _fairAvoidConsecutive, Margin = new Thickness(0,16,0,12) }; panel.Children.Add(avoid);
        panel.Children.Add(new TextBlock { Text = "Fewest turns: prioritise singers who have sung less this show.\nLongest waiting: prioritise the oldest arrival or previous turn.\nArrival order: prioritise earliest arrivals, even after they have sung.\n\nHeld singers and those without songs stay behind ready singers. The current performer stays in place. Manual moves require automatic ordering to be switched off.", TextWrapping = TextWrapping.Wrap });
        var apply = new Button { Content = "APPLY", MinHeight = 32, Margin = new Thickness(0,16,0,0) }; panel.Children.Add(apply);
        apply.Click += (_, _) =>
        {
            _fairPrimary = primary.SelectedItem as string ?? rules[0];
            _newcomerPlacement = placement.SelectedItem as string ?? "End of current round";
            _newcomerSpacing = (int)spacing.Value;
            if (groupSinger is not null) FairRecord(groupSinger).Group = group.Text.Trim();
            _fairSecondary = secondary.SelectedItem as string ?? rules[1];
            _fairAvoidConsecutive = avoid.IsChecked == true;
            _fairRotation = enabled.IsChecked == true;
            FairRotationMenuItem.IsChecked = _fairRotation;
            UpdateAudienceNext(); MarkLiveShowStateDirty(); SaveLiveShowStateNow(false); SaveMainLayout();
            window.Close();
        };
        window.ShowDialog();
    }
}
