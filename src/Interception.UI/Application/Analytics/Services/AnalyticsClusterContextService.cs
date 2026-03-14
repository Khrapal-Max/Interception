//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;
using Interception.UI.Application.Analytics.Dtos.AnalyticsClusters;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

public sealed class AnalyticsClusterContextService(IDbContextFactory<AppDbContext> dbFactory) : IAnalyticsClusterContextService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<AnalyticsClusterContextPageDto?> GetPageAsync(Guid clusterId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cluster = await db.UnknownClusters
            .AsNoTracking()
            .Where(x => x.Id == clusterId)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.DisplayName
            })
            .FirstOrDefaultAsync(ct);

        if (cluster is null)
            return null;

        var observations = await db.Observations
            .AsNoTracking()
            .Select(x => new ObservationRowDto(
                x.Id,
                x.ObservedDate,
                (short)x.DayPart,
                x.ActionRaw,
                x.ActionNorm,
                x.Layer,
                x.RmRaw,
                x.LocationRaw,
                x.DistrictRaw,
                x.Note))
            .ToListAsync(ct);

        var participants = await db.ObservationParticipants
            .AsNoTracking()
            .Select(x => new ParticipantRowDto(
                x.Id,
                x.ObservationId,
                x.Ordinal,
                x.LabelRaw,
                x.LabelNorm,
                x.IsUnknown,
                x.RoleRaw))
            .ToListAsync(ct);

        var resolutionMap = await db.UnknownClusterMembers
            .AsNoTracking()
            .Select(x => new ResolutionRowDto(
                x.ObservationParticipantId,
                x.UnknownClusterId,
                x.UnknownCluster.Code,
                x.UnknownCluster.DisplayName,
                x.UnknownCluster.ResolvedActorId,
                x.UnknownCluster.ResolvedActor != null ? x.UnknownCluster.ResolvedActor.DisplayName : null))
            .ToListAsync(ct);

        var resolutionDict = resolutionMap
            .GroupBy(x => x.ObservationParticipantId)
            .ToDictionary(x => x.Key, x => x.First());

        var memberIds = await db.UnknownClusterMembers
            .AsNoTracking()
            .Where(x => x.UnknownClusterId == clusterId)
            .Select(x => x.ObservationParticipantId)
            .ToListAsync(ct);

        if (memberIds.Count == 0)
        {
            return new AnalyticsClusterContextPageDto(
                cluster.Id,
                cluster.Code,
                cluster.DisplayName,
                0,
                0,
                [],
                [],
                [],
                []);
        }

        var memberParticipants = participants
            .Where(x => memberIds.Contains(x.Id))
            .ToList();

        var directObservationIds = memberParticipants
            .Select(x => x.ObservationId)
            .Distinct()
            .ToList();

        var directObservations = observations
            .Where(x => directObservationIds.Contains(x.Id))
            .OrderByDescending(x => x.ObservedDate)
            .ThenByDescending(x => x.DayPart)
            .ToList();

        var clusterActionNorms = directObservations
            .Select(x => x.ActionNorm)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clusterRms = directObservations
            .Select(x => AnalyticsScoring.Normalize(x.RmRaw))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clusterLayers = directObservations
            .Select(x => AnalyticsScoring.Normalize(x.Layer))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clusterDistricts = directObservations
            .Select(x => AnalyticsScoring.Normalize(x.DistrictRaw) ?? AnalyticsScoring.Normalize(x.LocationRaw))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clusterNoteTokens = AnalyticsScoring.ExtractNoteTokens(directObservations.Select(x => x.Note));

        var directNodeKeys = participants
            .Where(x => directObservationIds.Contains(x.ObservationId) && !memberIds.Contains(x.Id))
            .Select(x => BuildNodeKey(x, resolutionDict))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var directDtos = directObservations
            .Select(x => new AnalyticsClusterContextObservationDto(
                x.Id,
                x.ObservedDate,
                x.DayPart,
                x.ActionRaw,
                x.Layer,
                x.RmRaw,
                x.LocationRaw,
                x.DistrictRaw,
                x.Note,
                "direct",
                100,
                ["member of hypothesis"]))
            .ToList();

        var indirectDtos = new List<AnalyticsClusterContextObservationDto>();

        foreach (var observation in observations.Where(x => !directObservationIds.Contains(x.Id)))
        {
            var sameAction = clusterActionNorms.Contains(observation.ActionNorm);
            var sameRm = !string.IsNullOrWhiteSpace(observation.RmRaw)
                         && clusterRms.Contains(AnalyticsScoring.Normalize(observation.RmRaw)!);
            var sameLayer = !string.IsNullOrWhiteSpace(observation.Layer)
                            && clusterLayers.Contains(AnalyticsScoring.Normalize(observation.Layer)!);
            var sameDistrict = (!string.IsNullOrWhiteSpace(observation.DistrictRaw) &&
                                clusterDistricts.Contains(AnalyticsScoring.Normalize(observation.DistrictRaw)!))
                               || (!string.IsNullOrWhiteSpace(observation.LocationRaw) &&
                                   clusterDistricts.Contains(AnalyticsScoring.Normalize(observation.LocationRaw)!));

            var noteOverlap = AnalyticsScoring.HasNoteOverlap(observation.Note, clusterNoteTokens);

            var observationNodeKeys = participants
                .Where(x => x.ObservationId == observation.Id)
                .Select(x => BuildNodeKey(x, resolutionDict))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sameRelatedNode = observationNodeKeys.Overlaps(directNodeKeys);

            var closeInTime = directObservations.Any(x => Math.Abs(x.ObservedDate.DayNumber - observation.ObservedDate.DayNumber) <= 3);

            var score = AnalyticsScoring.CalculateContextScore(
                sameAction,
                sameRm,
                sameLayer,
                sameDistrict,
                noteOverlap,
                sameRelatedNode,
                closeInTime);

            if (score < 25)
                continue;

            indirectDtos.Add(new AnalyticsClusterContextObservationDto(
                observation.Id,
                observation.ObservedDate,
                observation.DayPart,
                observation.ActionRaw,
                observation.Layer,
                observation.RmRaw,
                observation.LocationRaw,
                observation.DistrictRaw,
                observation.Note,
                AnalyticsScoring.ToRelationKind(score, false),
                score,
                BuildMatchedSignals(sameAction, sameRm, sameLayer, sameDistrict, noteOverlap, sameRelatedNode, closeInTime)));
        }

        indirectDtos = [.. indirectDtos
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.ObservedDate)];

        var allContextObservationIds = directDtos
            .Select(x => x.ObservationId)
            .Concat(indirectDtos.Select(x => x.ObservationId))
            .Distinct()
            .ToList();

        var relatedNodes = BuildRelatedNodes(
            allContextObservationIds,
            [.. memberIds],
            observations,
            participants,
            resolutionDict);

        var actionChains = allContextObservationIds
            .Select(id => observations.First(x => x.Id == id))
            .GroupBy(x => x.ActionRaw)
            .Select(g =>
            {
                var relatedInAction = participants
                    .Where(x => g.Select(y => y.Id).Contains(x.ObservationId) && !memberIds.Contains(x.Id))
                    .Select(x =>
                    {
                        if (resolutionDict.TryGetValue(x.Id, out var resolution))
                            return resolution.ResolvedActorDisplayName ?? resolution.ClusterDisplayName ?? resolution.ClusterCode;

                        return x.IsUnknown
                            ? AnalyticsScoring.BuildUnknownDisplay(x.LabelRaw, x.Ordinal)
                            : x.LabelRaw ?? x.LabelNorm ?? $"known:{x.Id:N}";
                    })
                    .Distinct()
                    .Take(5)
                    .ToList();

                var signals = new List<string>();

                if (g.Any(x => clusterActionNorms.Contains(x.ActionNorm))) signals.Add("same-action");
                if (g.Any(x => !string.IsNullOrWhiteSpace(x.RmRaw) && clusterRms.Contains(AnalyticsScoring.Normalize(x.RmRaw)!))) signals.Add("same-rm");
                if (g.Any(x => !string.IsNullOrWhiteSpace(x.Layer) && clusterLayers.Contains(AnalyticsScoring.Normalize(x.Layer)!))) signals.Add("same-layer");
                if (g.Any(x => AnalyticsScoring.HasNoteOverlap(x.Note, clusterNoteTokens))) signals.Add("note-context");

                return new AnalyticsClusterActionChainDto(
                    g.Key,
                    g.Count(),
                    relatedInAction,
                    signals);
            })
            .OrderByDescending(x => x.SeenCount)
            .ThenBy(x => x.ActionRaw)
            .Take(10)
            .ToList();

        return new AnalyticsClusterContextPageDto(
            cluster.Id,
            cluster.Code,
            cluster.DisplayName,
            directDtos.Count,
            indirectDtos.Count,
            directDtos,
            indirectDtos,
            relatedNodes,
            actionChains);
    }

    private static List<string> BuildMatchedSignals(
        bool sameAction,
        bool sameRm,
        bool sameLayer,
        bool sameDistrict,
        bool noteOverlap,
        bool sameRelatedNode,
        bool closeInTime)
    {
        var list = new List<string>();

        if (sameAction) list.Add("same-action");
        if (sameRm) list.Add("same-rm");
        if (sameLayer) list.Add("same-layer");
        if (sameDistrict) list.Add("same-district");
        if (noteOverlap) list.Add("note-context");
        if (sameRelatedNode) list.Add("related-node");
        if (closeInTime) list.Add("time-window");

        return list;
    }

    private static string BuildNodeKey(ParticipantRowDto participant, Dictionary<Guid, ResolutionRowDto> resolutionMap)
    {
        if (resolutionMap.TryGetValue(participant.Id, out var resolution))
        {
            if (resolution.ResolvedActorId is not null)
                return $"actor:{resolution.ResolvedActorId}";

            return $"cluster:{resolution.ClusterId}";
        }

        if (participant.IsUnknown)
            return $"raw:{participant.Id}";

        return $"known:{participant.LabelNorm ?? participant.Id.ToString("N")}";
    }

    private static List<AnalyticsClusterContextNodeDto> BuildRelatedNodes(
        IReadOnlyCollection<Guid> observationIds,
        HashSet<Guid> excludedParticipantIds,
        IReadOnlyList<ObservationRowDto> observations,
        IReadOnlyList<ParticipantRowDto> participants,
        Dictionary<Guid, ResolutionRowDto> resolutionMap)
    {
        var rows = participants
            .Where(x => observationIds.Contains(x.ObservationId) && !excludedParticipantIds.Contains(x.Id))
            .Select(x =>
            {
                var key = BuildNodeKey(x, resolutionMap);

                string display;
                string type;

                if (resolutionMap.TryGetValue(x.Id, out var resolution))
                {
                    if (resolution.ResolvedActorId is not null)
                    {
                        display = resolution.ResolvedActorDisplayName ?? resolution.ClusterDisplayName ?? resolution.ClusterCode;
                        type = "fact";
                    }
                    else
                    {
                        display = resolution.ClusterDisplayName ?? resolution.ClusterCode;
                        type = "hypothesis";
                    }
                }
                else if (x.IsUnknown)
                {
                    display = AnalyticsScoring.BuildUnknownDisplay(x.LabelRaw, x.Ordinal);
                    type = "raw-unknown";
                }
                else
                {
                    display = x.LabelRaw ?? x.LabelNorm ?? $"known:{x.Id:N}";
                    type = "known";
                }

                return new
                {
                    Key = key,
                    Display = display,
                    Type = type,
                    x.ObservationId
                };
            })
            .ToList();

        return [.. rows
            .GroupBy(x => new { x.Key, x.Display, x.Type })
            .Select(g =>
            {
                var obs = observations.Where(x => g.Select(y => y.ObservationId).Contains(x.Id)).ToList();
                var stableRm = obs.Select(x => AnalyticsScoring.Normalize(x.RmRaw)).Where(x => x is not null).Distinct().Count() == 1;
                var stableLayer = obs.Select(x => AnalyticsScoring.Normalize(x.Layer)).Where(x => x is not null).Distinct().Count() == 1;
                var noteOverlap = AnalyticsScoring.ExtractNoteTokens(obs.Select(x => x.Note)).Count > 0;
                var distinctActions = obs.Select(x => x.ActionNorm).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                var score = AnalyticsScoring.CalculatePriorityScore(
                    obs.Count,
                    distinctActions,
                    stableRm,
                    stableLayer,
                    noteOverlap,
                    0,
                    hasOpenHypothesis: false,
                    recentActivity: obs.Any(x => x.ObservedDate >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))));

                var strongSignals = new List<string>();
                if (stableRm) strongSignals.Add("same-rm");
                if (stableLayer) strongSignals.Add("same-layer");
                if (noteOverlap) strongSignals.Add("note-context");
                if (distinctActions > 0 && distinctActions <= 3) strongSignals.Add("repeating-actions");

                return new AnalyticsClusterContextNodeDto(
                    g.Key.Display,
                    g.Key.Type,
                    g.Select(x => x.ObservationId).Distinct().Count(),
                    obs.Count == 0 ? null : obs.Max(x => (DateOnly?)x.ObservedDate),
                    [.. obs.GroupBy(x => x.ActionRaw)
                        .OrderByDescending(x => x.Count())
                        .ThenBy(x => x.Key)
                        .Take(3)
                        .Select(x => x.Key)],
                    strongSignals,
                    score);
            })
            .OrderByDescending(x => x.PriorityScore)
            .ThenByDescending(x => x.LastSeenDate)
            .ThenBy(x => x.DisplayName)];
    }
}