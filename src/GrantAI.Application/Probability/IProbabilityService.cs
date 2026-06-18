using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;

namespace GrantAI.Application.Probability;

/// <summary>
/// Estimates the probability that a participant clears the entrance threshold
/// for a group. Pure (no I/O); the caller supplies the historical records.
/// </summary>
public interface IProbabilityService
{
    ProbabilityDto Calculate(string code, IReadOnlyList<AdmissionRecord> records);
}
