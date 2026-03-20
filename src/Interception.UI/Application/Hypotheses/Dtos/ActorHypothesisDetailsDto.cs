//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Full analytical details for person hypothesis cluster.
/// </summary>
public sealed record ActorHypothesisDetailsDto(
    Guid Id,
    string Title,
    string? Note,
    Guid? ResolvedActorId,
    string? ResolvedActorDisplayName,
    string? ResolvedActorPrimaryRole,
    bool IsArchived,
    DateTime CreatedAtUtc,
    string? CreatedBy,
    DateTime? ArchivedAtUtc,
    IReadOnlyList<ActorHypothesisMemberDto> Members,
    IReadOnlyList<ActorHypothesisObservationDto> Observations);
