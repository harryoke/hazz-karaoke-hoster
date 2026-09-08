using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.Core.Interfaces;

public interface ILibraryRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SongRecord>> SearchAsync(string query, int limit = 200, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SongRecord>> SearchByKindAsync(string query, string? mediaKind, int limit = 200, CancellationToken cancellationToken = default);
    Task<SongRecord?> FindByFilePathAsync(string filePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SongRecord>> FindAlternativesAsync(string artist, string title, string? excludeFilePath = null, int limit = 100, CancellationToken cancellationToken = default);
    Task<long> UpsertSongAsync(SongRecord song, CancellationToken cancellationToken = default);
    Task SaveTrackPreferencesAsync(long songId, int keyChange, double cdgSyncSeconds, CancellationToken cancellationToken = default);
    Task<(long Karaoke, long Music)> GetLibraryCountsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SongRecord>> GetRandomCandidatesAsync(string mediaKind, int limit = 64, CancellationToken cancellationToken = default);
    Task<LibraryBrowsePage> BrowseAsync(string mediaKind, string? filter, string sortBy, bool descending, int offset, int pageSize = 500, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VirtualFolder>> GetVirtualFoldersAsync(CancellationToken cancellationToken = default);
    Task<long> CreateVirtualFolderAsync(string name, long? parentId = null, CancellationToken cancellationToken = default);
    Task RenameVirtualFolderAsync(long folderId, string name, CancellationToken cancellationToken = default);
    Task DeleteVirtualFolderAsync(long folderId, CancellationToken cancellationToken = default);
    Task EmptyVirtualFolderAsync(long folderId, CancellationToken cancellationToken = default);
    Task AddSongToVirtualFolderAsync(long folderId, long songId, CancellationToken cancellationToken = default);
    Task RemoveSongFromVirtualFolderAsync(long folderId, long songId, CancellationToken cancellationToken = default);
    Task<LibraryBrowsePage> BrowseVirtualFolderAsync(long folderId, string mediaKind, string? filter, string sortBy, bool descending, int offset, int pageSize = 500, CancellationToken cancellationToken = default);
}
