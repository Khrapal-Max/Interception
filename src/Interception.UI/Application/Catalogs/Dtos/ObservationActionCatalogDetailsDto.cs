//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Catalogs.Dtos;

/// <summary>
/// Full typed action catalog item for editor/details form.
/// </summary>
public sealed record ObservationActionCatalogDetailsDto(
    Guid Id,
    string Name,
    ObservationActionCategory Category,
    string? InitiatorRoleName,
    string? ResponderRoleName,
    string? Description,
    short? TypicalParticipantsCount,
    bool RequiresCounterparty,
    bool IsActive,
    int BoundObservationsCount,
    int ProbableUsageCount,
    DateTime CreatedAtUtc,
    string? CreatedBy);
