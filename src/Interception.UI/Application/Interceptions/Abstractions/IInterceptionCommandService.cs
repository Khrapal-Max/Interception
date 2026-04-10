//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Dtos;

namespace Interception.UI.Application.Interceptions.Abstractions;

/// <summary>
/// Write-side сервіс для створення, оновлення та видалення повідомлень перехоплення.
/// </summary>
public interface IInterceptionCommandService
{
    /// <summary>
    /// Створює нове повідомлення перехоплення.
    /// </summary>
    Task<Guid> CreateAsync(
        InterceptionFormDto form,
        string operatorName,
        CancellationToken ct = default);

    /// <summary>
    /// Оновлює існуюче повідомлення перехоплення.
    /// </summary>
    Task UpdateAsync(Guid id, InterceptionFormDto form, CancellationToken ct = default);

    /// <summary>
    /// Видаляє повідомлення перехоплення.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
