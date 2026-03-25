//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.Registry;

/// <summary>Плоский знімок учасника для read-side реєстру.</summary>
internal sealed record InterceptionRegistryParticipantSnapshot(
    Guid Id,
    string? Name,
    string? Role,
    bool IsUnknown,
    int Ordinal);
