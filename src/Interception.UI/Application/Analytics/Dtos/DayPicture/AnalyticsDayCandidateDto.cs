//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Enums;

namespace Interception.UI.Application.Analytics.Dtos.DayPicture;

public sealed record AnalyticsDayCandidateDto(
    string CandidateKey,
    string DisplayName,
    int PriorityScore,
    AnalyticsCandidateReadiness Readiness,
    int ObservationsCount,
    IReadOnlyList<string> TopSignals,
    Guid? ClusterId,
    Guid? ParticipantId);