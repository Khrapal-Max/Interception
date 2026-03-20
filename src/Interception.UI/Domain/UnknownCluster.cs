//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Analytical cluster of unknown participants that are believed to represent the same actor.
/// </summary>
public sealed class UnknownCluster
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Title { get; private set; } = default!;
    public string TitleNorm { get; private set; } = default!;
    public string? Note { get; private set; }

    public Guid? ResolvedActorId { get; private set; }
    public ResolvedActor? ResolvedActor { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }

    public List<UnknownClusterMember> Members { get; private set; } = [];

    private UnknownCluster()
    {
    }

    public static UnknownCluster Create(string title, string? note = null, string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Cluster title is required.", nameof(title));

        return new UnknownCluster
        {
            Title = title.Trim(),
            TitleNorm = TextNorm.NormalizeRequired(title),
            Note = NormalizeOptional(note),
            CreatedBy = NormalizeOptional(createdBy)
        };
    }

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Cluster title is required.", nameof(title));

        Title = title.Trim();
        TitleNorm = TextNorm.NormalizeRequired(title);
    }

    public void SetNote(string? note)
    {
        Note = NormalizeOptional(note);
    }

    public UnknownClusterMember AddMember(Guid observationParticipantId, string? note = null)
    {
        if (Members.Any(x => x.ObservationParticipantId == observationParticipantId))
            throw new InvalidOperationException("Participant already belongs to this cluster.");

        var member = UnknownClusterMember.Create(Id, observationParticipantId, note);
        Members.Add(member);
        return member;
    }

    public void ResolveToActor(Guid resolvedActorId)
    {
        if (resolvedActorId == Guid.Empty)
            throw new ArgumentException("Resolved actor id is required.", nameof(resolvedActorId));

        ResolvedActorId = resolvedActorId;
        ArchivedAtUtc ??= DateTime.UtcNow;
    }

    public void Reopen()
    {
        ResolvedActorId = null;
        ArchivedAtUtc = null;
    }

    public void Archive()
    {
        ArchivedAtUtc ??= DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
