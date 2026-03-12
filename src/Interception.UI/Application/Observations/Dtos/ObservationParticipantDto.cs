//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// DTO учасника observation з урахуванням поточної аналітичної резолюції.
/// </summary>
public sealed record ObservationParticipantDto(
    Guid Id,
    string? LabelRaw,
    bool IsUnknown,
    string? RoleRaw,
    int Ordinal,
    string EffectiveIdentityKey,
    string EffectiveDisplayName,
    Guid? UnknownClusterId,
    string? UnknownClusterCode,
    string? HypothesisRole,
    Guid? ResolvedActorId,
    string? ResolvedActorDisplayName,
    string? ConfirmedRole);