//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Request for creating a new person hypothesis cluster.
/// </summary>
public sealed record ActorHypothesisCreateDto(
    string Title,
    string? Note,
    IReadOnlyList<Guid> SeedParticipantIds);
