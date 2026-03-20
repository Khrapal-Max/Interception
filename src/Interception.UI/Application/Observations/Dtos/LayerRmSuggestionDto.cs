//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Suggestion pair for Layer -> R/M.
/// </summary>
public sealed record LayerRmSuggestionDto(
    string Layer,
    string? RmRaw,
    int SeenCount);
