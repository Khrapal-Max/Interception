//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Interceptions.Dtos;
using Interception.Domain;

namespace Interception.Application.Interceptions.Abstractions;

/// <summary>
/// Read-side сервіс реєстру перехоплень та деталей одного повідомлення.
/// </summary>
public interface IInterceptionQueryService
{
    /// <summary>
    /// Повертає сторінку реєстру перехоплень з overlay для НВ.
    /// </summary>
    Task<PagedResultDto<InterceptionListItemDto>> GetPagedAsync(
        InterceptionFilterDto filter,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Повертає доменне повідомлення з учасниками і мітками.
    /// </summary>
    Task<InterceptionMessage?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
