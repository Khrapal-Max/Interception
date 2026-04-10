//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Dtos;

namespace Interception.UI.Application.Interceptions.Abstractions;

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
    /// Повертає read-model деталізацію повідомлення.
    /// </summary>
    Task<InterceptionDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
