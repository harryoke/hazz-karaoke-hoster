using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.Core.Interfaces;

public interface IKarmaImportService
{
    Task<KarmaImportPreview> PreviewAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<KarmaImportResult> ImportAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<KarmaLibraryInspection> InspectLibraryAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<KarmaLibraryImportResult> ImportLibraryAsync(
        string sourcePath,
        KarmaLibraryColumnMapping mapping,
        bool verifyFilePaths = false,
        IProgress<KarmaLibraryImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
