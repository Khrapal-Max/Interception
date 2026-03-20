//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Catalogs.Abstractions;
using Interception.UI.Application.Catalogs.Dtos;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Catalogs.Services;

/// <summary>
/// EF-backed application service for confirmed subdivision catalog.
/// </summary>
public sealed class ResolvedSubdivisionCatalogService(IDbContextFactory<AppDbContext> dbFactory) : IResolvedSubdivisionCatalogService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<ResolvedSubdivisionCatalogPageDto> SearchAsync(ResolvedSubdivisionCatalogFilterDto filter, CancellationToken ct)
    {
        var skip = Math.Max(0, filter.Skip);
        var take = Math.Clamp(filter.Take, 1, 200);
        var queryText = (filter.Query ?? string.Empty).Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.ResolvedSubdivisions.AsNoTracking().AsQueryable();

        if (!filter.IncludeArchived)
            query = query.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(queryText))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, $"%{queryText}%") ||
                (x.LayerHint != null && EF.Functions.ILike(x.LayerHint, $"%{queryText}%")) ||
                (x.RmHint != null && EF.Functions.ILike(x.RmHint, $"%{queryText}%")) ||
                (x.Note != null && EF.Functions.ILike(x.Note, $"%{queryText}%")));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip(skip)
            .Take(take)
            .Select(x => new ResolvedSubdivisionCatalogItemDto(
                x.Id,
                x.Name,
                x.LayerHint,
                x.RmHint,
                x.IsActive,
                x.ResolvedClusters.Count,
                x.CreatedAtUtc))
            .ToListAsync(ct);

        return new ResolvedSubdivisionCatalogPageDto(items, totalCount);
    }

    public async Task<ResolvedSubdivisionCatalogDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.ResolvedSubdivisions
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ResolvedSubdivisionCatalogDetailsDto(
                x.Id,
                x.Name,
                x.LayerHint,
                x.RmHint,
                x.Note,
                x.IsActive,
                x.ResolvedClusters.Count,
                x.CreatedAtUtc,
                x.CreatedBy))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CatalogSaveResultDto> CreateAsync(ResolvedSubdivisionCatalogUpsertDto request, string? createdBy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var normalizedName = TextNorm.NormalizeRequired(request.Name);
        var duplicate = await db.ResolvedSubdivisions
            .AnyAsync(x => x.NameNorm == normalizedName, ct);

        if (duplicate)
            throw new InvalidOperationException($"Subdivision '{request.Name}' already exists.");

        var entity = ResolvedSubdivision.Create(
            request.Name,
            request.LayerHint,
            request.RmHint,
            request.Note,
            createdBy);

        db.ResolvedSubdivisions.Add(entity);
        await db.SaveChangesAsync(ct);

        return new CatalogSaveResultDto(entity.Id, true);
    }

    public async Task<CatalogSaveResultDto> UpdateAsync(Guid id, ResolvedSubdivisionCatalogUpsertDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.ResolvedSubdivisions
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Resolved subdivision was not found.");

        var normalizedName = TextNorm.NormalizeRequired(request.Name);
        var duplicate = await db.ResolvedSubdivisions
            .AnyAsync(x => x.Id != id && x.NameNorm == normalizedName, ct);

        if (duplicate)
            throw new InvalidOperationException($"Subdivision '{request.Name}' already exists.");

        entity.Rename(request.Name);
        entity.UpdateHints(request.LayerHint, request.RmHint);
        entity.SetNote(request.Note);

        await db.SaveChangesAsync(ct);
        return new CatalogSaveResultDto(entity.Id, false);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.ResolvedSubdivisions
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Resolved subdivision was not found.");

        entity.Archive();
        await db.SaveChangesAsync(ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.ResolvedSubdivisions
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Resolved subdivision was not found.");

        entity.Restore();
        await db.SaveChangesAsync(ct);
    }
}
