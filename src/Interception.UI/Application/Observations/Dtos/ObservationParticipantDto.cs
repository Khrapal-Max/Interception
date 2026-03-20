//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Observation participant read model.
/// </summary>
public sealed record ObservationParticipantDto(
    Guid Id,
    string? LabelRaw,
    bool IsUnknown,
    bool StartedAsUnknown,
    string? RoleRaw,
    int Ordinal);
