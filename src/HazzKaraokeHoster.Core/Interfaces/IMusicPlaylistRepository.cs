using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.Core.Interfaces;

public interface IMusicPlaylistRepository
{
    Task<IReadOnlyList<MusicPlaylistSummary>> GetPlaylistsAsync(int limit = 5000, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MusicPlaylistItem>> GetPlaylistItemsAsync(long playlistId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MusicHistoryEntry>> GetMusicHistoryAsync(int limit = 10000, CancellationToken cancellationToken = default);
    Task SavePlaylistAsync(string name, IReadOnlyList<MusicPlaylistSaveItem> items, CancellationToken cancellationToken = default);
    Task RecordMusicPlayAsync(string deckName, int position, long? songId, string filePath, string artist, string title, DateTimeOffset playedAt, double? durationSeconds, CancellationToken cancellationToken = default);
}
