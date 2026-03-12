//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Services;

public sealed partial class ObservationRegistryService
{
    public sealed record ParticipantResolutionReadModel(
        Guid ObservationParticipantId,
        Guid UnknownClusterId,
        string? ClusterCode,
        string? ClusterDisplayName,
        string? ClusterRole,
        string? ArchiveReason,
        Guid? ResolvedActorId,
        string? ResolvedActorDisplayName,
        string? ConfirmedRole);
}