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
    public Guid Id { get; private set; } = Guid.NewGuid();

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
}
