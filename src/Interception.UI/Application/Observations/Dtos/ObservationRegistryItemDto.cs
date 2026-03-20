//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Lightweight registry row for observation list.
/// </summary>
public sealed record ObservationRegistryItemDto(
    Guid Id,
    DateTime ObservedDate,
    string ActionRaw,
    Guid? ObservationActionId,
    string? ObservationActionName,
    string? Layer,
    string? RmRaw,
    string? LocationRaw,
    string? DistrictRaw,
    string? SubdivisionRaw,
    bool HasUnknownParticipants,
    int ParticipantsCount,
    int TagsCount,
    int ProbableActionsCount,
    IReadOnlyList<string> ParticipantsPreview);
