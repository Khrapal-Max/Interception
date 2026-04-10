//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;

namespace Interception.UI.Application.Registry.Abstractions;

/// <summary>
/// Контракт довідника ролей.
/// </summary>
public interface IParticipantRoleService
{
    Task<IReadOnlyList<ParticipantRole>> GetAllAsync(CancellationToken ct = default);

    Task<ParticipantRole> CreateAsync(string name, string description, CancellationToken ct = default);

    Task<ParticipantRole> UpdateAsync(Guid id, string name, string description, CancellationToken ct = default);

    Task<int> SeedFromListAsync(IEnumerable<string> names, CancellationToken ct = default);
}
