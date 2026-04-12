//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Registry.Dtos;

namespace Interception.UI.Application.Registry.Abstractions;

/// <summary>
/// Контур ручного ведення фактів структурного керування.
/// </summary>
public interface IPersonDirectiveRelationService
{
    Task<IReadOnlyList<PersonDirectiveRelationListItemDto>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PersonDirectiveRelationOptionDto>> GetIdentityOptionsAsync(
        IReadOnlyList<string>? participantNames = null,
        CancellationToken ct = default);

    Task SaveAsync(PersonDirectiveRelationSaveDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
