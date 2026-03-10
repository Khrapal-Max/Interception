//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Окремий application-сервіс для аналітичної роботи з raw-учасниками.
/// Не втручається в операторський write-flow і не змінює raw-спостереження.
/// </summary>
public sealed class AnalyticsParticipantResolutionService(IDbContextFactory<AppDbContext> dbFactory) : IAnalyticsParticipantResolutionService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<AnalyticsParticipantResolutionPageDto?> GetPageAsync(Guid participantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var participantRow = await db.ObservationParticipants
            .AsNoTracking()
            .Where(x => x.Id == participantId)
            .Select(x => new
            {
                x.Id,
                x.ObservationId,
                x.Ordinal,
                x.LabelRaw,
                x.LabelNorm,
                x.IsUnknown,
                x.RoleRaw,
                Observation = new
                {
                    x.Observation.ObservedDate,
                    DayPart = (short)x.Observation.DayPart,
                    x.Observation.ActionRaw,
                    x.Observation.Layer,
                    x.Observation.RmRaw,
                    x.Observation.LocationRaw,
                    x.Observation.DistrictRaw,
                    x.Observation.Note
                }
            })
            .FirstOrDefaultAsync(ct);

        if (participantRow is null)
            return null;

        var memberRow = await db.UnknownClusterMembers
            .AsNoTracking()
            .Where(x => x.ObservationParticipantId == participantId)
            .Select(x => new
            {
                x.UnknownClusterId,
                x.UnknownCluster.Code,
                x.UnknownCluster.DisplayName,
                x.UnknownCluster.ResolvedActorId,
                ActorDisplayName = x.UnknownCluster.ResolvedActor != null ? x.UnknownCluster.ResolvedActor.DisplayName : null
            })
            .FirstOrDefaultAsync(ct);

        var relatedRows = await db.ObservationParticipants
            .AsNoTracking()
            .Where(x => x.ObservationId == participantRow.ObservationId && x.Id != participantId)
            .Select(x => new
            {
                x.Id,
                x.Ordinal,
                x.LabelRaw,
                x.LabelNorm,
                x.IsUnknown,
                x.RoleRaw,
                ClusterId = x.IsUnknown
                    ? db.UnknownClusterMembers
                        .Where(m => m.ObservationParticipantId == x.Id)
                        .Select(m => (Guid?)m.UnknownClusterId)
                        .FirstOrDefault()
                    : null,
                ClusterCode = x.IsUnknown
                    ? db.UnknownClusterMembers
                        .Where(m => m.ObservationParticipantId == x.Id)
                        .Select(m => m.UnknownCluster.Code)
                        .FirstOrDefault()
                    : null,
                ResolvedActorDisplayName = x.IsUnknown
                    ? db.UnknownClusterMembers
                        .Where(m => m.ObservationParticipantId == x.Id)
                        .Select(m => m.UnknownCluster.ResolvedActor != null ? m.UnknownCluster.ResolvedActor.DisplayName : null)
                        .FirstOrDefault()
                    : null
            })
            .OrderBy(x => x.Ordinal)
            .ToListAsync(ct);

        var rawDisplayName = string.IsNullOrWhiteSpace(participantRow.LabelRaw)
            ? $"НВ {participantRow.Ordinal}"
            : participantRow.LabelRaw;

        var related = relatedRows
            .Select(x => new AnalyticsRelatedParticipantDto(
                x.Id,
                x.Ordinal,
                string.IsNullOrWhiteSpace(x.LabelRaw) ? $"НВ {x.Ordinal}" : x.LabelRaw!,
                x.IsUnknown,
                x.RoleRaw,
                BuildEffectiveNodeDisplay(x.IsUnknown, x.ClusterId, x.ClusterCode, x.ResolvedActorDisplayName, x.LabelRaw, x.LabelNorm, x.Ordinal)))
            .ToList();

        return new AnalyticsParticipantResolutionPageDto(
            participantRow.Id,
            participantRow.ObservationId,
            participantRow.Ordinal,
            rawDisplayName,
            participantRow.IsUnknown,
            participantRow.RoleRaw,
            participantRow.Observation.ObservedDate,
            participantRow.Observation.DayPart,
            participantRow.Observation.ActionRaw,
            participantRow.Observation.Layer,
            participantRow.Observation.RmRaw,
            participantRow.Observation.LocationRaw,
            participantRow.Observation.DistrictRaw,
            participantRow.Observation.Note,
            memberRow?.UnknownClusterId,
            memberRow?.Code,
            memberRow?.DisplayName,
            memberRow?.ResolvedActorId,
            memberRow?.ActorDisplayName,
            related);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnalyticsUnknownClusterLookupDto>> SearchUnknownClustersAsync(string query, int take, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        take = Math.Clamp(take, 1, 20);
        var norm = TextNorm.Normalize(query);

        var clusters = db.UnknownClusters
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(norm))
        {
            clusters = clusters.Where(x =>
                EF.Functions.ILike(x.Code, $"%{query.Trim()}%") ||
                (x.DisplayName != null && EF.Functions.ILike(x.DisplayName, $"%{query.Trim()}%")));
        }

        return await clusters
            .OrderBy(x => x.Status)
            .ThenBy(x => x.Code)
            .Take(take)
            .Select(x => new AnalyticsUnknownClusterLookupDto(
                x.Id,
                x.Code,
                x.DisplayName,
                x.Status,
                x.Members.Count,
                x.ResolvedActor != null ? x.ResolvedActor.DisplayName : null))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task CreateUnknownClusterAsync(Guid participantId, string? displayName, string? reason, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await EnsureParticipantExistsAsync(db, participantId, ct);

        var now = DateTime.UtcNow;
        var codeBase = $"UNK-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var code = codeBase[..Math.Min(64, codeBase.Length)];
        var cluster = UnknownCluster.Create(Guid.NewGuid(), code, Clean(displayName), now, null);

        db.UnknownClusters.Add(cluster);
        await UpsertMemberAsync(db, Guid.NewGuid(), cluster.Id, participantId, Clean(reason), now, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddToUnknownClusterAsync(Guid participantId, Guid unknownClusterId, string? reason, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await EnsureParticipantExistsAsync(db, participantId, ct);

        var clusterExists = await db.UnknownClusters.AnyAsync(x => x.Id == unknownClusterId, ct);
        if (!clusterExists)
            throw new InvalidOperationException("Unknown cluster was not found.");

        await UpsertMemberAsync(db, Guid.NewGuid(), unknownClusterId, participantId, Clean(reason), DateTime.UtcNow, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task ResolveAsActorAsync(Guid participantId, string displayName, string? callsign, string? note, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Actor display name is required.", nameof(displayName));

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await EnsureParticipantExistsAsync(db, participantId, ct);

        var existingMember = await db.UnknownClusterMembers
            .FirstOrDefaultAsync(x => x.ObservationParticipantId == participantId, ct);

        var now = DateTime.UtcNow;
        UnknownCluster cluster;

        if (existingMember is null)
        {
            var codeBase = $"UNK-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            var clusterCode = codeBase[..Math.Min(64, codeBase.Length)];
            cluster = UnknownCluster.Create(Guid.NewGuid(), clusterCode, null, now, null);
            db.UnknownClusters.Add(cluster);

            var member = UnknownClusterMember.Create(
                Guid.NewGuid(),
                cluster.Id,
                participantId,
                "Автоматично створено при резолюції в актора.",
                now,
                null);

            db.UnknownClusterMembers.Add(member);
        }
        else
        {
            cluster = await db.UnknownClusters.FirstAsync(x => x.Id == existingMember.UnknownClusterId, ct);
        }

        var actor = ResolvedActor.Create(Guid.NewGuid(), "person", displayName.Trim(), Clean(callsign), Clean(note), now, null);
        db.ResolvedActors.Add(actor);

        cluster.ResolveToActor(actor.Id);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task EnsureParticipantExistsAsync(AppDbContext db, Guid participantId, CancellationToken ct)
    {
        var exists = await db.ObservationParticipants.AsNoTracking().AnyAsync(x => x.Id == participantId, ct);
        if (!exists)
            throw new InvalidOperationException("Observation participant was not found.");
    }

    private static async Task UpsertMemberAsync(
        AppDbContext db,
        Guid memberId,
        Guid clusterId,
        Guid participantId,
        string? reason,
        DateTime addedAtUtc,
        CancellationToken ct)
    {
        var existing = await db.UnknownClusterMembers
            .FirstOrDefaultAsync(x => x.ObservationParticipantId == participantId, ct);

        if (existing is null)
        {
            db.UnknownClusterMembers.Add(UnknownClusterMember.Create(
                memberId,
                clusterId,
                participantId,
                reason,
                addedAtUtc,
                null));
            return;
        }

        existing.MoveToCluster(clusterId, reason, addedAtUtc, null);
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildEffectiveNodeDisplay(
        bool isUnknown,
        Guid? clusterId,
        string? clusterCode,
        string? actorDisplayName,
        string? labelRaw,
        string? labelNorm,
        int ordinal)
    {
        if (isUnknown && !string.IsNullOrWhiteSpace(actorDisplayName))
            return actorDisplayName!;

        if (isUnknown && clusterId is not null)
            return clusterCode ?? $"cluster:{clusterId}";

        if (isUnknown)
            return string.IsNullOrWhiteSpace(labelRaw) ? $"НВ {ordinal}" : labelRaw!;

        return labelRaw ?? labelNorm ?? $"known:{ordinal}";
    }
}
