//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

public sealed partial class AnalyticsCandidateService(IDbContextFactory<AppDbContext> dbFactory) : IAnalyticsCandidateService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<AnalyticsCandidatePageDto> GetPageAsync(AnalyticsCandidateFilterDto filter, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var observations = await LoadObservationsAsync(db, ct);
        var participants = await LoadParticipantsAsync(db, ct);
        var resolutionMap = await LoadResolutionMapAsync(db, ct);

        var clusterItems = await BuildOpenClusterCandidatesAsync(db, observations, participants, resolutionMap, ct);
        var orphanItems = BuildOrphanUnknownCandidates(observations, participants, resolutionMap);

        var all = clusterItems
            .Concat(orphanItems)
            .ToList();

        all = ApplyFilter(all, filter);

        var total = all.Count;

        var items = all
            .OrderByDescending(x => x.PriorityScore)
            .ThenByDescending(x => x.LastSeenDate)
            .ThenBy(x => x.DisplayName)
            .Skip(Math.Max(0, filter.Skip))
            .Take(Math.Clamp(filter.Take, 1, 200))
            .ToList();

        return new AnalyticsCandidatePageDto(total, items);
    }

    public async Task<AnalyticsCandidateDetailsDto?> GetDetailsAsync(string candidateKey, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var observations = await LoadObservationsAsync(db, ct);
        var participants = await LoadParticipantsAsync(db, ct);
        var resolutionMap = await LoadResolutionMapAsync(db, ct);

        if (TryParseClusterKey(candidateKey, out var clusterId))
            return await BuildClusterDetailsAsync(db, clusterId, observations, participants, resolutionMap, ct);

        if (TryParseParticipantKey(candidateKey, out var participantId))
            return BuildParticipantDetails(participantId, observations, participants, resolutionMap);

        return null;
    }

    private static List<AnalyticsCandidateItemDto> ApplyFilter(
        List<AnalyticsCandidateItemDto> items,
        AnalyticsCandidateFilterDto filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var query = filter.Query.Trim();
            items = [.. items
                .Where(x =>
                    x.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (x.PrimaryRole?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (x.ClusterCode?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    x.TopActions.Any(a => a.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    x.TopSignals.Any(s => s.Contains(query, StringComparison.OrdinalIgnoreCase)))];
        }

        if (!string.IsNullOrWhiteSpace(filter.CandidateType))
        {
            items = [.. items.Where(x => string.Equals(x.CandidateType, filter.CandidateType, StringComparison.OrdinalIgnoreCase))];
        }

        if (filter.OnlyWithoutHypothesis)
        {
            items = [.. items.Where(x => !x.HasOpenHypothesis)];
        }

        if (filter.DateFrom is not null)
        {
            items = [.. items.Where(x => x.LastSeenDate is not null && x.LastSeenDate.Value >= filter.DateFrom.Value)];
        }

        if (filter.DateTo is not null)
        {
            items = [.. items.Where(x => x.LastSeenDate is not null && x.LastSeenDate.Value <= filter.DateTo.Value)];
        }

        return items;
    }

    private static async Task<List<AnalyticsCandidateItemDto>> BuildOpenClusterCandidatesAsync(
        AppDbContext db,
        IReadOnlyList<ObservationRowDto> observations,
        IReadOnlyList<ParticipantRowDto> participants,
        IReadOnlyDictionary<Guid, ResolutionRowDto> resolutionMap,
        CancellationToken ct)
    {
        var clusters = await db.UnknownClusters
            .AsNoTracking()
            .Where(x => x.Status == "open")
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.DisplayName
            })
            .ToListAsync(ct);

        var items = new List<AnalyticsCandidateItemDto>();

        foreach (var cluster in clusters)
        {
            var memberIds = await db.UnknownClusterMembers
                .AsNoTracking()
                .Where(x => x.UnknownClusterId == cluster.Id)
                .Select(x => x.ObservationParticipantId)
                .ToListAsync(ct);

            if (memberIds.Count == 0)
                continue;

            var memberParticipants = participants
                .Where(x => memberIds.Contains(x.Id))
                .ToList();

            var directObservationIds = memberParticipants
                .Select(x => x.ObservationId)
                .Distinct()
                .ToList();

            var directObservations = observations
                .Where(x => directObservationIds.Contains(x.Id))
                .ToList();

            var topActions = directObservations
                .GroupBy(x => x.ActionRaw)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key)
                .Take(3)
                .Select(x => x.Key)
                .ToList();

            var distinctActionsCount = directObservations
                .Select(x => x.ActionNorm)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var stableRm = HasStableSingleValue(directObservations.Select(x => x.RmRaw));
            var stableLayer = HasStableSingleValue(directObservations.Select(x => x.Layer));
            var hasNoteOverlap = AnalyticsScoring.ExtractNoteTokens(directObservations.Select(x => x.Note)).Count > 0;

            var relatedNodes = BuildRelatedNodes(
                directObservationIds,
                [.. memberIds],
                observations,
                participants,
                resolutionMap);

            var lastSeen = directObservations.Count == 0
                ? null
                : directObservations.Max(x => (DateOnly?)x.ObservedDate);

            var recentActivity = lastSeen is not null &&
                                 lastSeen.Value >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

            var score = AnalyticsScoring.CalculatePriorityScore(
                directObservations.Count,
                distinctActionsCount,
                stableRm,
                stableLayer,
                hasNoteOverlap,
                relatedNodes.Count,
                hasOpenHypothesis: true,
                recentActivity);

            var topSignals = BuildTopSignals(
                stableRm,
                stableLayer,
                hasNoteOverlap,
                relatedNodes.Count,
                distinctActionsCount,
                directObservations.Count);

            var readiness = AnalyticsScoring.CalculateReadiness(
                directObservations.Count,
                distinctActionsCount,
                stableRm,
                stableLayer,
                hasNoteOverlap,
                hasOpenHypothesis: true,
                hasResolvedActor: false);

            items.Add(new AnalyticsCandidateItemDto(
                BuildClusterKey(cluster.Id),
                cluster.DisplayName ?? cluster.Code,
                "open-hypothesis",
                score,
                AnalyticsScoring.ToPriorityBand(score),
                readiness,
                directObservations.Count,
                distinctActionsCount,
                memberParticipants
                    .Select(x => x.RoleRaw)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .GroupBy(x => x!)
                    .OrderByDescending(x => x.Count())
                    .ThenBy(x => x.Key)
                    .Select(x => x.Key)
                    .FirstOrDefault(),
                lastSeen,
                true,
                cluster.Id,
                cluster.Code,
                null,
                directObservations
                    .OrderByDescending(x => x.ObservedDate)
                    .ThenByDescending(x => x.DayPart)
                    .Select(x => (Guid?)x.Id)
                    .FirstOrDefault(),
                topActions,
                topSignals,
                "Відкрити припущення"));
        }

        return items;
    }

    private static AnalyticsCandidateDetailsDto? BuildParticipantDetails(
        Guid participantId,
        IReadOnlyList<ObservationRowDto> observations,
        IReadOnlyList<ParticipantRowDto> participants,
        IReadOnlyDictionary<Guid, ResolutionRowDto> resolutionMap)
    {
        var participant = participants.FirstOrDefault(x => x.Id == participantId);
        if (participant is null)
            return null;

        var seedObservation = observations.FirstOrDefault(x => x.Id == participant.ObservationId);
        if (seedObservation is null)
            return null;

        var seedObservationParticipants = participants
            .Where(x => x.ObservationId == seedObservation.Id && x.Id != participantId)
            .ToList();

        var seedNodeKeys = seedObservationParticipants
            .Select(x => BuildNodeKey(x, resolutionMap))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seedNoteTokens = AnalyticsScoring.ExtractNoteTokens([seedObservation.Note]);

        var candidateObservations = new List<AnalyticsCandidateObservationDto>
        {
            new(
                seedObservation.Id,
                seedObservation.ObservedDate,
                seedObservation.DayPart,
                seedObservation.ActionRaw,
                seedObservation.Layer,
                seedObservation.RmRaw,
                seedObservation.LocationRaw,
                seedObservation.DistrictRaw,
                seedObservation.Note,
                true,
                100,
                ["direct participant"])
        };

        foreach (var observation in observations.Where(x => x.Id != seedObservation.Id))
        {
            var sameAction = string.Equals(observation.ActionNorm, seedObservation.ActionNorm, StringComparison.OrdinalIgnoreCase);
            var sameRm = SameNormalized(observation.RmRaw, seedObservation.RmRaw);
            var sameLayer = SameNormalized(observation.Layer, seedObservation.Layer);
            var sameDistrict = SameNormalized(observation.DistrictRaw, seedObservation.DistrictRaw)
                               || SameNormalized(observation.LocationRaw, seedObservation.LocationRaw);
            var noteOverlap = AnalyticsScoring.HasNoteOverlap(observation.Note, seedNoteTokens);
            var closeInTime = Math.Abs(observation.ObservedDate.DayNumber - seedObservation.ObservedDate.DayNumber) <= 3;

            var observationNodeKeys = participants
                .Where(x => x.ObservationId == observation.Id)
                .Select(x => BuildNodeKey(x, resolutionMap))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sameRelatedNode = observationNodeKeys.Overlaps(seedNodeKeys);

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

            candidateObservations.Add(new AnalyticsCandidateObservationDto(
                observation.Id,
                observation.ObservedDate,
                observation.DayPart,
                observation.ActionRaw,
                observation.Layer,
                observation.RmRaw,
                observation.LocationRaw,
                observation.DistrictRaw,
                observation.Note,
                false,
                score,
                BuildMatchedSignals(sameAction, sameRm, sameLayer, sameDistrict, noteOverlap, sameRelatedNode, closeInTime)));
        }

        var orderedObservations = candidateObservations
            .OrderByDescending(x => x.DirectMatch)
            .ThenByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.ObservedDate)
            .ToList();

        var relatedNodes = BuildRelatedNodes(
            [.. orderedObservations.Select(x => x.ObservationId).Distinct()],
            [participantId],
            observations,
            participants,
            resolutionMap);

        var distinctActionsCount = orderedObservations
            .Select(x => AnalyticsScoring.Normalize(x.ActionRaw))
            .Where(x => x is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var stableRm = HasStableSingleValue(orderedObservations.Select(x => x.RmRaw));
        var stableLayer = HasStableSingleValue(orderedObservations.Select(x => x.Layer));
        var hasNoteOverlap = orderedObservations.Any(x => x.MatchedSignals.Any(s => s == "note-context"));
        var lastSeen = orderedObservations.Max(x => (DateOnly?)x.ObservedDate);
        var recentActivity = lastSeen is not null &&
                             lastSeen.Value >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        var scoreValue = AnalyticsScoring.CalculatePriorityScore(
            orderedObservations.Count,
            distinctActionsCount,
            stableRm,
            stableLayer,
            hasNoteOverlap,
            relatedNodes.Count,
            hasOpenHypothesis: false,
            recentActivity);

        var signals = BuildSignalDtos(
            orderedObservations.Count,
            distinctActionsCount,
            stableRm,
            stableLayer,
            hasNoteOverlap,
            relatedNodes.Count,
            hasOpenHypothesis: false,
            recentActivity);

        var readiness = AnalyticsScoring.CalculateReadiness(
            orderedObservations.Count,
            distinctActionsCount,
            stableRm,
            stableLayer,
            hasNoteOverlap,
            hasOpenHypothesis: true,
            hasResolvedActor: false);

        return new AnalyticsCandidateDetailsDto(
            BuildParticipantKey(participantId),
            AnalyticsScoring.BuildUnknownDisplay(
                participant.LabelRaw,
                participant.Ordinal,
                seedObservation.ObservedDate,
                seedObservation.RmRaw),
            "unresolved-unknown",
            scoreValue,
            AnalyticsScoring.ToPriorityBand(scoreValue),
            readiness,
            orderedObservations.Count,
            distinctActionsCount,
            participant.RoleRaw,
            lastSeen,
            null,
            null,
            participant.Id,
            participant.ObservationId,
            orderedObservations,
            signals,
            relatedNodes,
            ["Відкрити аналітику учасника", "Створити припущення"]);
    }

    private static async Task<AnalyticsCandidateDetailsDto?> BuildClusterDetailsAsync(
        AppDbContext db,
        Guid clusterId,
        IReadOnlyList<ObservationRowDto> observations,
        IReadOnlyList<ParticipantRowDto> participants,
        IReadOnlyDictionary<Guid, ResolutionRowDto> resolutionMap,
        CancellationToken ct)
    {
        var cluster = await db.UnknownClusters
            .AsNoTracking()
            .Where(x => x.Id == clusterId)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.DisplayName,
                x.Status
            })
            .FirstOrDefaultAsync(ct);

        if (cluster is null)
            return null;

        var memberIds = await db.UnknownClusterMembers
            .AsNoTracking()
            .Where(x => x.UnknownClusterId == clusterId)
            .Select(x => x.ObservationParticipantId)
            .ToListAsync(ct);

        if (memberIds.Count == 0)
            return null;

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
            .Select(x => new AnalyticsCandidateObservationDto(
                x.Id,
                x.ObservedDate,
                x.DayPart,
                x.ActionRaw,
                x.Layer,
                x.RmRaw,
                x.LocationRaw,
                x.DistrictRaw,
                x.Note,
                true,
                100,
                ["member of hypothesis"]))
            .ToList();

        var relatedNodes = BuildRelatedNodes(
            directObservationIds,
            [.. memberIds],
            observations,
            participants,
            resolutionMap);

        var distinctActionsCount = directObservations
            .Select(x => AnalyticsScoring.Normalize(x.ActionRaw))
            .Where(x => x is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var stableRm = HasStableSingleValue(directObservations.Select(x => x.RmRaw));
        var stableLayer = HasStableSingleValue(directObservations.Select(x => x.Layer));
        var hasNoteOverlap = AnalyticsScoring.ExtractNoteTokens(directObservations.Select(x => x.Note)).Count > 0;
        var lastSeen = directObservations.Max(x => (DateOnly?)x.ObservedDate);
        var recentActivity = lastSeen is not null &&
                             lastSeen.Value >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        var score = AnalyticsScoring.CalculatePriorityScore(
            directObservations.Count,
            distinctActionsCount,
            stableRm,
            stableLayer,
            hasNoteOverlap,
            relatedNodes.Count,
            hasOpenHypothesis: true,
            recentActivity);

        var signals = BuildSignalDtos(
            directObservations.Count,
            distinctActionsCount,
            stableRm,
            stableLayer,
            hasNoteOverlap,
            relatedNodes.Count,
            hasOpenHypothesis: true,
            recentActivity);

        var readiness = AnalyticsScoring.CalculateReadiness(
            directObservations.Count,
            distinctActionsCount,
            stableRm,
            stableLayer,
            hasNoteOverlap,
            hasOpenHypothesis: true,
            hasResolvedActor: false);

        return new AnalyticsCandidateDetailsDto(
            BuildClusterKey(cluster.Id),
            cluster.DisplayName ?? cluster.Code,
            "open-hypothesis",
            score,
            AnalyticsScoring.ToPriorityBand(score),
            readiness,
            directObservations.Count,
            distinctActionsCount,
            memberParticipants
                .Select(x => x.RoleRaw)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .GroupBy(x => x!)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key)
                .Select(x => x.Key)
                .FirstOrDefault(),
            lastSeen,
            cluster.Id,
            cluster.Code,
            null,
            directObservationIds.FirstOrDefault(),
            directObservations,
            signals,
            relatedNodes,
            ["Відкрити гіпотезу", "Відкрити контекст"]);
    }

    private static List<AnalyticsCandidateItemDto> BuildOrphanUnknownCandidates(
        IReadOnlyList<ObservationRowDto> observations,
        IReadOnlyList<ParticipantRowDto> participants,
        IReadOnlyDictionary<Guid, ResolutionRowDto> resolutionMap)
    {
        var orphanParticipants = participants
            .Where(x => x.IsUnknown && !resolutionMap.ContainsKey(x.Id))
            .ToList();

        var items = new List<AnalyticsCandidateItemDto>();

        foreach (var participant in orphanParticipants)
        {
            var seedObservation = observations.FirstOrDefault(x => x.Id == participant.ObservationId);
            if (seedObservation is null)
                continue;

            var seedObservationParticipants = participants
                .Where(x => x.ObservationId == seedObservation.Id && x.Id != participant.Id)
                .ToList();

            var seedNodeKeys = seedObservationParticipants
                .Select(x => BuildNodeKey(x, resolutionMap))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var seedNoteTokens = AnalyticsScoring.ExtractNoteTokens([seedObservation.Note]);

            var contexts = new List<ObservationRowDto> { seedObservation };

            foreach (var observation in observations.Where(x => x.Id != seedObservation.Id))
            {
                var sameAction = string.Equals(observation.ActionNorm, seedObservation.ActionNorm, StringComparison.OrdinalIgnoreCase);
                var sameRm = SameNormalized(observation.RmRaw, seedObservation.RmRaw);
                var sameLayer = SameNormalized(observation.Layer, seedObservation.Layer);
                var sameDistrict = SameNormalized(observation.DistrictRaw, seedObservation.DistrictRaw)
                                   || SameNormalized(observation.LocationRaw, seedObservation.LocationRaw);
                var noteOverlap = AnalyticsScoring.HasNoteOverlap(observation.Note, seedNoteTokens);
                var closeInTime = Math.Abs(observation.ObservedDate.DayNumber - seedObservation.ObservedDate.DayNumber) <= 3;

                var observationNodeKeys = participants
                    .Where(x => x.ObservationId == observation.Id)
                    .Select(x => BuildNodeKey(x, resolutionMap))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var sameRelatedNode = observationNodeKeys.Overlaps(seedNodeKeys);

                var scores = AnalyticsScoring.CalculateContextScore(
                    sameAction,
                    sameRm,
                    sameLayer,
                    sameDistrict,
                    noteOverlap,
                    sameRelatedNode,
                    closeInTime);

                if (scores >= 25)
                    contexts.Add(observation);
            }

            var relatedNodes = BuildRelatedNodes(
                [.. contexts.Select(x => x.Id).Distinct()],
                [participant.Id],
                observations,
                participants,
                resolutionMap);

            var distinctActionsCount = contexts
                .Select(x => x.ActionNorm)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var stableRm = HasStableSingleValue(contexts.Select(x => x.RmRaw));
            var stableLayer = HasStableSingleValue(contexts.Select(x => x.Layer));
            var hasNoteOverlap = contexts.Count(x => AnalyticsScoring.HasNoteOverlap(x.Note, seedNoteTokens)) > 1;
            var lastSeen = contexts.Max(x => (DateOnly?)x.ObservedDate);
            var recentActivity = lastSeen is not null &&
                                 lastSeen.Value >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

            var score = AnalyticsScoring.CalculatePriorityScore(
                contexts.Count,
                distinctActionsCount,
                stableRm,
                stableLayer,
                hasNoteOverlap,
                relatedNodes.Count,
                hasOpenHypothesis: false,
                recentActivity);

            var topActions = contexts
                .GroupBy(x => x.ActionRaw)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key)
                .Take(3)
                .Select(x => x.Key)
                .ToList();

            var topSignals = BuildTopSignals(
                stableRm,
                stableLayer,
                hasNoteOverlap,
                relatedNodes.Count,
                distinctActionsCount,
                contexts.Count);

            var readiness = AnalyticsScoring.CalculateReadiness(
                contexts.Count,
                distinctActionsCount,
                stableRm,
                stableLayer,
                hasNoteOverlap,
                hasOpenHypothesis: true,
                hasResolvedActor: false);

            items.Add(new AnalyticsCandidateItemDto(
                BuildParticipantKey(participant.Id),
                AnalyticsScoring.BuildUnknownDisplay(
                    participant.LabelRaw,
                    participant.Ordinal,
                    seedObservation.ObservedDate,
                    seedObservation.RmRaw),
                "unresolved-unknown",
                score,
                AnalyticsScoring.ToPriorityBand(score),
                readiness,
                contexts.Count,
                distinctActionsCount,
                participant.RoleRaw,
                lastSeen,
                false,
                null,
                null,
                participant.Id,
                participant.ObservationId,
                topActions,
                topSignals,
                contexts.Count >= 3 ? "Створити припущення" : "Поки спостерігати"));
        }

        return items;
    }

    private static List<AnalyticsCandidateSignalDto> BuildSignalDtos(
        int observationsCount,
        int distinctActionsCount,
        bool stableRm,
        bool stableLayer,
        bool hasNoteOverlap,
        int relatedNodesCount,
        bool hasOpenHypothesis,
        bool recentActivity)
    {
        var list = new List<AnalyticsCandidateSignalDto>
        {
            new("observations", observationsCount * 5, $"Observation у кейсі: {observationsCount}")
        };

        if (distinctActionsCount > 0 && distinctActionsCount <= 3)
            list.Add(new("repeating-actions", 15, $"Невеликий стабільний набір дій: {distinctActionsCount}"));

        if (stableRm)
            list.Add(new("same-rm", 10, "Стабільний Р/М"));

        if (stableLayer)
            list.Add(new("same-layer", 10, "Стабільний шар"));

        if (hasNoteOverlap)
            list.Add(new("note-context", 10, "Є повторюваний note-context"));

        if (relatedNodesCount >= 3)
            list.Add(new("related-nodes", 15, $"Пов’язаних вузлів: {relatedNodesCount}"));

        if (!hasOpenHypothesis)
            list.Add(new("without-hypothesis", 20, "Ще немає відкритої гіпотези"));

        if (recentActivity)
            list.Add(new("recent", 10, "Є недавня активність"));

        return [.. list.OrderByDescending(x => x.Weight)];
    }

    private static List<string> BuildTopSignals(
        bool stableRm,
        bool stableLayer,
        bool hasNoteOverlap,
        int relatedNodesCount,
        int distinctActionsCount,
        int observationsCount)
    {
        var list = new List<string>();

        if (observationsCount >= 3) list.Add("observation-volume");
        if (distinctActionsCount > 0 && distinctActionsCount <= 3) list.Add("repeating-actions");
        if (stableRm) list.Add("same-rm");
        if (stableLayer) list.Add("same-layer");
        if (hasNoteOverlap) list.Add("note-context");
        if (relatedNodesCount >= 3) list.Add("related-nodes");

        return list;
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

    private static bool SameNormalized(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
           && !string.IsNullOrWhiteSpace(right)
           && string.Equals(AnalyticsScoring.Normalize(left), AnalyticsScoring.Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static bool HasStableSingleValue(IEnumerable<string?> values)
    {
        var normalized = values
            .Select(AnalyticsScoring.Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count == 1 && normalized.Count > 0;
    }

    private static string BuildClusterKey(Guid clusterId) => $"cluster-{clusterId:N}";
    private static string BuildParticipantKey(Guid participantId) => $"participant-{participantId:N}";

    private static bool TryParseClusterKey(string key, out Guid clusterId)
    {
        clusterId = Guid.Empty;
        if (!key.StartsWith("cluster-", StringComparison.OrdinalIgnoreCase))
            return false;

        return Guid.TryParseExact(key["cluster-".Length..], "N", out clusterId);
    }

    private static bool TryParseParticipantKey(string key, out Guid participantId)
    {
        participantId = Guid.Empty;
        if (!key.StartsWith("participant-", StringComparison.OrdinalIgnoreCase))
            return false;

        return Guid.TryParseExact(key["participant-".Length..], "N", out participantId);
    }

    private static string BuildNodeKey(ParticipantRowDto participant, IReadOnlyDictionary<Guid, ResolutionRowDto> resolutionMap)
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

    private static List<AnalyticsCandidateNodeDto> BuildRelatedNodes(
        IReadOnlyCollection<Guid> observationIds,
        HashSet<Guid> excludedParticipantIds,
        IReadOnlyList<ObservationRowDto> observations,
        IReadOnlyList<ParticipantRowDto> participants,
        IReadOnlyDictionary<Guid, ResolutionRowDto> resolutionMap)
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
                    var obs = observations.FirstOrDefault(o => o.Id == x.ObservationId);
                    display = AnalyticsScoring.BuildUnknownDisplay(
                        x.LabelRaw,
                        x.Ordinal,
                        obs?.ObservedDate,
                        obs?.RmRaw);
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

                return new AnalyticsCandidateNodeDto(
                    g.Key.Display,
                    g.Key.Type,
                    g.Select(x => x.ObservationId).Distinct().Count(),
                    obs.Count == 0 ? null : obs.Max(x => (DateOnly?)x.ObservedDate),
                    [.. obs.GroupBy(x => x.ActionRaw)
                        .OrderByDescending(x => x.Count())
                        .ThenBy(x => x.Key)
                        .Take(3)
                        .Select(x => x.Key)],
                    []);
            })
            .OrderByDescending(x => x.SeenCount)
            .ThenByDescending(x => x.LastSeenDate)
            .ThenBy(x => x.DisplayName)];
    }

    private static async Task<Dictionary<Guid, ResolutionRowDto>> LoadResolutionMapAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.UnknownClusterMembers
            .AsNoTracking()
            .Select(x => new ResolutionRowDto(
                x.ObservationParticipantId,
                x.UnknownClusterId,
                x.UnknownCluster.Code,
                x.UnknownCluster.DisplayName,
                x.UnknownCluster.ResolvedActorId,
                x.UnknownCluster.ResolvedActor != null ? x.UnknownCluster.ResolvedActor.DisplayName : null))
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.ObservationParticipantId)
            .ToDictionary(x => x.Key, x => x.First());
    }

    private static Task<List<ObservationRowDto>> LoadObservationsAsync(AppDbContext db, CancellationToken ct)
        => db.Observations
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

    private static Task<List<ParticipantRowDto>> LoadParticipantsAsync(AppDbContext db, CancellationToken ct)
        => db.ObservationParticipants
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
}