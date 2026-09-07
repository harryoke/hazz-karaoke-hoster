using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.Core.Interfaces;

public interface IBpmStudioImportService
{
    Task<BpmStudioImportPreview> PreviewAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<BpmStudioImportResult> ImportAsync(
        string sourcePath,
        IProgress<BpmStudioImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
