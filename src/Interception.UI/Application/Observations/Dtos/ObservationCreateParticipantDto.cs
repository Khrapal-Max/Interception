//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

public sealed record ObservationCreateParticipantDto(
    string? LabelRaw,
    bool IsUnknown,
    string? RoleRaw);
