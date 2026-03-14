//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;
using Interception.UI.Application.Analytics.Dtos.DayPicture;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

public sealed class AnalyticsDayPictureService(IDbContextFactory<AppDbContext> dbFactory) : IAnalyticsDayPictureService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<AnalyticsDayPicturePageDto> GetPageAsync(DateOnly date, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var observations = await db.Observations
         .AsNoTracking()
         .Where(x => x.ObservedDate == date)
         .OrderBy(x => x.DayPart)
         .ThenBy(x => x.ActionRaw)
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

        var observationIds = observations.Select(x => x.Id).ToList();

        var participants = await db.ObservationParticipants
            .AsNoTracking()
            .Where(x => observationIds.Contains(x.ObservationId))
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
            .Where(x => observationIds.Contains(x.ObservationParticipant.ObservationId))
            .Select(x => new ResolutionRowDto(
                x.ObservationParticipantId,
                x.UnknownClusterId,
                x.UnknownCluster.Code,
                x.UnknownCluster.DisplayName,
                x.UnknownCluster.ResolvedActorId,
                x.UnknownCluster.ResolvedActor != null
                    ? x.UnknownCluster.ResolvedActor.DisplayName
                    : null))
            .ToListAsync(ct);

        var resolutionDict = resolutionMap
            .GroupBy(x => x.ObservationParticipantId)
            .ToDictionary(x => x.Key, x => x.First());

        var lines = BuildLines(observations, participants, resolutionDict);
        var topCandidates = await BuildTopCandidatesAsync(db, date, observations, participants, resolutionDict, ct);

        return new AnalyticsDayPicturePageDto(date, lines, topCandidates);
    }

    private static List<AnalyticsDayLineDto> BuildLines(
        IReadOnlyList<ObservationRowDto> observations,
        IReadOnlyList<ParticipantRowDto> participants,
        IReadOnlyDictionary<Guid, ResolutionRowDto> resolutionDict)
    {
        return [.. observations
            .GroupBy(x => BuildLineKey(x.ActionNorm, x.RmRaw, x.Layer, x.DistrictRaw, x.LocationRaw))
            .Select(g =>
            {
                var first = g.First();

                var cards = g
                    .OrderBy(x => x.DayPart)
                    .ThenBy(x => x.ActionRaw)
                    .Select(obs =>
                    {
                        var names = participants
                            .Where(p => p.ObservationId == obs.Id)
                            .OrderBy(p => p.Ordinal)
                            .Select(p => BuildParticipantDisplay(p, resolutionDict, obs.ObservedDate, obs.RmRaw))
                            .ToList();

                        return new AnalyticsDayObservationCardDto(
                            obs.Id,
                            obs.DayPart,
                            obs.ActionRaw,
                            obs.Layer,
                            obs.RmRaw,
                            obs.LocationRaw,
                            obs.DistrictRaw,
                            obs.Note,
                            names);
                    })
                    .ToList();

                var signals = new List<string>();
                if (!string.IsNullOrWhiteSpace(first.RmRaw)) signals.Add("same-rm");
                if (!string.IsNullOrWhiteSpace(first.Layer)) signals.Add("same-layer");
                if (!string.IsNullOrWhiteSpace(first.DistrictRaw) || !string.IsNullOrWhiteSpace(first.LocationRaw)) signals.Add("same-area");
                if (g.Count() > 1) signals.Add("repeating-action");

                return new AnalyticsDayLineDto(
                    g.Key,
                    BuildLineTitle(first.ActionRaw, first.RmRaw, first.Layer, first.DistrictRaw, first.LocationRaw),
                    signals,
                    cards);
            })
            .OrderByDescending(x => x.Observations.Count)
            .ThenBy(x => x.Title)];
    }

    private static async Task<List<AnalyticsDayCandidateDto>> BuildTopCandidatesAsync(
        AppDbContext db,
        DateOnly date,
        IReadOnlyList<ObservationRowDto> observations,
        IReadOnlyList<ParticipantRowDto> participants,
        IReadOnlyDictionary<Guid, ResolutionRowDto> resolutionDict,
        CancellationToken ct)
    {
        var items = new List<AnalyticsDayCandidateDto>();

        // 1. Open hypotheses that are present in this day
        var openClusters = await db.UnknownClusters
            .AsNoTracking()
            .Where(x => x.Status == "open")
            .Where(x => x.Members.Any(m => m.ObservationParticipant.Observation.ObservedDate == date))
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.DisplayName
            })
            .ToListAsync(ct);

        foreach (var cluster in openClusters)
        {
            var memberIds = await db.UnknownClusterMembers
                .AsNoTracking()
                .Where(x => x.UnknownClusterId == cluster.Id)
                .Select(x => x.ObservationParticipantId)
                .ToListAsync(ct);

            var memberParticipants = participants
                .Where(x => memberIds.Contains(x.Id))
                .ToList();

            if (memberParticipants.Count == 0)
                continue;

            var clusterObservations = observations
                .Where(x => memberParticipants.Select(mp => mp.ObservationId).Contains(x.Id))
                .ToList();

            var distinctActionsCount = clusterObservations
                .Select(x => x.ActionNorm)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var stableRm = HasStableSingleValue(clusterObservations.Select(x => x.RmRaw));
            var stableLayer = HasStableSingleValue(clusterObservations.Select(x => x.Layer));
            var hasNoteOverlap = AnalyticsScoring.ExtractNoteTokens(clusterObservations.Select(x => x.Note)).Count > 0;

            var relatedNodesCount = BuildRelatedNodeKeysForObservations(
                [.. clusterObservations.Select(x => x.Id)],
                [.. memberIds],
                participants,
                resolutionDict).Count;

            var lastSeen = clusterObservations.Max(x => (DateOnly?)x.ObservedDate);
            var recentActivity = lastSeen is not null &&
                                 lastSeen.Value >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

            var score = AnalyticsScoring.CalculatePriorityScore(
                clusterObservations.Count,
                distinctActionsCount,
                stableRm,
                stableLayer,
                hasNoteOverlap,
                relatedNodesCount,
                hasOpenHypothesis: true,
                recentActivity);

            var readiness = AnalyticsScoring.CalculateReadiness(
                clusterObservations.Count,
                distinctActionsCount,
                stableRm,
                stableLayer,
                hasNoteOverlap,
                hasOpenHypothesis: true,
                hasResolvedActor: false);

            var topSignals = BuildTopSignals(
                stableRm,
                stableLayer,
                hasNoteOverlap,
                relatedNodesCount,
                distinctActionsCount,
                clusterObservations.Count);

            items.Add(new AnalyticsDayCandidateDto(
                CandidateKey: $"cluster-{cluster.Id:N}",
                DisplayName: cluster.DisplayName ?? cluster.Code,
                PriorityScore: score,
                Readiness: readiness,
                ObservationsCount: clusterObservations.Count,
                TopSignals: topSignals,
                ClusterId: cluster.Id,
                ParticipantId: null));
        }

        // 2. Unresolved raw unknown participants for this day
        var unresolvedParticipants = participants
            .Where(x => x.IsUnknown && !resolutionDict.ContainsKey(x.Id))
            .ToList();

        foreach (var participant in unresolvedParticipants)
        {
            var seedObservation = observations.FirstOrDefault(x => x.Id == participant.ObservationId);
            if (seedObservation is null)
                continue;

            var contexts = observations
                .Where(x => x.Id == seedObservation.Id)
                .ToList();

            // same-day context expansion: action/rm/layer/district/note overlap
            var seedNoteTokens = AnalyticsScoring.ExtractNoteTokens([seedObservation.Note]);

            foreach (var observation in observations.Where(x => x.Id != seedObservation.Id))
            {
                var sameAction = string.Equals(observation.ActionNorm, seedObservation.ActionNorm, StringComparison.OrdinalIgnoreCase);
                var sameRm = SameNormalized(observation.RmRaw, seedObservation.RmRaw);
                var sameLayer = SameNormalized(observation.Layer, seedObservation.Layer);
                var sameDistrict = SameNormalized(observation.DistrictRaw, seedObservation.DistrictRaw)
                                   || SameNormalized(observation.LocationRaw, seedObservation.LocationRaw);
                var noteOverlap = AnalyticsScoring.HasNoteOverlap(observation.Note, seedNoteTokens);

                var scores  = AnalyticsScoring.CalculateContextScore(
                    sameAction,
                    sameRm,
                    sameLayer,
                    sameDistrict,
                    noteOverlap,
                    sameRelatedNode: false,
                    closeInTime: true);

                if (scores >= 25)
                    contexts.Add(observation);
            }

            var distinctActionsCount = contexts
                .Select(x => x.ActionNorm)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var stableRm = HasStableSingleValue(contexts.Select(x => x.RmRaw));
            var stableLayer = HasStableSingleValue(contexts.Select(x => x.Layer));
            var hasNoteOverlap = contexts.Count(x => AnalyticsScoring.HasNoteOverlap(x.Note, seedNoteTokens)) > 1;

            var relatedNodesCount = BuildRelatedNodeKeysForObservations(
                [.. contexts.Select(x => x.Id)],
                [participant.Id],
                participants,
                resolutionDict).Count;

            var lastSeen = contexts.Max(x => (DateOnly?)x.ObservedDate);
            var recentActivity = lastSeen is not null &&
                                 lastSeen.Value >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

            var score = AnalyticsScoring.CalculatePriorityScore(
                contexts.Count,
                distinctActionsCount,
                stableRm,
                stableLayer,
                hasNoteOverlap,
                relatedNodesCount,
                hasOpenHypothesis: false,
                recentActivity);

            var readiness = AnalyticsScoring.CalculateReadiness(
                contexts.Count,
                distinctActionsCount,
                stableRm,
                stableLayer,
                hasNoteOverlap,
                hasOpenHypothesis: false,
                hasResolvedActor: false);

            var topSignals = BuildTopSignals(
                stableRm,
                stableLayer,
                hasNoteOverlap,
                relatedNodesCount,
                distinctActionsCount,
                contexts.Count);

            items.Add(new AnalyticsDayCandidateDto(
                CandidateKey: $"participant-{participant.Id:N}",
                DisplayName: AnalyticsScoring.BuildUnknownDisplay(
                    participant.LabelRaw,
                    participant.Ordinal,
                    seedObservation.ObservedDate,
                    seedObservation.RmRaw),
                PriorityScore: score,
                Readiness: readiness,
                ObservationsCount: contexts.Count,
                TopSignals: topSignals,
                ClusterId: null,
                ParticipantId: participant.Id));
        }

        return [.. items
            .OrderByDescending(x => x.PriorityScore)
            .ThenByDescending(x => x.ObservationsCount)
            .ThenBy(x => x.DisplayName)
            .Take(10)];
    }

    private static HashSet<string> BuildRelatedNodeKeysForObservations(
        HashSet<Guid> observationIds,
        HashSet<Guid> excludedParticipantIds,
        IReadOnlyList<ParticipantRowDto> participants,
        IReadOnlyDictionary<Guid, ResolutionRowDto> resolutionDict)
    {
        return participants
            .Where(x => observationIds.Contains(x.ObservationId) && !excludedParticipantIds.Contains(x.Id))
            .Select(x => BuildNodeKey(x, resolutionDict))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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

    private static string BuildLineKey(
        string actionNorm,
        string? rmRaw,
        string? layer,
        string? districtRaw,
        string? locationRaw)
    {
        return string.Join("|",
            actionNorm ?? string.Empty,
            AnalyticsScoring.Normalize(rmRaw) ?? string.Empty,
            AnalyticsScoring.Normalize(layer) ?? string.Empty,
            AnalyticsScoring.Normalize(districtRaw) ?? string.Empty,
            AnalyticsScoring.Normalize(locationRaw) ?? string.Empty);
    }

    private static string BuildLineTitle(
        string actionRaw,
        string? rmRaw,
        string? layer,
        string? districtRaw,
        string? locationRaw)
    {
        var context = rmRaw ?? layer ?? districtRaw ?? locationRaw;
        return string.IsNullOrWhiteSpace(context)
            ? actionRaw
            : $"{context} / {actionRaw}";
    }

    private static string BuildParticipantDisplay(
        ParticipantRowDto participant,
        IReadOnlyDictionary<Guid, ResolutionRowDto> resolutionDict,
        DateOnly observedDate,
        string? rmRaw)
    {
        if (resolutionDict.TryGetValue(participant.Id, out var resolution))
        {
            if (resolution.ResolvedActorId is not null)
                return resolution.ResolvedActorDisplayName ?? resolution.ClusterDisplayName ?? resolution.ClusterCode;

            return resolution.ClusterDisplayName ?? resolution.ClusterCode;
        }

        if (participant.IsUnknown)
        {
            return AnalyticsScoring.BuildUnknownDisplay(
                participant.LabelRaw,
                participant.Ordinal,
                observedDate,
                rmRaw);
        }

        return participant.LabelRaw ?? participant.LabelNorm ?? $"known:{participant.Id:N}";
    }

    private static string BuildNodeKey(
        ParticipantRowDto participant,
        IReadOnlyDictionary<Guid, ResolutionRowDto> resolutionMap)
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

    private static bool SameNormalized(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
           && !string.IsNullOrWhiteSpace(right)
           && string.Equals(
               AnalyticsScoring.Normalize(left),
               AnalyticsScoring.Normalize(right),
               StringComparison.OrdinalIgnoreCase);

    private static bool HasStableSingleValue(IEnumerable<string?> values)
    {
        var normalized = values
            .Select(AnalyticsScoring.Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count == 1 && normalized.Count > 0;
    }

    private sealed record ObservationRowDto(
        Guid Id,
        DateOnly ObservedDate,
        short DayPart,
        string ActionRaw,
        string ActionNorm,
        string? Layer,
        string? RmRaw,
        string? LocationRaw,
        string? DistrictRaw,
        string? Note);

    private sealed record ParticipantRowDto(
        Guid Id,
        Guid ObservationId,
        int Ordinal,
        string? LabelRaw,
        string? LabelNorm,
        bool IsUnknown,
        string? RoleRaw);

    private sealed record ResolutionRowDto(
        Guid ObservationParticipantId,
        Guid ClusterId,
        string ClusterCode,
        string? ClusterDisplayName,
        Guid? ResolvedActorId,
        string? ResolvedActorDisplayName);
}