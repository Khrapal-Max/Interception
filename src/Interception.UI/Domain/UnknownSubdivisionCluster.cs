//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Analytical cluster for raw subdivision hints that are not yet confirmed.
/// </summary>
public sealed class UnknownSubdivisionCluster
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string LabelRaw { get; private set; } = default!;
    public string LabelNorm { get; private set; } = default!;
    public string? LayerHint { get; private set; }
    public string? RmHint { get; private set; }
    public string? Note { get; private set; }

    public Guid? ResolvedSubdivisionId { get; private set; }
    public ResolvedSubdivision? ResolvedSubdivision { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }

    public List<UnknownSubdivisionObservation> Observations { get; private set; } = [];

    private UnknownSubdivisionCluster()
    {
    }

    public static UnknownSubdivisionCluster Create(
        string labelRaw,
        string? layerHint = null,
        string? rmHint = null,
        string? note = null,
        string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(labelRaw))
            throw new ArgumentException("Subdivision label is required.", nameof(labelRaw));

        return new UnknownSubdivisionCluster
        {
            LabelRaw = labelRaw.Trim(),
            LabelNorm = TextNorm.NormalizeRequired(labelRaw),
            LayerHint = NormalizeOptional(layerHint),
            RmHint = NormalizeOptional(rmHint),
            Note = NormalizeOptional(note),
            CreatedBy = NormalizeOptional(createdBy)
        };
    }

    public void UpdateHints(string? layerHint, string? rmHint)
    {
        LayerHint = NormalizeOptional(layerHint);
        RmHint = NormalizeOptional(rmHint);
    }

    public void SetNote(string? note)
    {
        Note = NormalizeOptional(note);
    }

    public UnknownSubdivisionObservation AddObservation(Guid observationId, string? note = null)
    {
        if (Observations.Any(x => x.ObservationId == observationId))
            throw new InvalidOperationException("Observation already belongs to this subdivision cluster.");

        var link = UnknownSubdivisionObservation.Create(Id, observationId, note);
        Observations.Add(link);
        return link;
    }

    public void Resolve(Guid resolvedSubdivisionId)
    {
        if (resolvedSubdivisionId == Guid.Empty)
            throw new ArgumentException("Resolved subdivision id is required.", nameof(resolvedSubdivisionId));

        ResolvedSubdivisionId = resolvedSubdivisionId;
        ArchivedAtUtc ??= DateTime.UtcNow;
    }

    public void Reopen()
    {
        ResolvedSubdivisionId = null;
        ArchivedAtUtc = null;
    }

    public void Archive()
    {
        ArchivedAtUtc ??= DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
