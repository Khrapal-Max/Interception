//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Короткий DTO пов'язаного raw-учасника з того ж observation.
/// </summary>
public sealed record AnalyticsRelatedParticipantDto(
    Guid ParticipantId,
    int Ordinal,
    string DisplayName,
    bool IsUnknown,
    string? RoleRaw,
    string EffectiveNodeDisplay);
