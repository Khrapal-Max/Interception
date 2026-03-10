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
    /// <summary>
    /// Ідентифікатор кластера.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Стабільний код кластера, напр. UNK-000123.
    /// </summary>
    public string Code { get; private set; } = default!;

    /// <summary>
    /// Людинозрозуміла назва кластера.
    /// </summary>
    public string? DisplayName { get; private set; }

    /// <summary>
    /// Поточний статус кластера: open / resolved / merged / archived.
    /// </summary>
    public string Status { get; private set; } = default!;

    /// <summary>
    /// Якщо кластер вже резолвлено в канонічного актора — тут його Id.
    /// </summary>
    public Guid? ResolvedActorId { get; private set; }

    /// <summary>
    /// Канонічний актор, у якого зараз відображається кластер.
    /// </summary>
    public ResolvedActor? ResolvedActor { get; private set; }

    /// <summary>
    /// Дата створення кластера.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Хто створив кластер.
    /// </summary>
    public string? CreatedBy { get; private set; }

    /// <summary>
    /// Учасники raw-спостережень, які входять у цей кластер.
    /// </summary>
    public List<UnknownClusterMember> Members { get; private set; } = [];

    private UnknownCluster()
    {
    }

    /// <summary>
    /// Створює новий unknown-кластер.
    /// </summary>
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

    /// <summary>
    /// Позначає кластер як резолвлений у встановлену особу.
    /// </summary>
    public void ResolveToActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Actor id is required.", nameof(actorId));

        ResolvedActorId = actorId;
        Status = "resolved";
    }
}
