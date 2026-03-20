//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain;

/// <summary>
/// Link between an analytical unknown-person cluster and a concrete participant snapshot.
/// </summary>
public sealed class UnknownClusterMember
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid UnknownClusterId { get; private set; }
    public UnknownCluster UnknownCluster { get; private set; } = default!;

    public Guid ObservationParticipantId { get; private set; }
    public ObservationParticipant ObservationParticipant { get; private set; } = default!;

    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private UnknownClusterMember()
    {
    }

    internal static UnknownClusterMember Create(Guid unknownClusterId, Guid observationParticipantId, string? note = null)
    {
        if (unknownClusterId == Guid.Empty)
            throw new ArgumentException("Cluster id is required.", nameof(unknownClusterId));
        if (observationParticipantId == Guid.Empty)
            throw new ArgumentException("Observation participant id is required.", nameof(observationParticipantId));

        return new UnknownClusterMember
        {
            UnknownClusterId = unknownClusterId,
            ObservationParticipantId = observationParticipantId,
            Note = NormalizeOptional(note)
        };
    }

    public void MoveToCluster(Guid unknownClusterId)
    {
        if (unknownClusterId == Guid.Empty)
            throw new ArgumentException("Cluster id is required.", nameof(unknownClusterId));

        UnknownClusterId = unknownClusterId;
    }

    public void SetNote(string? note)
    {
        Note = NormalizeOptional(note);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
