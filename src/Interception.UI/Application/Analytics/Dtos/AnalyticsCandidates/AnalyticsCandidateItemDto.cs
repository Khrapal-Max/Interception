//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Enums;

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;

public sealed record AnalyticsCandidateItemDto(
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
    bool HasOpenHypothesis,
    Guid? ClusterId,
    string? ClusterCode,
    Guid? ParticipantId,
    Guid? PrimaryObservationId,
    IReadOnlyList<string> TopActions,
    IReadOnlyList<string> TopSignals,
    string RecommendedAction);