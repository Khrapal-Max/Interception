//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Member of a person hypothesis cluster.
/// </summary>
public sealed record ActorHypothesisMemberDto(
    Guid MemberId,
    Guid ObservationParticipantId,
    Guid ObservationId,
    DateTime ObservedDate,
    int Ordinal,
    string DisplayLabel,
    bool IsUnknown,
    bool StartedAsUnknown,
    string? RoleRaw,
    string? LinkNote,
    DateTime LinkedAtUtc);
