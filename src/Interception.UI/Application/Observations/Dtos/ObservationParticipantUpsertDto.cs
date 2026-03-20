//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Create/update participant request.
/// </summary>
public sealed class ObservationParticipantUpsertDto
{
    public Guid? Id { get; init; }

    public string? LabelRaw { get; init; }

    public bool IsUnknown { get; init; }

    public string? RoleRaw { get; init; }

    public int? Ordinal { get; init; }
}
