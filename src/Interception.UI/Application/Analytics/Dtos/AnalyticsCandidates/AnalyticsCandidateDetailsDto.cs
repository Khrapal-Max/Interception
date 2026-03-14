//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Enums;

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;

public sealed record AnalyticsCandidateDetailsDto(
    string CandidateKey,
    string DisplayName,
    string CandidateType,
    int PriorityScore,
    string PriorityBand,
    AnalyticsCandidateReadiness Readiness,
    int ObservationsCount,
    int DistinctActionsCount,
    string? PrimaryRole,
    DateOnly? LastSeenDate,
    Guid? ClusterId,
    string? ClusterCode,
    Guid? ParticipantId,
    Guid? PrimaryObservationId,
    IReadOnlyList<AnalyticsCandidateObservationDto> Observations,
    IReadOnlyList<AnalyticsCandidateSignalDto> Signals,
    IReadOnlyList<AnalyticsCandidateNodeDto> RelatedNodes,
    IReadOnlyList<string> RecommendedActions);