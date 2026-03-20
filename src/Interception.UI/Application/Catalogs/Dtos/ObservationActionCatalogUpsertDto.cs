//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Create/update payload for typed action catalog item.
/// </summary>
public sealed record ObservationActionCatalogUpsertDto(
    string Name,
    ObservationActionCategory Category,
    string? InitiatorRoleName,
    string? ResponderRoleName,
    string? Description,
    short? TypicalParticipantsCount,
    bool RequiresCounterparty);
