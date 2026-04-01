//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain.Entities;

namespace Interception.Application.Registry.Abstractions;

/// <summary>
/// Контракт сервісу довідника дій.
///
/// Видалення навмисно відсутнє — InterceptionAction є довідниковою сутністю,
/// на яку посилаються всі InterceptionMessage. Видалення порушить цілісність даних.
/// </summary>
public interface IInterceptionActionService
{
    /// <summary>Повертає всі дії відсортовані за назвою.</summary>
    Task<IReadOnlyList<InterceptionAction>> GetAllAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Створює нову дію.
    /// Кидає <see cref="InvalidOperationException"/> якщо назва вже існує.
    /// </summary>
    Task<InterceptionAction> CreateAsync(
        string name,
        string description,
        CancellationToken ct = default);

    /// <summary>
    /// Оновлює назву та опис існуючої дії.
    /// Кидає <see cref="InvalidOperationException"/> якщо запис не знайдено
    /// або нова назва вже зайнята іншою дією.
    /// </summary>
    Task<InterceptionAction> UpdateAsync(
        Guid id,
        string name,
        string description,
        CancellationToken ct = default);

    /// <summary>
    /// Масове заповнення довідника зі списку назв (наприклад, з аркуша "ДІЇ" Excel).
    /// Пропускає вже існуючі. Повертає кількість доданих.
    /// </summary>
    Task<int> SeedFromListAsync(
        IEnumerable<string> names,
        CancellationToken ct = default);
}
