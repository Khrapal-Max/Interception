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
/// Сервіс сторінки деталей unknown-кластера.
/// Працює поверх EF-сутностей і не змінює raw-історію observation.
/// </summary>
public sealed class AnalyticsUnknownClusterDetailsService(IDbContextFactory<AppDbContext> dbFactory) : IAnalyticsUnknownClusterDetailsService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<AnalyticsUnknownClusterDetailsPageDto?> GetPageAsync(Guid clusterId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var clusterRow = await db.UnknownClusters
            .AsNoTracking()
            .Where(x => x.Id == clusterId)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.DisplayName,
                x.Role,
                x.Status,
                x.ArchiveReason,
                x.ResolvedActorId,
                ResolvedActorDisplayName = x.ResolvedActor != null ? x.ResolvedActor.DisplayName : null,
                ConfirmedRole = x.ResolvedActor != null ? x.ResolvedActor.Role : null
            })
            .FirstOrDefaultAsync(ct);

        if (clusterRow is null)
            return null;

        var memberRows = await db.UnknownClusterMembers
            .AsNoTracking()
            .Where(x => x.UnknownClusterId == clusterId)
            .Select(x => new
            {
                x.ObservationParticipantId,
                x.ObservationParticipant.ObservationId,
                x.ObservationParticipant.Ordinal,
                x.ObservationParticipant.LabelRaw,
                x.ObservationParticipant.IsUnknown,
                x.ObservationParticipant.RoleRaw,
                x.ObservationParticipant.Observation.ObservedDate,
                DayPart = (short)x.ObservationParticipant.Observation.DayPart,
                x.ObservationParticipant.Observation.ActionRaw,
                x.ObservationParticipant.Observation.Layer,
                x.ObservationParticipant.Observation.LocationRaw
            })
            .OrderByDescending(x => x.ObservedDate)
            .ThenByDescending(x => x.DayPart)
            .ThenBy(x => x.Ordinal)
            .ToListAsync(ct);

        var memberObservationIds = memberRows
            .Select(x => x.ObservationId)
            .Distinct()
            .ToList();

        var historyRows = await db.Observations
            .AsNoTracking()
            .Where(x => memberObservationIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.ObservedDate,
                DayPart = (short)x.DayPart,
                x.ActionRaw,
                x.Layer,
                x.RmRaw,
                x.LocationRaw,
                x.DistrictRaw,
                ParticipantsCount = x.Participants.Count
            })
            .OrderByDescending(x => x.ObservedDate)
            .ThenByDescending(x => x.DayPart)
            .ToListAsync(ct);

        var clusterMemberParticipantIds = await db.UnknownClusterMembers
            .AsNoTracking()
            .Where(m => m.UnknownClusterId == clusterId)
            .Select(m => m.ObservationParticipantId)
            .ToListAsync(ct);

        var relatedRows = await db.ObservationParticipants
            .AsNoTracking()
            .Where(x => memberObservationIds.Contains(x.ObservationId)
                        && !clusterMemberParticipantIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.ObservationId,
                x.Ordinal,
                x.LabelRaw,
                x.LabelNorm,
                x.IsUnknown,
                x.Observation.ObservedDate,
                ClusterId = db.UnknownClusterMembers
                    .Where(m => m.ObservationParticipantId == x.Id)
                    .Select(m => (Guid?)m.UnknownClusterId)
                    .FirstOrDefault(),
                ClusterCode = db.UnknownClusterMembers
                    .Where(m => m.ObservationParticipantId == x.Id)
                    .Select(m => m.UnknownCluster.Code)
                    .FirstOrDefault(),
                ResolvedActorDisplayName = db.UnknownClusterMembers
                    .Where(m => m.ObservationParticipantId == x.Id)
                    .Select(m => m.UnknownCluster.ResolvedActor != null ? m.UnknownCluster.ResolvedActor.DisplayName : null)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var memberDtos = memberRows
            .Select(x => new AnalyticsUnknownClusterMemberDto(
                x.ObservationParticipantId,
                x.ObservationId,
                x.Ordinal,
                string.IsNullOrWhiteSpace(x.LabelRaw) ? $"НВ {x.Ordinal}" : x.LabelRaw!,
                x.IsUnknown,
                x.RoleRaw,
                x.ObservedDate,
                x.DayPart,
                x.ActionRaw,
                x.Layer,
                x.LocationRaw))
            .ToList();

        var historyDtos = historyRows
            .Select(x => new AnalyticsUnknownClusterObservationDto(
                x.Id,
                x.ObservedDate,
                x.DayPart,
                x.ActionRaw,
                x.Layer,
                x.RmRaw,
                x.LocationRaw,
                x.DistrictRaw,
                x.ParticipantsCount))
            .ToList();

        var relatedDtos = relatedRows
            .Select(x => new
            {
                Key = BuildRelatedKey(x.IsUnknown, x.ClusterId, x.ResolvedActorDisplayName, x.LabelNorm, x.Ordinal),
                Display = BuildRelatedDisplay(x.IsUnknown, x.ClusterId, x.ClusterCode, x.ResolvedActorDisplayName, x.LabelRaw, x.Ordinal),
                Kind = BuildRelatedKind(x.IsUnknown, x.ClusterId, x.ResolvedActorDisplayName),
                x.ObservationId,
                x.ObservedDate
            })
            .GroupBy(x => new { x.Key, x.Display, x.Kind })
            .Select(g => new AnalyticsUnknownClusterRelatedPersonDto(
                g.Key.Display,
                g.Key.Kind,
                g.Select(x => x.ObservationId).Distinct().Count(),
                g.Max(x => (DateOnly?)x.ObservedDate)))
            .OrderByDescending(x => x.SeenCount)
            .ThenByDescending(x => x.LastSeenDate)
            .ThenBy(x => x.DisplayName)
            .ToList();

        return new AnalyticsUnknownClusterDetailsPageDto(
            clusterRow.Id,
            clusterRow.Code,
            clusterRow.DisplayName,
            clusterRow.Role,
            clusterRow.Status,
            clusterRow.ArchiveReason,
            clusterRow.ResolvedActorId,
            clusterRow.ResolvedActorDisplayName,
            clusterRow.ConfirmedRole,
            memberDtos.Count,
            historyDtos.Count == 0 ? null : historyDtos.Max(x => x.ObservedDate),
            memberDtos,
            historyDtos,
            relatedDtos);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AnalyticsUnknownClusterLookupDto>> SearchOtherClustersAsync(Guid clusterId, string query, int take, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        take = Math.Clamp(take, 1, 20);
        var rawQuery = (query ?? string.Empty).Trim();

        if (rawQuery.Length < 2)
            return [];

        return await db.UnknownClusters
            .AsNoTracking()
            .Where(x => x.Id != clusterId)
            .Where(x =>
                EF.Functions.ILike(x.Code, $"%{rawQuery}%") ||
                (x.DisplayName != null && EF.Functions.ILike(x.DisplayName, $"%{rawQuery}%")) ||
                (x.ResolvedActor != null && EF.Functions.ILike(x.ResolvedActor.DisplayName, $"%{rawQuery}%")))
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
    public async Task ResolveClusterAsActorAsync(Guid clusterId, string displayName, string? role, string? callsign, string? note, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Actor display name is required.", nameof(displayName));

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var cluster = await db.UnknownClusters.FirstOrDefaultAsync(x => x.Id == clusterId, ct)
                     ?? throw new InvalidOperationException("Unknown cluster was not found.");

        if (!string.IsNullOrWhiteSpace(role))
            cluster.SetRole(role);

        var actor = ResolvedActor.Create(
            Guid.NewGuid(),
            "person",
            displayName.Trim(),
            Clean(role) ?? cluster.Role,
            Clean(callsign),
            Clean(note),
            DateTime.UtcNow,
            null);

        db.ResolvedActors.Add(actor);
        cluster.ResolveToActor(actor.Id);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task MergeClusterAsync(Guid clusterId, Guid targetClusterId, string? reason, CancellationToken ct)
    {
        if (clusterId == targetClusterId)
            throw new InvalidOperationException("Cannot merge cluster into itself.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var source = await db.UnknownClusters
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == clusterId, ct)
            ?? throw new InvalidOperationException("Source cluster was not found.");

        var target = await db.UnknownClusters.FirstOrDefaultAsync(x => x.Id == targetClusterId, ct)
            ?? throw new InvalidOperationException("Target cluster was not found.");

        source.MarkMerged();
        target.SetDisplayNameIfMissing(source.DisplayName);
        target.SetRoleIfMissing(source.Role);

        var movedAt = DateTime.UtcNow;
        foreach (var member in source.Members)
        {
            member.MoveToCluster(targetClusterId, Clean(reason) ?? "merged", movedAt, null);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReassignParticipantAsync(Guid clusterId, Guid participantId, Guid targetClusterId, string? reason, CancellationToken ct)
    {
        if (clusterId == targetClusterId)
            throw new InvalidOperationException("Source and target cluster must be different.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var source = await db.UnknownClusters
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == clusterId, ct)
            ?? throw new InvalidOperationException("Source cluster was not found.");

        var target = await db.UnknownClusters.FirstOrDefaultAsync(x => x.Id == targetClusterId, ct)
            ?? throw new InvalidOperationException("Target cluster was not found.");

        var member = source.Members.FirstOrDefault(x => x.ObservationParticipantId == participantId)
            ?? throw new InvalidOperationException("Cluster member was not found.");

        member.MoveToCluster(targetClusterId, Clean(reason), DateTime.UtcNow, null);

        var participant = await db.ObservationParticipants.FirstOrDefaultAsync(x => x.Id == participantId, ct);
        if (participant is not null)
        {
            target.SetDisplayNameIfMissing(participant.LabelRaw);
            target.SetRoleIfMissing(participant.RoleRaw);
        }

        if (!await db.UnknownClusterMembers.AnyAsync(x => x.UnknownClusterId == clusterId && x.ObservationParticipantId != participantId, ct))
            source.MarkArchived("empty");

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildRelatedKey(bool isUnknown, Guid? clusterId, string? actorDisplayName, string? labelNorm, int ordinal)
    {
        if (!string.IsNullOrWhiteSpace(actorDisplayName))
            return $"actor:{TextNorm.Normalize(actorDisplayName)}";

        if (isUnknown && clusterId is not null)
            return $"cluster:{clusterId}";

        if (isUnknown)
            return $"unknown:{ordinal}";

        return $"known:{labelNorm ?? ordinal.ToString()}";
    }

    private static string BuildRelatedDisplay(
        bool isUnknown,
        Guid? clusterId,
        string? clusterCode,
        string? actorDisplayName,
        string? labelRaw,
        int ordinal)
    {
        if (!string.IsNullOrWhiteSpace(actorDisplayName))
            return actorDisplayName!;

        if (isUnknown && clusterId is not null)
            return clusterCode ?? $"Припущення {clusterId}";

        if (isUnknown)
            return string.IsNullOrWhiteSpace(labelRaw) ? $"НВ {ordinal}" : labelRaw!;

        return labelRaw ?? $"Особа {ordinal}";
    }

    private static string BuildRelatedKind(bool isUnknown, Guid? clusterId, string? actorDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(actorDisplayName))
            return "fact";

        if (isUnknown && clusterId is not null)
            return "hypothesis";

        if (isUnknown)
            return "raw-unknown";

        return "known";
    }
}