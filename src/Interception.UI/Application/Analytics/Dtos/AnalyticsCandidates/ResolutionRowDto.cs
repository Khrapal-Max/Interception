//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;

public sealed record ResolutionRowDto(
        Guid ObservationParticipantId,
        Guid ClusterId,
        string ClusterCode,
        string? ClusterDisplayName,
        Guid? ResolvedActorId,
        string? ResolvedActorDisplayName);