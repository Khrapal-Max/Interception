//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;

namespace Interception.UI.Application.Interceptions.Import;

/// <summary>
/// Короткочасний кеш, що живе тільки під час одного сеансу імпорту.
///
/// Зберігає:
///   1. Довідник InterceptionAction (ключ — назва в нижньому регістрі)
///   2. Останню відому Frequency і VectorSignal для автопідстановки
///   3. Останніх відомих учасників (ім'я → роль) для автопідстановки ролі
/// </summary>
internal sealed class ImportContextCache(IEnumerable<InterceptionAction> actions)
{
    // --- InterceptionAction lookup ---
    private readonly Dictionary<string, InterceptionAction> _actions = actions.ToDictionary(
            a => a.Name.Trim().ToLowerInvariant(),
            a => a);

    // --- Автопідстановка сигнальних полів ---
    private string? _lastFrequency;
    private string? _lastVectorSignal;

    // --- Автопідстановка ролі учасника: ім'я (lower) → остання відома роль ---
    private readonly Dictionary<string, string> _participantRoles = new(StringComparer.OrdinalIgnoreCase);

    // -------------------------------------------------------------------------
    // InterceptionAction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Шукає дію за назвою (case-insensitive).
    /// Повертає null якщо не знайдено.
    /// </summary>
    public InterceptionAction? FindAction(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        _actions.TryGetValue(name.Trim().ToLowerInvariant(), out var action);
        return action;
    }

    // -------------------------------------------------------------------------
    // Автопідстановка сигнальних полів
    // -------------------------------------------------------------------------

    /// <summary>
    /// Повертає частоту: якщо в рядку є значення — оновлює кеш і повертає його,
    /// інакше повертає останнє відоме.
    /// </summary>
    public string? ResolveFrequency(string? rowValue)
    {
        if (!string.IsNullOrWhiteSpace(rowValue))
            _lastFrequency = rowValue.Trim();
        return _lastFrequency;
    }

    /// <summary>
    /// Аналогічно для вектора сигналу.
    /// </summary>
    public string? ResolveVectorSignal(string? rowValue)
    {
        if (!string.IsNullOrWhiteSpace(rowValue))
            _lastVectorSignal = rowValue.Trim();
        return _lastVectorSignal;
    }

    // -------------------------------------------------------------------------
    // Автопідстановка ролі учасника
    // -------------------------------------------------------------------------

    /// <summary>
    /// Повертає роль учасника:
    ///   — якщо в рядку є роль → зберігає в кеші та повертає її
    ///   — інакше повертає останню відому роль для цього імені
    ///   — якщо учасник невідомий (name == null) → повертає null
    /// </summary>
    public string? ResolveParticipantRole(string? name, string? rowRole)
    {
        if (name is null) return null;

        var key = name.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(rowRole))
        {
            _participantRoles[key] = rowRole.Trim();
            return rowRole.Trim();
        }

        _participantRoles.TryGetValue(key, out var cachedRole);
        return cachedRole;
    }

    /// <summary>
    /// Скидає кеш автопідстановки сигнальних полів.
    /// Використовується якщо оператор явно хоче почати нову сесію.
    /// </summary>
    public void ResetSignalDefaults()
    {
        _lastFrequency   = null;
        _lastVectorSignal = null;
    }
}
