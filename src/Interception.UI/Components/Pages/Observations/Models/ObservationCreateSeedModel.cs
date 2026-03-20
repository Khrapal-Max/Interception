using Interception.UI.Domain.Enums;

namespace Interception.UI.Components.Pages.Observations.Models;

/// <summary>
/// Parsed draft that can be transferred into the main create drawer.
/// </summary>
public sealed class ObservationCreateSeedModel
{
    public DateTime ObservedDate { get; set; } = DateTime.Now;
    public Guid? ObservationActionId { get; set; }
    public string? ObservationActionName { get; set; }
    public string ActionRaw { get; set; } = string.Empty;
    public string? Layer { get; set; }
    public string? RmRaw { get; set; }
    public string? PointRaw { get; set; }
    public string? LocationRaw { get; set; }
    public string? DistrictRaw { get; set; }
    public string? SubdivisionRaw { get; set; }
    public SubdivisionLinkStrength? SubdivisionStrength { get; set; }
    public ObservationSubdivisionSource SubdivisionSource { get; set; } = ObservationSubdivisionSource.Manual;
    public string? Note { get; set; }
    public string? SourcePost { get; set; }

    public List<ObservationParticipantSeedRow> Participants { get; set; } = [];
    public List<ObservationTagSeedRow> Tags { get; set; } = [];

    public ObservationCreateSeedModel Clone()
        => new()
        {
            ObservedDate = ObservedDate,
            ObservationActionId = ObservationActionId,
            ObservationActionName = ObservationActionName,
            ActionRaw = ActionRaw,
            Layer = Layer,
            RmRaw = RmRaw,
            PointRaw = PointRaw,
            LocationRaw = LocationRaw,
            DistrictRaw = DistrictRaw,
            SubdivisionRaw = SubdivisionRaw,
            SubdivisionStrength = SubdivisionStrength,
            SubdivisionSource = SubdivisionSource,
            Note = Note,
            SourcePost = SourcePost,
            Participants = Participants
                .Select(x => new ObservationParticipantSeedRow
                {
                    LabelRaw = x.LabelRaw,
                    RoleRaw = x.RoleRaw,
                    IsUnknown = x.IsUnknown,
                    Ordinal = x.Ordinal
                })
                .ToList(),
            Tags = Tags
                .Select(x => new ObservationTagSeedRow
                {
                    RawValue = x.RawValue,
                    Kind = x.Kind,
                    Source = x.Source
                })
                .ToList()
        };
}
