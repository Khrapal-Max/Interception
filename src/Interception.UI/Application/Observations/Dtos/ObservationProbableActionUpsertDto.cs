//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Create/update probable action request.
/// </summary>
public sealed class ObservationProbableActionUpsertDto
{
    public Guid? Id { get; init; }

    public Guid ObservationActionId { get; init; }

    public decimal Confidence { get; init; }

    public string? Reason { get; init; }

    public ProbableActionSource Source { get; init; } = ProbableActionSource.Manual;
}
