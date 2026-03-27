//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;

namespace Interception.UI.Application.Interceptions.Abstractions.Interceptions;

/// <summary>
/// Read-side сервіс реєстру перехоплень та деталей одного повідомлення.
/// </summary>
public interface IInterceptionQueryService
{
    /// <summary>
    /// Повертає сторінку реєстру перехоплень з overlay для НВ.
    /// </summary>
    Task<PagedResult<InterceptionListItemDto>> GetPagedAsync(
        InterceptionFilter filter,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Повертає доменне повідомлення з учасниками і мітками.
    /// </summary>
    Task<InterceptionMessage?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
