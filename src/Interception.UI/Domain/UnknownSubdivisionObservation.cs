//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain;

/// <summary>
/// Link between unknown subdivision analytical cluster and the raw observation.
/// </summary>
public sealed class UnknownSubdivisionObservation
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid UnknownSubdivisionClusterId { get; private set; }
    public UnknownSubdivisionCluster UnknownSubdivisionCluster { get; private set; } = default!;

    public Guid ObservationId { get; private set; }
    public Observation Observation { get; private set; } = default!;

    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private UnknownSubdivisionObservation()
    {
    }

    internal static UnknownSubdivisionObservation Create(Guid unknownSubdivisionClusterId, Guid observationId, string? note = null)
    {
        if (unknownSubdivisionClusterId == Guid.Empty)
            throw new ArgumentException("Subdivision cluster id is required.", nameof(unknownSubdivisionClusterId));
        if (observationId == Guid.Empty)
            throw new ArgumentException("Observation id is required.", nameof(observationId));

        return new UnknownSubdivisionObservation
        {
            UnknownSubdivisionClusterId = unknownSubdivisionClusterId,
            ObservationId = observationId,
            Note = NormalizeOptional(note)
        };
    }

    public void SetNote(string? note)
    {
        Note = NormalizeOptional(note);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
