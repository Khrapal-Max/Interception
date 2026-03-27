//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Dtos;

namespace Interception.UI.Application.Interceptions.Abstractions.Registry;

/// <summary>
/// Контракт реєстру осіб.
/// </summary>
public interface IPersonRegistryService
{
    /// <summary>
    /// Повертає всіх відомих осіб у системі.
    /// </summary>
    Task<IReadOnlyList<PersonRegistryItemDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Оновлює канонічну особу або створює її для відомої особи зі спостережень.
    /// </summary>
    Task<PersonRegistryItemDto> UpdateAsync(Guid id, PersonRegistryUpdateDto dto, CancellationToken ct = default);
}
