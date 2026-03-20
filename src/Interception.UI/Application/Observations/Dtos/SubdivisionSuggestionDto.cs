//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Suggestion for raw subdivision hints.
/// </summary>
public sealed record SubdivisionSuggestionDto(
    string RawValue,
    string NormValue,
    SubdivisionLinkStrength? Strength,
    int SeenCount,
    DateTime? LastSeenAtUtc);
