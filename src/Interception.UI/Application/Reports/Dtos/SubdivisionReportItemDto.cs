//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Dtos;

public sealed record SubdivisionReportItemDto(
    Guid? ResolvedSubdivisionId,
    string GroupKey,
    string DisplayName,
    bool IsResolved,
    string? LayerHint,
    string? RmHint,
    int ObservationsCount,
    int DistinctActorsCount,
    int UnknownParticipantsCount,
    DateTime? FirstObservedAt,
    DateTime? LastObservedAt,
    IReadOnlyList<SubdivisionActionStatDto> Actions,
    IReadOnlyList<SubdivisionActorStatDto> Actors,
    IReadOnlyList<SubdivisionTagStatDto> Tags,
    IReadOnlyList<SubdivisionObservationSampleDto> Samples);
