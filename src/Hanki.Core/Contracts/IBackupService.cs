using Hanki.Core.Models;

namespace Hanki.Core.Contracts;

public interface IBackupService
{
    Task ExportAsync(string filePath, CancellationToken cancellationToken = default);
    Task<ImportResult> ImportAsync(
        string filePath,
        ImportConflictStrategy strategy,
        CancellationToken cancellationToken = default);
}
