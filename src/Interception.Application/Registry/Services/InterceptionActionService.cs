//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Registry.Abstractions;
using Interception.Domain.Entities;
using Interception.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Application.Registry.Services;

/// <summary>
/// Сервіс довідника дій (InterceptionAction).
///
/// Правила:
///   — Видалення відсутнє — дії є довідниковими даними на які посилаються повідомлення.
///   — Назва дії унікальна (case-sensitive, як в БД).
///   — Домен відповідає за валідацію через Create() / Update().
/// </summary>
public sealed class InterceptionActionService(
    IDbContextFactory<AppDbContext> dbFactory) : IInterceptionActionService
{
    // -------------------------------------------------------------------------
    // Read
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<InterceptionAction>> GetAllAsync(
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        return await db.InterceptionActions
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(ct);
    }

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    public async Task<InterceptionAction> CreateAsync(
        string name,
        string description,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var normalized = name?.Trim() ?? string.Empty;

        var exists = await db.InterceptionActions
            .AnyAsync(a => a.Name == normalized, ct);

        if (exists)
            throw new InvalidOperationException(
                $"Дія '{normalized}' вже існує в довіднику.");

        // FIX: замість рефлексії — публічний фабричний метод на домені
        var action = InterceptionAction.Create(normalized, description ?? string.Empty);

        db.InterceptionActions.Add(action);
        await db.SaveChangesAsync(ct);

        return action;
    }

    // -------------------------------------------------------------------------
    // Update
    // -------------------------------------------------------------------------

    public async Task<InterceptionAction> UpdateAsync(
        Guid id,
        string name,
        string description,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var action = await db.InterceptionActions
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new InvalidOperationException($"Дію з Id '{id}' не знайдено.");

        var normalized = name?.Trim() ?? string.Empty;

        // Перевіряємо унікальність назви серед інших дій
        var nameConflict = await db.InterceptionActions
            .AnyAsync(a => a.Name == normalized && a.Id != id, ct);

        if (nameConflict)
            throw new InvalidOperationException(
                $"Дія '{normalized}' вже існує в довіднику.");

        action.Update(normalized, description ?? string.Empty);
        await db.SaveChangesAsync(ct);

        return action;
    }

    // -------------------------------------------------------------------------
    // Seed
    // -------------------------------------------------------------------------

    public async Task<int> SeedFromListAsync(
        IEnumerable<string> names,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var existing = await db.InterceptionActions
            .Select(a => a.Name)
            .ToHashSetAsync(ct);

        var toAdd = names
            .Select(n => n.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n) && !existing.Contains(n))
            .Distinct()
            .Select(n => InterceptionAction.Create(n, string.Empty))
            .ToList();

        if (toAdd.Count == 0) return 0;

        db.InterceptionActions.AddRange(toAdd);
        await db.SaveChangesAsync(ct);

        return toAdd.Count;
    }
}
