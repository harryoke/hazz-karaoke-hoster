using HazzKaraokeHoster.Core.Models;
namespace HazzKaraokeHoster.App;

public sealed class RotationRoundState
{
    public int Round { get; set; } = 1;
    public bool Started { get; set; }
    public int ReturningSinceNew { get; set; }
}

public static class RotationMethods
{
    public static readonly string[] Names = { "Round robin", "Closed rounds", "Newcomers at the end", "Interleaved newcomers", "Longest waiting", "Fewest turns", "Group rotation", "Arrival order" };
    public static bool IsRoundBased(string method) => method is "Round robin" or "Closed rounds" or "Newcomers at the end" or "Interleaved newcomers" or "Group rotation";
    public static SingerQueueEntry[] Order(SingerQueueEntry[] singers, Func<SingerQueueEntry, FairTurnRecord> record,
        RotationRoundState state, string method, string placement, int spacing, string tie, bool avoid, long lastTurn)
    {
        if (!IsRoundBased(method)) return FairRotationPolicy.Order(singers, record, method, tie, avoid, lastTurn);
        spacing = Math.Clamp(spacing, 1, 10);
        var ready = singers.Where(x => !x.IsHeld && x.Songs.Count > 0).ToArray();
        string Group(SingerQueueEntry x) => string.IsNullOrWhiteSpace(record(x).Group) ? "singer:" + x.Id : "group:" + record(x).Group.Trim().ToUpperInvariant();
        int LastRound(SingerQueueEntry x) => method == "Group rotation" ? singers.Where(y => Group(y) == Group(x)).Max(y => record(y).LastRound) : record(x).LastRound;
        if (ready.Length > 0 && !ready.Any(x => LastRound(x) < state.Round && record(x).EligibleRound <= state.Round))
        {
            state.Round = Math.Max(state.Round + 1, ready.Min(x => record(x).EligibleRound));
            state.ReturningSinceNew = 0;
        }
        state.Started = true;
        var eligible = ready.Where(x => LastRound(x) < state.Round && record(x).EligibleRound <= state.Round).OrderBy(x => record(x).Arrived).ToList();
        if (method == "Group rotation")
            eligible = eligible.GroupBy(Group).Select(g => g.OrderBy(x => record(x).LastTurn).ThenBy(x => record(x).Arrived).First()).OrderBy(x => record(x).Arrived).ToList();
        var policy = method == "Interleaved newcomers" ? "Interleave" : method == "Newcomers at the end" ? "End of current round" : placement;
        if (policy == "Interleave")
        {
            var newcomers = new Queue<SingerQueueEntry>(eligible.Where(x => record(x).Turns == 0));
            var returning = new Queue<SingerQueueEntry>(eligible.Where(x => record(x).Turns > 0));
            eligible.Clear();
            var count = state.ReturningSinceNew;
            while (newcomers.Count > 0 || returning.Count > 0)
            {
                if (newcomers.Count > 0 && (count >= spacing || returning.Count == 0)) { eligible.Add(newcomers.Dequeue()); count = 0; }
                else { eligible.Add(returning.Dequeue()); count++; }
            }
        }
        if (avoid && eligible.Count > 1 && record(eligible[0]).LastTurn == lastTurn && lastTurn > 0)
        { var first = eligible[0]; eligible.RemoveAt(0); eligible.Add(first); }
        // Every singer stays visible. Ready singers already served this round follow its remaining slots.
        return eligible.Concat(singers.Except(eligible).OrderBy(x => x.IsHeld ? 2 : x.Songs.Count == 0 ? 1 : 0).ThenBy(x => record(x).Arrived)).ToArray();
    }
}
