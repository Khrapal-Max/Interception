//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Paged registry result for subdivision hypotheses.
/// </summary>
public sealed record SubdivisionHypothesisRegistryPageDto(
    IReadOnlyList<SubdivisionHypothesisRegistryItemDto> Items,
    int TotalCount);
