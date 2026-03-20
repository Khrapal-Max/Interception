//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Hypotheses.Abstractions;
using Interception.UI.Application.Hypotheses.Dtos;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Hypotheses.Services;

/// <summary>
/// EF-backed application service for analytical subdivision hypotheses.
/// </summary>
public sealed class SubdivisionHypothesisService(IDbContextFactory<AppDbContext> dbFactory) : ISubdivisionHypothesisService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<SubdivisionHypothesisRegistryPageDto> SearchAsync(SubdivisionHypothesisFilterDto filter, CancellationToken ct)
    {
        var skip = Math.Max(0, filter.Skip);
        var take = Math.Clamp(filter.Take, 1, 200);
        var queryText = (filter.Query ?? string.Empty).Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.UnknownSubdivisionClusters
            .AsNoTracking()
            .AsQueryable();

        if (!filter.IncludeArchived)
            query = query.Where(x => x.ArchivedAtUtc == null);

        if (filter.OnlyResolved)
            query = query.Where(x => x.ResolvedSubdivisionId != null);

        if (!string.IsNullOrWhiteSpace(queryText))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.LabelRaw, $"%{queryText}%") ||
                (x.LayerHint != null && EF.Functions.ILike(x.LayerHint, $"%{queryText}%")) ||
                (x.RmHint != null && EF.Functions.ILike(x.RmHint, $"%{queryText}%")) ||
                (x.Note != null && EF.Functions.ILike(x.Note, $"%{queryText}%")) ||
                (x.ResolvedSubdivision != null && EF.Functions.ILike(x.ResolvedSubdivision.Name, $"%{queryText}%")));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.ArchivedAtUtc != null)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(x => new SubdivisionHypothesisRegistryItemDto(
                x.Id,
                x.LabelRaw,
                x.LayerHint,
                x.RmHint,
                x.Note,
                x.Observations.Count,
                x.ResolvedSubdivisionId,
                x.ResolvedSubdivision != null ? x.ResolvedSubdivision.Name : null,
                x.ArchivedAtUtc != null,
                x.CreatedAtUtc,
                x.ArchivedAtUtc,
                x.Observations
                    .Select(o => (DateTime?)o.Observation.ObservedDate)
                    .OrderByDescending(v => v)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return new SubdivisionHypothesisRegistryPageDto(items, totalCount);
    }

    public async Task<SubdivisionHypothesisDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownSubdivisionClusters
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.LabelRaw,
                x.LayerHint,
                x.RmHint,
                x.Note,
                x.ResolvedSubdivisionId,
                ResolvedSubdivisionName = x.ResolvedSubdivision != null ? x.ResolvedSubdivision.Name : null,
                IsArchived = x.ArchivedAtUtc != null,
                x.CreatedAtUtc,
                x.CreatedBy,
                x.ArchivedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (cluster is null)
            return null;

        var observations = await db.UnknownSubdivisionObservations
            .AsNoTracking()
            .Where(x => x.UnknownSubdivisionClusterId == id)
            .OrderByDescending(x => x.Observation.ObservedDate)
            .ThenByDescending(x => x.Observation.CreatedAtUtc)
            .Select(x => new SubdivisionHypothesisObservationDto(
                x.Id,
                x.ObservationId,
                x.Observation.ObservedDate,
                x.Observation.ActionRaw,
                x.Observation.Layer,
                x.Observation.RmRaw,
                x.Observation.LocationRaw,
                x.Observation.DistrictRaw,
                x.Observation.SubdivisionRaw,
                x.Observation.Note,
                x.Note))
            .ToListAsync(ct);

        return new SubdivisionHypothesisDetailsDto(
            cluster.Id,
            cluster.LabelRaw,
            cluster.LayerHint,
            cluster.RmHint,
            cluster.Note,
            cluster.ResolvedSubdivisionId,
            cluster.ResolvedSubdivisionName,
            cluster.IsArchived,
            cluster.CreatedAtUtc,
            cluster.CreatedBy,
            cluster.ArchivedAtUtc,
            observations);
    }

    public async Task<HypothesisSaveResultDto> CreateAsync(SubdivisionHypothesisCreateDto request, string? createdBy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var normalizedLabel = TextNorm.NormalizeRequired(request.LabelRaw);
        var duplicate = await db.UnknownSubdivisionClusters
            .AnyAsync(x => x.ArchivedAtUtc == null && x.LabelNorm == normalizedLabel, ct);

        if (duplicate)
            throw new InvalidOperationException($"Subdivision hypothesis '{request.LabelRaw}' already exists.");

        var cluster = UnknownSubdivisionCluster.Create(
            request.LabelRaw,
            request.LayerHint,
            request.RmHint,
            request.Note,
            createdBy);

        var seedIds = request.SeedObservationIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (seedIds.Count > 0)
        {
            var alreadyLinkedCount = await db.UnknownSubdivisionObservations
                .CountAsync(x => seedIds.Contains(x.ObservationId), ct);

            if (alreadyLinkedCount > 0)
                throw new InvalidOperationException("At least one seed observation already belongs to another subdivision hypothesis.");

            var existingObservationsCount = await db.Observations
                .CountAsync(x => seedIds.Contains(x.Id), ct);

            if (existingObservationsCount != seedIds.Count)
                throw new InvalidOperationException("At least one seed observation was not found.");

            foreach (var observationId in seedIds)
                cluster.AddObservation(observationId);
        }

        db.UnknownSubdivisionClusters.Add(cluster);
        await db.SaveChangesAsync(ct);

        return new HypothesisSaveResultDto(cluster.Id, true);
    }

    public async Task UpdateAsync(Guid id, SubdivisionHypothesisUpdateDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownSubdivisionClusters
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Subdivision hypothesis was not found.");

        var normalizedLabel = TextNorm.NormalizeRequired(request.LabelRaw);
        var duplicate = await db.UnknownSubdivisionClusters
            .AnyAsync(x => x.Id != id && x.ArchivedAtUtc == null && x.LabelNorm == normalizedLabel, ct);

        if (duplicate)
            throw new InvalidOperationException($"Subdivision hypothesis '{request.LabelRaw}' already exists.");

        // Domain currently has no rename method, so recreate the raw label semantics through direct cluster methods already available.
        // We keep update minimal and consistent with current domain API.
        if (!string.Equals(cluster.LabelNorm, normalizedLabel, StringComparison.Ordinal))
            throw new InvalidOperationException("Changing subdivision hypothesis label is not supported by the current domain API.");

        cluster.UpdateHints(request.LayerHint, request.RmHint);
        cluster.SetNote(request.Note);

        await db.SaveChangesAsync(ct);
    }

    public async Task AddObservationAsync(Guid clusterId, Guid observationId, string? note, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownSubdivisionClusters
            .Include(x => x.Observations)
            .FirstOrDefaultAsync(x => x.Id == clusterId, ct)
            ?? throw new InvalidOperationException("Subdivision hypothesis was not found.");

        var observationExists = await db.Observations
            .AnyAsync(x => x.Id == observationId, ct);

        if (!observationExists)
            throw new InvalidOperationException("Observation was not found.");

        var alreadyLinked = await db.UnknownSubdivisionObservations
            .AnyAsync(x => x.ObservationId == observationId, ct);

        if (alreadyLinked)
            throw new InvalidOperationException("Observation already belongs to another subdivision hypothesis.");

        cluster.AddObservation(observationId, note);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveObservationAsync(Guid clusterId, Guid observationId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var link = await db.UnknownSubdivisionObservations
            .FirstOrDefaultAsync(x => x.UnknownSubdivisionClusterId == clusterId && x.ObservationId == observationId, ct)
            ?? throw new InvalidOperationException("Linked observation was not found in this subdivision hypothesis.");

        db.UnknownSubdivisionObservations.Remove(link);
        await db.SaveChangesAsync(ct);
    }

    public async Task MoveObservationAsync(Guid sourceClusterId, Guid observationId, Guid targetClusterId, CancellationToken ct)
    {
        if (sourceClusterId == targetClusterId)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var link = await db.UnknownSubdivisionObservations
            .FirstOrDefaultAsync(x => x.UnknownSubdivisionClusterId == sourceClusterId && x.ObservationId == observationId, ct)
            ?? throw new InvalidOperationException("Linked observation was not found in the source subdivision hypothesis.");

        var targetExists = await db.UnknownSubdivisionClusters
            .AnyAsync(x => x.Id == targetClusterId, ct);

        if (!targetExists)
            throw new InvalidOperationException("Target subdivision hypothesis was not found.");

        var duplicateInTarget = await db.UnknownSubdivisionObservations
            .AnyAsync(x => x.UnknownSubdivisionClusterId == targetClusterId && x.ObservationId == observationId, ct);

        if (duplicateInTarget)
            throw new InvalidOperationException("Target subdivision hypothesis already contains this observation.");

        db.UnknownSubdivisionObservations.Remove(link);
        db.UnknownSubdivisionObservations.Add(UnknownSubdivisionObservation.Create(targetClusterId, observationId, link.Note));
        await db.SaveChangesAsync(ct);
    }

    public async Task MergeAsync(Guid sourceClusterId, Guid targetClusterId, CancellationToken ct)
    {
        if (sourceClusterId == targetClusterId)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var source = await db.UnknownSubdivisionClusters
            .Include(x => x.Observations)
            .FirstOrDefaultAsync(x => x.Id == sourceClusterId, ct)
            ?? throw new InvalidOperationException("Source subdivision hypothesis was not found.");

        var target = await db.UnknownSubdivisionClusters
            .Include(x => x.Observations)
            .FirstOrDefaultAsync(x => x.Id == targetClusterId, ct)
            ?? throw new InvalidOperationException("Target subdivision hypothesis was not found.");

        var targetObservationIds = target.Observations
            .Select(x => x.ObservationId)
            .ToHashSet();

        foreach (var link in source.Observations.ToList())
        {
            if (targetObservationIds.Contains(link.ObservationId))
            {
                db.UnknownSubdivisionObservations.Remove(link);
                continue;
            }

            db.UnknownSubdivisionObservations.Remove(link);
            db.UnknownSubdivisionObservations.Add(UnknownSubdivisionObservation.Create(targetClusterId, link.ObservationId, link.Note));
        }

        source.Archive();
        await db.SaveChangesAsync(ct);
    }

    public async Task ResolveAsync(Guid clusterId, ResolvedSubdivisionUpsertDto request, string? createdBy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownSubdivisionClusters
            .FirstOrDefaultAsync(x => x.Id == clusterId, ct)
            ?? throw new InvalidOperationException("Subdivision hypothesis was not found.");

        var normalizedName = TextNorm.NormalizeRequired(request.Name);
        var subdivision = await db.ResolvedSubdivisions
            .FirstOrDefaultAsync(x => x.IsActive && x.NameNorm == normalizedName, ct);

        if (subdivision is null)
        {
            subdivision = ResolvedSubdivision.Create(request.Name, request.LayerHint, request.RmHint, request.Note, createdBy);
            db.ResolvedSubdivisions.Add(subdivision);
        }
        else
        {
            subdivision.UpdateHints(request.LayerHint, request.RmHint);
            subdivision.SetNote(request.Note);
        }

        cluster.Resolve(subdivision.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReopenAsync(Guid clusterId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownSubdivisionClusters
            .FirstOrDefaultAsync(x => x.Id == clusterId, ct)
            ?? throw new InvalidOperationException("Subdivision hypothesis was not found.");

        cluster.Reopen();
        await db.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid clusterId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownSubdivisionClusters
            .FirstOrDefaultAsync(x => x.Id == clusterId, ct)
            ?? throw new InvalidOperationException("Subdivision hypothesis was not found.");

        cluster.Archive();
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UnknownSubdivisionClusterLookupDto>> SearchOpenClustersAsync(string? query, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 50);
        var queryText = (query ?? string.Empty).Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var search = db.UnknownSubdivisionClusters
            .AsNoTracking()
            .Where(x => x.ArchivedAtUtc == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryText))
        {
            search = search.Where(x =>
                EF.Functions.ILike(x.LabelRaw, $"%{queryText}%") ||
                (x.LayerHint != null && EF.Functions.ILike(x.LayerHint, $"%{queryText}%")) ||
                (x.RmHint != null && EF.Functions.ILike(x.RmHint, $"%{queryText}%")) ||
                (x.Note != null && EF.Functions.ILike(x.Note, $"%{queryText}%")));
        }

        return await search
            .OrderBy(x => x.LabelRaw)
            .Take(take)
            .Select(x => new UnknownSubdivisionClusterLookupDto(
                x.Id,
                x.LabelRaw,
                x.LayerHint,
                x.RmHint,
                x.Observations.Count,
                x.ResolvedSubdivisionId != null,
                x.ArchivedAtUtc != null,
                x.ResolvedSubdivision != null ? x.ResolvedSubdivision.Name : null))
            .ToListAsync(ct);
    }
}
