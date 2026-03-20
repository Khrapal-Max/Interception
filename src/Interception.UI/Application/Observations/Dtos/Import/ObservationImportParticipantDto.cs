//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos.Import;

/// <summary>
/// Parsed import participant row.
/// </summary>
public sealed class ObservationImportParticipantDto
{
    public string? LabelRaw { get; init; }

    public bool IsUnknown { get; init; }

    public string? RoleRaw { get; init; }
}
