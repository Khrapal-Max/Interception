//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain;

/// <summary>
/// Тимчасова аналітична сутність для групування невизначених осіб.
/// Не змінює raw-історію, а лише дає стабільний вузол для читання графа/реєстру.
/// </summary>
public sealed class UnknownCluster
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string? DisplayName { get; private set; }
    public string Status { get; private set; } = default!;
    public Guid? ResolvedActorId { get; private set; }
    public ResolvedActor? ResolvedActor { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }
    public List<UnknownClusterMember> Members { get; private set; } = [];

    private UnknownCluster()
    {
    }

    public static UnknownCluster Create(Guid id, string code, string? displayName, DateTime createdAtUtc, string? createdBy)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Cluster id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Cluster code is required.", nameof(code));

        return new UnknownCluster
        {
            Id = id,
            Code = code.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            Status = "open",
            CreatedAtUtc = createdAtUtc,
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy.Trim()
        };
    }

    public void ResolveToActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        ResolvedActorId = actorId;
        Status = "resolved";
    }

    public void MarkMerged()
    {
        Status = "merged";
    }

    /// <summary>
    /// Переводить кластер в архівний стан.
    /// </summary>
    public void MarkArchived()
    {
        Status = "archived";
    }
}