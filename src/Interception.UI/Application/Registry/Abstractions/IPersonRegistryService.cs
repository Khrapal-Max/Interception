//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Registry.Dtos;

namespace Interception.UI.Application.Registry.Abstractions;

/// <summary>
/// Контракт реєстру осіб.
/// </summary>
public interface IPersonRegistryService
{
    /// <summary>
    /// Повертає всіх не-НВ осіб у системі:
    /// confirmed, observed-known та partial.
    /// </summary>
    Task<IReadOnlyList<PersonRegistryItemDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Оновлює confirmed person або створює canonical person
    /// при першій правці observed-known / partial особи.
    /// </summary>
    Task<PersonRegistryItemDto> UpdateAsync(Guid id, PersonRegistryUpdateDto dto, CancellationToken ct = default);
}
