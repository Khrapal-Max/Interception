//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Registry.Services;

/// <summary>
/// Сервіс довідника ролей.
/// </summary>
public sealed class ParticipantRoleService(IDbContextFactory<AppDbContext> dbFactory) : IParticipantRoleService
{
    public async Task<IReadOnlyList<ParticipantRole>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ParticipantRoles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<ParticipantRole> CreateAsync(string name, string description, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var normalized = name?.Trim() ?? string.Empty;
        var exists = await db.ParticipantRoles
            .AnyAsync(x => x.Name.ToUpper() == normalized.ToUpper(), ct);

        if (exists)
            throw new InvalidOperationException($"Роль '{normalized}' вже існує в довіднику.");

        var role = ParticipantRole.Create(normalized, description);
        db.ParticipantRoles.Add(role);
        await db.SaveChangesAsync(ct);
        return role;
    }

    public async Task<ParticipantRole> UpdateAsync(Guid id, string name, string description, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var role = await db.ParticipantRoles.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException($"Роль '{id}' не знайдено.");

        var normalized = name?.Trim() ?? string.Empty;
        var conflict = await db.ParticipantRoles
            .AnyAsync(x => x.Id != id && x.Name.ToUpper() == normalized.ToUpper(), ct);

        if (conflict)
            throw new InvalidOperationException($"Роль '{normalized}' вже існує в довіднику.");

        role.Update(normalized, description);
        await db.SaveChangesAsync(ct);
        return role;
    }

    public async Task<int> SeedFromListAsync(IEnumerable<string> names, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var existing = await db.ParticipantRoles
            .Select(x => x.Name)
            .ToListAsync(ct);
        var existingSet = existing.Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = names
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x) && !existingSet.Contains(x!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => ParticipantRole.Create(x!))
            .ToList();

        if (toAdd.Count == 0)
            return 0;

        db.ParticipantRoles.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
        return toAdd.Count;
    }
}
