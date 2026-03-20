//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Search filter for observation registry.
/// </summary>
public sealed class ObservationRegistryFilterDto
{
    public string? Query { get; set; }

    public DateTime? ObservedFrom { get; set; }

    public DateTime? ObservedTo { get; set; }

    public Guid? ObservationActionId { get; set; }

    public TagKind? TagKind { get; set; }

    public SubdivisionLinkStrength? SubdivisionStrength { get; set; }

    public bool OnlyUnknownParticipants { get; set; }

    public bool OnlyBoundAction { get; set; }

    public int Skip { get; set; }

    public int Take { get; set; } = 25;
}
