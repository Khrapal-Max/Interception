//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Сервіс ручного ведення контуру структурного керування.
/// Працює як з канонічними особами, так і з поодинокими підтвердженими особами.
/// </summary>
public interface IPersonDirectiveRelationService
{
    Task<IReadOnlyList<PersonDirectiveRelationListItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PersonDirectiveRelationOptionDto>> GetIdentityOptionsAsync(CancellationToken ct = default);
    Task SaveAsync(PersonDirectiveRelationSaveDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
