//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Probable analytical action read model.
/// </summary>
public sealed record ObservationProbableActionDto(
    Guid Id,
    Guid ObservationActionId,
    string ObservationActionName,
    decimal Confidence,
    string? Reason,
    ProbableActionSource Source,
    DateTime CreatedAtUtc,
    string? CreatedBy);
