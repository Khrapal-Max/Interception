//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Registry.Dtos;

namespace Interception.UI.Application.Registry.Abstractions;

/// <summary>
/// Контракт довідника ролей.
/// </summary>
public interface IParticipantRoleService
{
    Task<IReadOnlyList<ParticipantRoleListItemDto>> GetAllAsync(CancellationToken ct = default);

    Task<ParticipantRoleListItemDto> CreateAsync(string name, string description, CancellationToken ct = default);

    Task<ParticipantRoleListItemDto> UpdateAsync(Guid id, string name, string description, CancellationToken ct = default);

    Task<int> SeedFromListAsync(IEnumerable<string> names, CancellationToken ct = default);
}
