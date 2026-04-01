//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Registry.Builders;

/// <summary>Плоский знімок рядка реєстру перехоплень до мапінгу у DTO.</summary>
internal sealed record InterceptionRegistryItemSnapshot(
    Guid Id,
    DateTime ObservedDate,
    string? Frequency,
    string? VectorSignal,
    string? Division,
    string? ActionName,
    IReadOnlyList<InterceptionRegistryParticipantSnapshot> Participants,
    IReadOnlyList<string> Labels);
