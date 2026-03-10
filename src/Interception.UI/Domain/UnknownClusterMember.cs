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

    /// <summary>
    /// Створює нову прив'язку учасника до unknown-кластера.
    /// </summary>
    public static UnknownClusterMember Create(
        Guid id,
        Guid unknownClusterId,
        Guid observationParticipantId,
        string? reason,
        DateTime addedAtUtc,
        string? addedBy,
        decimal? confidence = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Member id is required.", nameof(id));
        if (unknownClusterId == Guid.Empty)
            throw new ArgumentException("Unknown cluster id is required.", nameof(unknownClusterId));
        if (observationParticipantId == Guid.Empty)
            throw new ArgumentException("Observation participant id is required.", nameof(observationParticipantId));

        return new UnknownClusterMember
        {
            Id = id,
            UnknownClusterId = unknownClusterId,
            ObservationParticipantId = observationParticipantId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            AddedAtUtc = addedAtUtc,
            AddedBy = string.IsNullOrWhiteSpace(addedBy) ? null : addedBy.Trim(),
            Confidence = confidence
        };
    }

    /// <summary>
    /// Переносить прив'язку в інший кластер та оновлює супровідні дані.
    /// </summary>
    public void MoveToCluster(
        Guid unknownClusterId,
        string? reason = null,
        DateTime? addedAtUtc = null,
        string? addedBy = null,
        decimal? confidence = null)
    {
        if (unknownClusterId == Guid.Empty)
            throw new ArgumentException("Cluster id is required.", nameof(unknownClusterId));

        UnknownClusterId = unknownClusterId;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (addedAtUtc.HasValue)
            AddedAtUtc = addedAtUtc.Value;

        if (addedBy is not null)
            AddedBy = string.IsNullOrWhiteSpace(addedBy) ? null : addedBy.Trim();

        if (confidence.HasValue)
            Confidence = confidence;
    }
}