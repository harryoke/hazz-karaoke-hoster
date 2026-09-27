namespace HazzKaraokeHoster.Core.Models;

public static class CrossfadeCurve
{
    public static readonly string[] Names = ["Linear", "Equal power", "Smooth", "Overlap", "Fade out then in"];
    public static string Normalize(string? name) => Names.Contains(name) ? name! : Names[0];
    public static (double Outgoing, double Incoming) Gains(string? name, double progress)
    {
        var t = double.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 0;
        if (t == 0) return (1, 0);
        if (t == 1) return (0, 1);
        return Normalize(name) switch
        {
            "Equal power" => (Math.Cos(t * Math.PI / 2), Math.Sin(t * Math.PI / 2)),
            "Smooth" => (1 - t * t * (3 - 2 * t), t * t * (3 - 2 * t)),
            "Overlap" => (Math.Min(1, 2 * (1 - t)), Math.Min(1, 2 * t)),
            "Fade out then in" => (Math.Max(0, 1 - 2 * t), Math.Max(0, 2 * t - 1)),
            _ => (1 - t, t)
        };
    }
}
