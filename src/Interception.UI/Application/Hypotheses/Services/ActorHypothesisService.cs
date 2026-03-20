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
/// EF-backed application service for analytical person hypotheses.
/// </summary>
public sealed class ActorHypothesisService(IDbContextFactory<AppDbContext> dbFactory) : IActorHypothesisService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<ActorHypothesisRegistryPageDto> SearchAsync(ActorHypothesisFilterDto filter, CancellationToken ct)
    {
        var skip = Math.Max(0, filter.Skip);
        var take = Math.Clamp(filter.Take, 1, 200);
        var queryText = (filter.Query ?? string.Empty).Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.UnknownClusters
            .AsNoTracking()
            .AsQueryable();

        if (!filter.IncludeArchived)
            query = query.Where(x => x.ArchivedAtUtc == null);

        if (filter.OnlyResolved)
            query = query.Where(x => x.ResolvedActorId != null);

        if (!string.IsNullOrWhiteSpace(queryText))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.Title, $"%{queryText}%") ||
                (x.Note != null && EF.Functions.ILike(x.Note, $"%{queryText}%")) ||
                (x.ResolvedActor != null && EF.Functions.ILike(x.ResolvedActor.DisplayName, $"%{queryText}%")) ||
                (x.ResolvedActor != null && x.ResolvedActor.PrimaryRole != null && EF.Functions.ILike(x.ResolvedActor.PrimaryRole, $"%{queryText}%")));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.ArchivedAtUtc != null)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(x => new ActorHypothesisRegistryItemDto(
                x.Id,
                x.Title,
                x.Note,
                x.Members.Count,
                x.ResolvedActorId,
                x.ResolvedActor != null ? x.ResolvedActor.DisplayName : null,
                x.ResolvedActor != null ? x.ResolvedActor.PrimaryRole : null,
                x.ArchivedAtUtc != null,
                x.CreatedAtUtc,
                x.ArchivedAtUtc,
                x.Members
                    .Select(m => (DateTime?)m.ObservationParticipant.Observation.ObservedDate)
                    .OrderByDescending(v => v)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return new ActorHypothesisRegistryPageDto(items, totalCount);
    }

    public async Task<ActorHypothesisDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownClusters
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Note,
                x.ResolvedActorId,
                ResolvedActorDisplayName = x.ResolvedActor != null ? x.ResolvedActor.DisplayName : null,
                ResolvedActorPrimaryRole = x.ResolvedActor != null ? x.ResolvedActor.PrimaryRole : null,
                IsArchived = x.ArchivedAtUtc != null,
                x.CreatedAtUtc,
                x.CreatedBy,
                x.ArchivedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (cluster is null)
            return null;

        var members = await db.UnknownClusterMembers
            .AsNoTracking()
            .Where(x => x.UnknownClusterId == id)
            .OrderByDescending(x => x.ObservationParticipant.Observation.ObservedDate)
            .ThenBy(x => x.ObservationParticipant.Ordinal)
            .Select(x => new ActorHypothesisMemberDto(
                x.Id,
                x.ObservationParticipantId,
                x.ObservationParticipant.ObservationId,
                x.ObservationParticipant.Observation.ObservedDate,
                x.ObservationParticipant.Ordinal,
                string.IsNullOrWhiteSpace(x.ObservationParticipant.LabelRaw)
                    ? $"НВ {x.ObservationParticipant.Ordinal}"
                    : x.ObservationParticipant.LabelRaw!,
                x.ObservationParticipant.IsUnknown,
                x.ObservationParticipant.StartedAsUnknown,
                x.ObservationParticipant.RoleRaw,
                x.Note,
                x.CreatedAtUtc))
            .ToListAsync(ct);

        var observationIds = members
            .Select(x => x.ObservationId)
            .Distinct()
            .ToList();

        var observations = observationIds.Count == 0
            ? []
            : await db.Observations
                .AsNoTracking()
                .Where(x => observationIds.Contains(x.Id))
                .OrderByDescending(x => x.ObservedDate)
                .ThenByDescending(x => x.CreatedAtUtc)
                .Select(x => new ActorHypothesisObservationDto(
                    x.Id,
                    x.ObservedDate,
                    x.ActionRaw,
                    x.Layer,
                    x.RmRaw,
                    x.LocationRaw,
                    x.DistrictRaw,
                    x.SubdivisionRaw,
                    x.Note,
                    x.Participants.Count))
                .ToListAsync(ct);

        return new ActorHypothesisDetailsDto(
            cluster.Id,
            cluster.Title,
            cluster.Note,
            cluster.ResolvedActorId,
            cluster.ResolvedActorDisplayName,
            cluster.ResolvedActorPrimaryRole,
            cluster.IsArchived,
            cluster.CreatedAtUtc,
            cluster.CreatedBy,
            cluster.ArchivedAtUtc,
            members,
            observations);
    }

    public async Task<HypothesisSaveResultDto> CreateAsync(ActorHypothesisCreateDto request, string? createdBy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var normalizedTitle = TextNorm.NormalizeRequired(request.Title);
        var duplicate = await db.UnknownClusters
            .AnyAsync(x => x.ArchivedAtUtc == null && x.TitleNorm == normalizedTitle, ct);

        if (duplicate)
            throw new InvalidOperationException($"Person hypothesis '{request.Title}' already exists.");

        var cluster = UnknownCluster.Create(request.Title, request.Note, createdBy);

        var seedIds = request.SeedParticipantIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (seedIds.Count > 0)
        {
            var alreadyLinkedCount = await db.UnknownClusterMembers
                .CountAsync(x => seedIds.Contains(x.ObservationParticipantId), ct);

            if (alreadyLinkedCount > 0)
                throw new InvalidOperationException("At least one seed participant already belongs to another hypothesis.");

            var existingParticipantsCount = await db.ObservationParticipants
                .CountAsync(x => seedIds.Contains(x.Id), ct);

            if (existingParticipantsCount != seedIds.Count)
                throw new InvalidOperationException("At least one seed participant was not found.");

            foreach (var participantId in seedIds)
                cluster.AddMember(participantId);
        }

        db.UnknownClusters.Add(cluster);
        await db.SaveChangesAsync(ct);

        return new HypothesisSaveResultDto(cluster.Id, true);
    }

    public async Task UpdateAsync(Guid id, ActorHypothesisUpdateDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownClusters
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Person hypothesis was not found.");

        var normalizedTitle = TextNorm.NormalizeRequired(request.Title);
        var duplicate = await db.UnknownClusters
            .AnyAsync(x => x.Id != id && x.ArchivedAtUtc == null && x.TitleNorm == normalizedTitle, ct);

        if (duplicate)
            throw new InvalidOperationException($"Person hypothesis '{request.Title}' already exists.");

        cluster.Rename(request.Title);
        cluster.SetNote(request.Note);

        await db.SaveChangesAsync(ct);
    }

    public async Task AddParticipantAsync(Guid clusterId, Guid observationParticipantId, string? note, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownClusters
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == clusterId, ct)
            ?? throw new InvalidOperationException("Person hypothesis was not found.");

        var participantExists = await db.ObservationParticipants
            .AnyAsync(x => x.Id == observationParticipantId, ct);

        if (!participantExists)
            throw new InvalidOperationException("Observation participant was not found.");

        var alreadyLinked = await db.UnknownClusterMembers
            .AnyAsync(x => x.ObservationParticipantId == observationParticipantId, ct);

        if (alreadyLinked)
            throw new InvalidOperationException("Observation participant already belongs to another hypothesis.");

        cluster.AddMember(observationParticipantId, note);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveParticipantAsync(Guid clusterId, Guid observationParticipantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var member = await db.UnknownClusterMembers
            .FirstOrDefaultAsync(x => x.UnknownClusterId == clusterId && x.ObservationParticipantId == observationParticipantId, ct)
            ?? throw new InvalidOperationException("Linked observation participant was not found in this hypothesis.");

        db.UnknownClusterMembers.Remove(member);
        await db.SaveChangesAsync(ct);
    }

    public async Task MoveParticipantAsync(Guid sourceClusterId, Guid observationParticipantId, Guid targetClusterId, CancellationToken ct)
    {
        if (sourceClusterId == targetClusterId)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var member = await db.UnknownClusterMembers
            .FirstOrDefaultAsync(x => x.UnknownClusterId == sourceClusterId && x.ObservationParticipantId == observationParticipantId, ct)
            ?? throw new InvalidOperationException("Linked observation participant was not found in the source hypothesis.");

        var targetExists = await db.UnknownClusters
            .AnyAsync(x => x.Id == targetClusterId, ct);

        if (!targetExists)
            throw new InvalidOperationException("Target hypothesis was not found.");

        var duplicateInTarget = await db.UnknownClusterMembers
            .AnyAsync(x => x.UnknownClusterId == targetClusterId && x.ObservationParticipantId == observationParticipantId, ct);

        if (duplicateInTarget)
            throw new InvalidOperationException("Target hypothesis already contains this participant.");

        member.MoveToCluster(targetClusterId);
        await db.SaveChangesAsync(ct);
    }

    public async Task MergeAsync(Guid sourceClusterId, Guid targetClusterId, CancellationToken ct)
    {
        if (sourceClusterId == targetClusterId)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var source = await db.UnknownClusters
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == sourceClusterId, ct)
            ?? throw new InvalidOperationException("Source hypothesis was not found.");

        var target = await db.UnknownClusters
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == targetClusterId, ct)
            ?? throw new InvalidOperationException("Target hypothesis was not found.");

        var targetParticipantIds = target.Members
            .Select(x => x.ObservationParticipantId)
            .ToHashSet();

        foreach (var member in source.Members.ToList())
        {
            if (targetParticipantIds.Contains(member.ObservationParticipantId))
            {
                db.UnknownClusterMembers.Remove(member);
                continue;
            }

            member.MoveToCluster(targetClusterId);
        }

        source.Archive();
        await db.SaveChangesAsync(ct);
    }

    public async Task ResolveAsync(Guid clusterId, ResolvedActorUpsertDto request, string? createdBy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownClusters
            .FirstOrDefaultAsync(x => x.Id == clusterId, ct)
            ?? throw new InvalidOperationException("Person hypothesis was not found.");

        var normalizedDisplayName = TextNorm.NormalizeRequired(request.DisplayName);
        var actor = await db.ResolvedActors
            .FirstOrDefaultAsync(x => x.IsActive && x.DisplayNameNorm == normalizedDisplayName, ct);

        if (actor is null)
        {
            actor = ResolvedActor.Create(request.DisplayName, request.PrimaryRole, request.Note, createdBy);
            db.ResolvedActors.Add(actor);
        }
        else
        {
            actor.UpdatePrimaryRole(request.PrimaryRole);
            actor.SetNote(request.Note);
        }

        cluster.ResolveToActor(actor.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task ReopenAsync(Guid clusterId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownClusters
            .FirstOrDefaultAsync(x => x.Id == clusterId, ct)
            ?? throw new InvalidOperationException("Person hypothesis was not found.");

        cluster.Reopen();
        await db.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid clusterId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownClusters
            .FirstOrDefaultAsync(x => x.Id == clusterId, ct)
            ?? throw new InvalidOperationException("Person hypothesis was not found.");

        cluster.Archive();
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UnknownClusterLookupDto>> SearchOpenClustersAsync(string? query, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 50);
        var queryText = (query ?? string.Empty).Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var search = db.UnknownClusters
            .AsNoTracking()
            .Where(x => x.ArchivedAtUtc == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryText))
        {
            search = search.Where(x =>
                EF.Functions.ILike(x.Title, $"%{queryText}%") ||
                (x.Note != null && EF.Functions.ILike(x.Note, $"%{queryText}%")));
        }

        return await search
            .OrderBy(x => x.Title)
            .Take(take)
            .Select(x => new UnknownClusterLookupDto(
                x.Id,
                x.Title,
                x.Members.Count,
                x.ResolvedActorId != null,
                x.ArchivedAtUtc != null,
                x.ResolvedActor != null ? x.ResolvedActor.DisplayName : null))
            .ToListAsync(ct);
    }
}
