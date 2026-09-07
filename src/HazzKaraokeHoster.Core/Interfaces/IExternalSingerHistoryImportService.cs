using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.Core.Interfaces;

public interface IExternalSingerHistoryImportService
{
    Task<SingerHistoryImportPreview> PreviewAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<SingerHistoryImportResult> ImportAsync(string sourcePath, IProgress<SingerHistoryImportProgress>? progress = null, CancellationToken cancellationToken = default);
}
