//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Observation tag read model.
/// </summary>
public sealed record ObservationTagDto(
    Guid Id,
    string RawValue,
    TagKind Kind,
    ObservationTagSource Source,
    Guid? TagCatalogId,
    string? TagCatalogName);
