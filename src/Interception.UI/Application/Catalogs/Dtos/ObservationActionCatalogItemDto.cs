//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Row in typed action catalog registry.
/// </summary>
public sealed record ObservationActionCatalogItemDto(
    Guid Id,
    string Name,
    ObservationActionCategory Category,
    string? InitiatorRoleName,
    string? ResponderRoleName,
    short? TypicalParticipantsCount,
    bool RequiresCounterparty,
    bool IsActive,
    int BoundObservationsCount,
    int ProbableUsageCount,
    DateTime CreatedAtUtc);
