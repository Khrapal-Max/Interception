//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Suggestion from typed action catalog.
/// </summary>
public sealed record ActionCatalogSuggestionDto(
    Guid Id,
    string Name,
    ObservationActionCategory Category,
    string? InitiatorRoleName,
    string? ResponderRoleName,
    short? TypicalParticipantsCount,
    bool RequiresCounterparty);
