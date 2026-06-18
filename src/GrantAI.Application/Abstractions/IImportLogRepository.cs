using GrantAI.Domain.Entities;

namespace GrantAI.Application.Abstractions;

/// <summary>Persistence port for import audit logs.</summary>
public interface IImportLogRepository
{
    Task AddAsync(ImportLog log, CancellationToken ct = default);

    Task<IReadOnlyList<ImportLog>> GetRecentAsync(int limit, CancellationToken ct = default);
}
