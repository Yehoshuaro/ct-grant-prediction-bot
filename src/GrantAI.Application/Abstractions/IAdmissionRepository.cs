using GrantAI.Domain.Entities;

namespace GrantAI.Application.Abstractions;

/// <summary>
/// Persistence port for <see cref="AdmissionRecord"/>. Implemented in
/// Infrastructure with MongoDB.Driver. Kept intentionally small and intent
/// revealing rather than exposing a generic repository.
/// </summary>
public interface IAdmissionRepository
{
    /// <summary>
    /// Returns every record whose group code OR specialty code matches
    /// <paramref name="code"/> (case-insensitive). This is the main lookup
    /// behind /history, /forecast and /chance.
    /// </summary>
    Task<IReadOnlyList<AdmissionRecord>> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Distinct educational program group codes, sorted.</summary>
    Task<IReadOnlyList<string>> GetGroupCodesAsync(CancellationToken ct = default);

    /// <summary>Every record in the collection (used for the global statistics overview).</summary>
    Task<IReadOnlyList<AdmissionRecord>> GetAllAsync(CancellationToken ct = default);

    Task<long> CountAsync(CancellationToken ct = default);

    /// <summary>Of the supplied ids, returns those that already exist (duplicate detection).</summary>
    Task<IReadOnlyCollection<string>> GetExistingIdsAsync(
        IReadOnlyCollection<string> ids, CancellationToken ct = default);

    /// <summary>
    /// Inserts new records and replaces existing ones (idempotent upsert keyed
    /// on the natural <see cref="AdmissionRecord.Id"/>).
    /// </summary>
    Task BulkUpsertAsync(IReadOnlyCollection<AdmissionRecord> records, CancellationToken ct = default);
}
