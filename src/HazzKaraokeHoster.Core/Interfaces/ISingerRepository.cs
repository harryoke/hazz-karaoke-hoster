using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.Core.Interfaces;

public interface ISingerRepository
{
    Task<IReadOnlyList<Singer>> SearchSingersAsync(string query, int limit = 100, CancellationToken cancellationToken = default);
    Task<long> UpsertSingerAsync(string displayName, string? notes = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Singer>> GetSingersAsync(int limit = 5000, CancellationToken cancellationToken = default);
    Task<Singer?> GetSingerAsync(long singerId, CancellationToken cancellationToken = default);
    Task UpdateSingerAsync(long singerId, string displayName, string? notes, CancellationToken cancellationToken = default);
    Task AddHistoryAsync(long singerId, long? songId, string artist, string title, string filePath, DateTimeOffset sungAt, int keyChange, double cdgSyncSeconds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SingerHistoryEntry>> GetHistoryAsync(long singerId, int limit = 500, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SingerHistoryEntry>> SearchHistoryAsync(long singerId, string query, int limit = 5000, CancellationToken cancellationToken = default);
    Task<RecentSingerPerformance?> FindRecentPerformanceAsync(long? songId, string artist, string title, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);
}
