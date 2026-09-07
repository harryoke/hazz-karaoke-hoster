using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.Core.Interfaces;

public interface IExternalLibraryImportService
{
    Task<ExternalImportPreview> PreviewAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<ExternalImportResult> ImportAsync(
        string sourcePath,
        ExternalMediaKindMode mediaKindMode,
        bool verifyPaths,
        IProgress<ExternalImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
