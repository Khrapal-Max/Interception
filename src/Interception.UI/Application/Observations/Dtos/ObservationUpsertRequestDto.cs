//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Create/update request for a full observation aggregate.
/// </summary>
public sealed class ObservationUpsertRequestDto
{
    public DateTime ObservedDate { get; init; }

    public Guid? ObservationActionId { get; init; }

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

    public IReadOnlyList<ObservationParticipantUpsertDto> Participants { get; init; } = Array.Empty<ObservationParticipantUpsertDto>();

    public IReadOnlyList<ObservationTagUpsertDto> Tags { get; init; } = Array.Empty<ObservationTagUpsertDto>();

    public IReadOnlyList<ObservationProbableActionUpsertDto> ProbableActions { get; init; } = Array.Empty<ObservationProbableActionUpsertDto>();
}
