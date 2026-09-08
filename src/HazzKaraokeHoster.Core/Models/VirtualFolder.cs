namespace HazzKaraokeHoster.Core.Models;

/// <summary>A named library category. Songs are linked to it without moving their media files.</summary>
public sealed record VirtualFolder(long Id, long? ParentId, string Name, long TrackCount);
