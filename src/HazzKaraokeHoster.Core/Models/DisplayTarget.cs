namespace HazzKaraokeHoster.Core.Models;

public sealed record DisplayTarget(int Index, string Name, bool IsPrimary, int Left, int Top, int Width, int Height)
{
    public string Label => $"Display {Index + 1}{(IsPrimary ? " (Primary)" : string.Empty)} — {Width}x{Height}";
}
