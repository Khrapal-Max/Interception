//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Suggestion from raw action history.
/// </summary>
public sealed record ActionTextSuggestionDto(
    string ActionRaw,
    string ActionNorm,
    int SeenCount,
    Guid? BoundObservationActionId,
    string? BoundObservationActionName,
    DateTime? LastSeenAtUtc);
