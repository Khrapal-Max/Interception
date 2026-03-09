//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Пояснення пов'язаного учасника в контексті поточної дії.
/// </summary>
public sealed record ActionContextParticipantDto(
    string Key,
    string Display,
    string LinkType,
    int Weight,
    DateOnly? LastSeenDate,
    string? PrimaryRole,
    string? Description);
