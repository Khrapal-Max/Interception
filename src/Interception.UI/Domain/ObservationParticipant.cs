//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Raw participant snapshot inside a single observation.
/// Can later be уточнений by label/role, but we still keep whether the record started as unknown.
/// </summary>
public sealed class ObservationParticipant
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ObservationId { get; private set; }
    public Observation Observation { get; private set; } = default!;

    /// <summary>
    /// Поточна raw-назва/позначення учасника в межах observation.
    /// </summary>
    public string? LabelRaw { get; private set; }

    /// <summary>
    /// Нормалізована назва для пошуку/порівняння.
    /// </summary>
    public string? LabelNorm { get; private set; }

    /// <summary>
    /// Поточний стан учасника: чи він зараз вважається unknown.
    /// </summary>
    public bool IsUnknown { get; private set; }

    /// <summary>
    /// Ознака, що запис був створений як unknown (НВ), навіть якщо пізніше його уточнили.
    /// Це окремий технічний флаг для аналітики і сценаріїв редагування.
    /// </summary>
    public bool StartedAsUnknown { get; private set; }

    /// <summary>
    /// Поточна raw-роль учасника в межах observation.
    /// </summary>
    public string? RoleRaw { get; private set; }

    /// <summary>
    /// Порядок учасника у вихідному записі observation.
    /// </summary>
    public int Ordinal { get; private set; }

    private ObservationParticipant()
    {
    }

    internal ObservationParticipant(Guid observationId, string? labelRaw, bool isUnknown, string? roleRaw, int ordinal)
    {
        if (ordinal <= 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal), "Ordinal must be >= 1.");

        ObservationId = observationId;
        Ordinal = ordinal;

        ApplySnapshot(labelRaw, isUnknown, roleRaw, initializeStartedFlag: true);
    }

    /// <summary>
    /// Оновлює raw-дані учасника всередині observation.
    /// Флаг <see cref="StartedAsUnknown"/> зберігає історію старту запису і ніколи не скидається назад у false.
    /// </summary>
    public void UpdateSnapshot(string? labelRaw, bool isUnknown, string? roleRaw)
    {
        ApplySnapshot(labelRaw, isUnknown, roleRaw, initializeStartedFlag: false);
    }

    /// <summary>
    /// Оновлює тільки raw-роль учасника.
    /// </summary>
    public void UpdateRole(string? roleRaw)
    {
        RoleRaw = NormalizeOptional(roleRaw);
    }

    private void ApplySnapshot(string? labelRaw, bool isUnknown, string? roleRaw, bool initializeStartedFlag)
    {
        LabelRaw = NormalizeOptional(labelRaw);
        LabelNorm = TextNorm.Normalize(LabelRaw);
        RoleRaw = NormalizeOptional(roleRaw);

        var effectiveUnknown = isUnknown || LabelNorm is null;
        IsUnknown = effectiveUnknown;

        if (initializeStartedFlag)
        {
            StartedAsUnknown = effectiveUnknown;
            return;
        }

        if (effectiveUnknown)
            StartedAsUnknown = true;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
