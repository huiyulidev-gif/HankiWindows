using Hanki.Core.Models;

namespace Hanki.Core.Contracts;

public interface IShortcutRepository
{
    Task<IReadOnlyList<ShortcutItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ShortcutItem shortcut, CancellationToken cancellationToken = default);
    Task UpdateAsync(ShortcutItem shortcut, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task IncrementUsageAsync(string id, DateTimeOffset usedAt, CancellationToken cancellationToken = default);
}
