using AutoMapper;
using GrantAI.Application.Common;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Domain.Entities;

namespace GrantAI.Application.Mapping;

/// <summary>
/// AutoMapper configuration. The only non-trivial projection is a campaign
/// point, where the participation rate, pass rate and human label are derived
/// rather than copied. Centralising it here keeps the analytics code declarative.
/// </summary>
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<AdmissionRecord, CampaignPointDto>()
            .ForMember(d => d.Label,
                o => o.MapFrom(s => CampaignOrder.Label(s.Year, s.Season)))
            .ForMember(d => d.ParticipationRate,
                o => o.MapFrom(s => s.Applications > 0
                    ? Math.Round((double)s.Participants / s.Applications * 100.0, 2)
                    : 0d))
            .ForMember(d => d.PassRate,
                o => o.MapFrom(s => s.Participants > 0
                    ? Math.Round((double)s.PassedThreshold / s.Participants * 100.0, 2)
                    : 0d));
    }
}
