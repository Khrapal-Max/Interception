//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Full observation details for modal/page.
/// </summary>
public sealed record ObservationDetailsDto(
    Guid Id,
    DateTime ObservedDate,
    Guid? ObservationActionId,
    string? ObservationActionName,
    string ActionRaw,
    string ActionNorm,
    string? Layer,
    string? RmRaw,
    string? PointRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string? SubdivisionRaw,
    SubdivisionLinkStrength? SubdivisionStrength,
    ObservationSubdivisionSource? SubdivisionSource,
    string? Note,
    string Source,
    Guid? SourceFileId,
    int? SourceRow,
    string ContentHash,
    DateTime CreatedAtUtc,
    string? CreatedBy,
    IReadOnlyList<ObservationParticipantDto> Participants,
    IReadOnlyList<ObservationTagDto> Tags,
    IReadOnlyList<ObservationProbableActionDto> ProbableActions);
