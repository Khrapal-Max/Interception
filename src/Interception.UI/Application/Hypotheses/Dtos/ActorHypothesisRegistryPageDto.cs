//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Paged registry result for person hypotheses.
/// </summary>
public sealed record ActorHypothesisRegistryPageDto(
    IReadOnlyList<ActorHypothesisRegistryItemDto> Items,
    int TotalCount);
