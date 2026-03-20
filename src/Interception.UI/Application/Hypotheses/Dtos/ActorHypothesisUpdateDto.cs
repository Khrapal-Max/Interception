//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Request for updating person hypothesis metadata.
/// </summary>
public sealed record ActorHypothesisUpdateDto(
    string Title,
    string? Note);
