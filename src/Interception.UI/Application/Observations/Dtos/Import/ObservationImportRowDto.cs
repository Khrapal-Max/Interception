//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Observations.Dtos.Import;

/// <summary>
/// Parsed import row ready for application validation/persistence.
/// </summary>
public sealed class ObservationImportRowDto
{
    public int RowNumber { get; init; }

    public DateTime ObservedDate { get; init; }

    public string ActionRaw { get; init; } = string.Empty;

    public string? Layer { get; init; }

    public string? RmRaw { get; init; }

    public string? PointRaw { get; init; }

    public string? LocationRaw { get; init; }

    public string? DistrictRaw { get; init; }

    public string? SubdivisionRaw { get; init; }

    public SubdivisionLinkStrength? SubdivisionStrength { get; init; }

    public ObservationSubdivisionSource? SubdivisionSource { get; init; }

    public string? Note { get; init; }

    public IReadOnlyList<ObservationImportParticipantDto> Participants { get; init; } = Array.Empty<ObservationImportParticipantDto>();
}
