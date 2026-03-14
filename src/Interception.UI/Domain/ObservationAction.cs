//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Довідниковий тип дії observation.
/// Дає змогу жити не тільки на вільному raw-тексті, а й на контрольованому переліку дій з описом ролей.
/// </summary>
public sealed class ObservationAction
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Офіційна назва дії для UI та реєстрів.
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Нормалізована назва дії для пошуку та зіставлення.
    /// </summary>
    public string NameNorm { get; private set; } = default!;

    /// <summary>
    /// Людська назва ролі ініціатора для цього типу дії.
    /// </summary>
    public string? InitiatorRoleName { get; private set; }

    /// <summary>
    /// Людська назва ролі відповідача/цілі для цього типу дії.
    /// </summary>
    public string? ResponderRoleName { get; private set; }

    /// <summary>
    /// Короткий опис або підказка для оператора/аналітика.
    /// </summary>
    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }

    public List<Observation> Observations { get; private set; } = [];

    private ObservationAction()
    {
    }

    public static ObservationAction Create(
        string name,
        string? initiatorRoleName = null,
        string? responderRoleName = null,
        string? description = null,
        string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Action name is required.", nameof(name));

        var normalizedName = TextNorm.NormalizeRequired(name);

        return new ObservationAction
        {
            Name = name.Trim(),
            NameNorm = normalizedName,
            InitiatorRoleName = NormalizeOptional(initiatorRoleName),
            ResponderRoleName = NormalizeOptional(responderRoleName),
            Description = NormalizeOptional(description),
            CreatedBy = NormalizeOptional(createdBy),
            IsActive = true
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Action name is required.", nameof(name));

        Name = name.Trim();
        NameNorm = TextNorm.NormalizeRequired(name);
    }

    public void ConfigureRoles(string? initiatorRoleName, string? responderRoleName)
    {
        InitiatorRoleName = NormalizeOptional(initiatorRoleName);
        ResponderRoleName = NormalizeOptional(responderRoleName);
    }

    public void SetDescription(string? description)
    {
        Description = NormalizeOptional(description);
    }

    public void Archive()
    {
        IsActive = false;
    }

    public void Restore()
    {
        IsActive = true;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
