//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain;

/// <summary>
/// Прив'язка raw-учасника спостереження до unknown-кластера.
/// Сам raw-учасник не змінюється; змінюється лише шар інтерпретації.
/// </summary>
public sealed class UnknownClusterMember
{
    /// <summary>
    /// Ідентифікатор запису прив'язки.
    /// </summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Ідентифікатор кластера.
    /// </summary>
    public Guid UnknownClusterId { get; private set; }

    /// <summary>
    /// Кластер unknown-особи.
    /// </summary>
    public UnknownCluster UnknownCluster { get; private set; } = default!;

    /// <summary>
    /// Ідентифікатор raw-учасника спостереження.
    /// </summary>
    public Guid ObservationParticipantId { get; private set; }

    /// <summary>
    /// Raw-учасник, який входить до кластера.
    /// </summary>
    public ObservationParticipant ObservationParticipant { get; private set; } = default!;

    /// <summary>
    /// Довіра до прив'язки (0..1), якщо оцінюється.
    /// </summary>
    public decimal? Confidence { get; private set; }

    /// <summary>
    /// Пояснення, чому учасника додано до кластера.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// Дата додавання до кластера.
    /// </summary>
    public DateTime AddedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Хто додав у кластер.
    /// </summary>
    public string? AddedBy { get; private set; }

    private UnknownClusterMember()
    {
    }
}
