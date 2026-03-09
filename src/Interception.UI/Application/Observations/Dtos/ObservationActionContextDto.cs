//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Контекст поточної дії: пов'язані учасники та схожі спостереження.
/// </summary>
public sealed record ObservationActionContextDto(
    IReadOnlyList<ActionContextParticipantDto> RelatedParticipants,
    IReadOnlyList<ActionContextObservationDto> SimilarObservations);
