using GrantAI.Domain.Entities;

namespace GrantAI.Application.Abstractions;

/// <summary>
/// Persistence port for <see cref="GrantCutoffRecord"/>. Implemented in
/// Infrastructure with MongoDB.Driver. Kept intentionally small and intent
/// revealing rather than exposing a generic repository.
///
/// This is the grant-side counterpart of <see cref="IAdmissionRepository"/>:
/// the two data streams are independent (entrance threshold versus grant
/// allocation) so they get their own collection and their own port.
/// </summary>
public interface IGrantCutoffRepository
{
    /// <summary>
    /// Returns every record whose group code matches <paramref name="code"/>
    /// (case-insensitive). The main lookup behind /api/grants/{code} and the
    /// grant forecast.
    /// </summary>
    Task<IReadOnlyList<GrantCutoffRecord>> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Distinct educational program group codes that have grant data, sorted.</summary>
    Task<IReadOnlyList<string>> GetGroupCodesAsync(CancellationToken ct = default);

    /// <summary>Every record in the collection (used for the /api/grants list).</summary>
    Task<IReadOnlyList<GrantCutoffRecord>> GetAllAsync(CancellationToken ct = default);

    Task<long> CountAsync(CancellationToken ct = default);

    /// <summary>
    /// Inserts new records and replaces existing ones (idempotent upsert keyed
    /// on the natural <see cref="GrantCutoffRecord.Id"/>).
    /// </summary>
    Task BulkUpsertAsync(IReadOnlyCollection<GrantCutoffRecord> records, CancellationToken ct = default);
}
