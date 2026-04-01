//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain.Extensions;

namespace Interception.Domain.Entities;

public class InterceptionMessageParticipant
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid InterceptionMessageId { get; private set; }
    public InterceptionMessage InterceptionMessage { get; private set; } = default!;

    /// <summary>
    /// Поточна назва/позначення учасника.
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>
    /// Поточний стан учасника: чи він зараз вважається unknown.
    /// </summary>
    public bool IsUnknown { get; private set; }

    /// <summary>
    /// Поточна роль учасника в межах observation.
    /// </summary>
    public string? Role { get; private set; }

    /// <summary>
    /// Порядок учасника у вихідному записі interception message.
    /// </summary>
    public int Ordinal { get; private set; }

    public InterceptionMessageParticipant(Guid interceptionMessageId, string? name, bool isUnknown, string? role, int ordinal)
    {
        if (ordinal <= 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal), "Ordinal must be >= 1.");

        InterceptionMessageId = interceptionMessageId;
        Ordinal = ordinal;

        ApplySnapshot(name, role, isUnknown);
    }

    /// <summary>
    /// Оновлює raw-дані учасника всередині interception message.
    /// </summary>
    public void UpdateSnapshot(string? name, string? role, bool isUnknown)
    {
        ApplySnapshot(name, role, isUnknown);
    }

    public void ResolveAsKnown(string name, string? role = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Known participant name is required.", nameof(name));

        ApplySnapshot(name, role, isUnknown: false);
    }

    /// <summary>
    /// Оновлює тільки роль учасника.
    /// </summary>
    public void UpdateRole(string? role)
    {
        Role = NormalizeOptional(role);
    }

    private void ApplySnapshot(string? name, string? role, bool isUnknown)
    {
        Name = NormalizeOptional(name);
        Role = NormalizeOptional(role);

        var effectiveUnknown = isUnknown || Name is null;
        IsUnknown = effectiveUnknown;
    }

    private static string? NormalizeOptional(string? value)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(value);
}
