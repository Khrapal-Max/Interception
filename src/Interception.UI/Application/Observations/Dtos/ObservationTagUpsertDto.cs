//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Create/update tag request.
/// </summary>
public sealed class ObservationTagUpsertDto
{
    public Guid? Id { get; init; }

    public string RawValue { get; init; } = string.Empty;

    public TagKind Kind { get; init; }

    public ObservationTagSource Source { get; init; } = ObservationTagSource.Manual;

    public Guid? TagCatalogId { get; init; }
}
