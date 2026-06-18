using FluentValidation;
using GrantAI.Domain.Entities;

namespace GrantAI.Application.Validation;

/// <summary>
/// Validates a parsed <see cref="AdmissionRecord"/> before it is persisted.
/// Encodes the invariants the count-based statistics must satisfy: non-negative
/// counts, participants no greater than applications, and pass/fail counts no
/// greater than the number of participants.
/// </summary>
public sealed class AdmissionRecordValidator : AbstractValidator<AdmissionRecord>
{
    public AdmissionRecordValidator()
    {
        RuleFor(r => r.Year).InclusiveBetween(2000, 2100);
        RuleFor(r => r.GroupCode).NotEmpty();

        RuleFor(r => r.Applications).GreaterThanOrEqualTo(0);
        RuleFor(r => r.Participants).GreaterThanOrEqualTo(0);
        RuleFor(r => r.PassedThreshold).GreaterThanOrEqualTo(0);
        RuleFor(r => r.FailedThreshold).GreaterThanOrEqualTo(0);

        RuleFor(r => r.Participants)
            .LessThanOrEqualTo(r => r.Applications)
            .WithMessage("Participants cannot exceed applications.");

        RuleFor(r => r.PassedThreshold)
            .LessThanOrEqualTo(r => r.Participants)
            .WithMessage("Passed count cannot exceed participants.");

        RuleFor(r => r)
            .Must(r => r.PassedThreshold + r.FailedThreshold <= r.Participants)
            .WithMessage("Passed + failed cannot exceed participants.");
    }
}
