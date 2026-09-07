using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.Core.Interfaces;

public interface ILibraryImportService
{
    Task<LibraryImportResult> ImportAsync(
        LibraryImportOptions options,
        IProgress<LibraryImportProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<SingleFileIndexResult> IndexFileAsync(
        string path,
        LibraryImportMode mode,
        CancellationToken cancellationToken = default);
}
