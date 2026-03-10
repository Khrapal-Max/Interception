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
                x.Status,
                x.ResolvedActorId,
                ResolvedActorDisplayName = x.ResolvedActor != null ? x.ResolvedActor.DisplayName : null
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
            clusterRow.Status,
            clusterRow.ResolvedActorId,
            clusterRow.ResolvedActorDisplayName,
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
    public async Task ResolveClusterAsActorAsync(Guid clusterId, string displayName, string? callsign, string? note, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Actor display name is required.", nameof(displayName));

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var cluster = await db.UnknownClusters.FirstOrDefaultAsync(x => x.Id == clusterId, ct)
                     ?? throw new InvalidOperationException("Unknown cluster was not found.");

        var actor = ResolvedActor.Create(Guid.NewGuid(), "person", displayName.Trim(), Clean(callsign), Clean(note), DateTime.UtcNow, null);
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

        var target = await db.UnknownClusters
            .FirstOrDefaultAsync(x => x.Id == targetClusterId, ct)
            ?? throw new InvalidOperationException("Target cluster was not found.");

        if (source.ResolvedActorId.HasValue && target.ResolvedActorId.HasValue && source.ResolvedActorId != target.ResolvedActorId)
            throw new InvalidOperationException("Both clusters are already resolved to different actors.");

        var now = DateTime.UtcNow;
        foreach (var member in source.Members.ToList())
        {
            member.MoveToCluster(target.Id, Clean(reason), now, null);
        }

        if (!target.ResolvedActorId.HasValue && source.ResolvedActorId.HasValue)
            target.ResolveToActor(source.ResolvedActorId.Value);

        source.MarkMerged();

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReassignParticipantAsync(Guid sourceClusterId, Guid participantId, Guid targetClusterId, string? reason, CancellationToken ct)
    {
        if (sourceClusterId == targetClusterId)
            throw new InvalidOperationException("Неможливо перепризначити учасника в той самий кластер.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var sourceCluster = await db.UnknownClusters
            .FirstOrDefaultAsync(x => x.Id == sourceClusterId, ct)
            ?? throw new InvalidOperationException("Початковий кластер не знайдено.");

        var targetCluster = await db.UnknownClusters
            .FirstOrDefaultAsync(x => x.Id == targetClusterId, ct)
            ?? throw new InvalidOperationException("Цільовий кластер не знайдено.");

        var member = await db.UnknownClusterMembers
            .FirstOrDefaultAsync(
                x => x.UnknownClusterId == sourceClusterId && x.ObservationParticipantId == participantId,
                ct)
            ?? throw new InvalidOperationException("Учасника не знайдено в поточному кластері.");

        var now = DateTime.UtcNow;
        member.MoveToCluster(targetCluster.Id, Clean(reason), now, null);

        await db.SaveChangesAsync(ct);

        var sourceHasMembers = await db.UnknownClusterMembers
            .AnyAsync(x => x.UnknownClusterId == sourceClusterId, ct);

        if (!sourceHasMembers)
        {
            sourceCluster.MarkArchived();
            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildRelatedKey(bool isUnknown, Guid? clusterId, string? resolvedActorDisplayName, string? labelNorm, int ordinal)
    {
        if (!string.IsNullOrWhiteSpace(resolvedActorDisplayName))
            return $"actor:{TextNorm.NormalizeRequired(resolvedActorDisplayName)}";

        if (clusterId.HasValue)
            return $"cluster:{clusterId.Value:N}";

        if (!isUnknown && !string.IsNullOrWhiteSpace(labelNorm))
            return $"known:{labelNorm}";

        return $"unknown:{ordinal}";
    }

    private static string BuildRelatedDisplay(bool isUnknown, Guid? clusterId, string? clusterCode, string? resolvedActorDisplayName, string? labelRaw, int ordinal)
    {
        if (!string.IsNullOrWhiteSpace(resolvedActorDisplayName))
            return resolvedActorDisplayName;

        if (clusterId.HasValue)
            return string.IsNullOrWhiteSpace(clusterCode) ? "Кластер" : clusterCode!;

        if (!isUnknown)
            return string.IsNullOrWhiteSpace(labelRaw) ? "—" : labelRaw!;

        return string.IsNullOrWhiteSpace(labelRaw) ? $"НВ {ordinal}" : labelRaw!;
    }

    private static string BuildRelatedKind(bool isUnknown, Guid? clusterId, string? resolvedActorDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(resolvedActorDisplayName))
            return "Встановлена особа";

        if (clusterId.HasValue)
            return "Кластер";

        return isUnknown ? "Невизначена особа" : "Відома особа";
    }
}