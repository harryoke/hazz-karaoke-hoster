namespace HazzKaraokeHoster.Core.Models;

/// <summary>
/// One bounded page from the indexed media library.  Hazz never materialises the
/// full multi-million-row library in the UI; the browser requests pages on demand.
/// </summary>
public sealed record LibraryBrowsePage(
    IReadOnlyList<SongRecord> Items,
    long TotalCount,
    int Offset,
    int PageSize)
{
    public int PageNumber => PageSize <= 0 ? 1 : (Offset / PageSize) + 1;
    public long PageCount => PageSize <= 0 ? 1 : Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
    public bool HasPrevious => Offset > 0;
    public bool HasNext => (long)Offset + Items.Count < TotalCount;
}
