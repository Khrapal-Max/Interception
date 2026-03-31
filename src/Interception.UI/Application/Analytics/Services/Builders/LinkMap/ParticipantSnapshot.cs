//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Services.Builders.LinkMap;

internal sealed record ParticipantSnapshot(
    string Name,
    string? Role,
    DateTime ObservedAt,
    string? Frequency);
