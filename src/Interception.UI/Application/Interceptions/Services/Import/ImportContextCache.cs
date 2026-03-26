//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;

namespace Interception.UI.Application.Interceptions.Services.Import;

/// <summary>
/// Короткочасний кеш, що живе тільки під час одного сеансу імпорту.
///
/// Зберігає:
///   1. Довідник InterceptionAction (ключ — назва в нижньому регістрі)
///   2. Останніх відомих учасників (ім'я → роль) для автопідстановки ролі
///
/// Примітка: автопідстановка Frequency і VectorSignal прибрана навмисно.
/// Порожнє поле в рядку Excel означає відсутність інформації, а не
/// "взяти з попереднього рядка". Кожен рядок імпортується незалежно.
/// </summary>
public sealed class ImportContextCache(IEnumerable<InterceptionAction> actions)
{
    private readonly Dictionary<string, InterceptionAction> _actions =
        actions.ToDictionary(
            a => a.Name.Trim().ToLowerInvariant(),
            a => a);

    // Автопідстановка ролі учасника: ім'я (lower) → остання відома роль
    private readonly Dictionary<string, string> _participantRoles =
        new(StringComparer.OrdinalIgnoreCase);

    // -------------------------------------------------------------------------
    // InterceptionAction
    // -------------------------------------------------------------------------

    public InterceptionAction? FindAction(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        _actions.TryGetValue(name.Trim().ToLowerInvariant(), out var action);
        return action;
    }

    // -------------------------------------------------------------------------
    // Автопідстановка ролі учасника
    // -------------------------------------------------------------------------

    /// <summary>
    /// Повертає роль учасника:
    ///   — якщо в рядку є роль → зберігає в кеші та повертає її
    ///   — інакше повертає останню відому роль для цього імені
    ///   — якщо учасник невідомий (name == null) → null
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
}
