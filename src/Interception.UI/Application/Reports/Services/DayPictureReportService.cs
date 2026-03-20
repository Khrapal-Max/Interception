//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Reports.Abstractions;
using Interception.UI.Application.Reports.Dtos;
using Interception.UI.Domain.Enums;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Reports.Services;

/// <summary>
/// Builds a day picture by finding logical links between observations inside one day.
/// Links are calculated from shared subdivision hints, actor links, tags, action types and location context.
/// </summary>
public sealed class DayPictureReportService(IDbContextFactory<AppDbContext> dbFactory) : IDayPictureReportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<DayPictureReportDto> BuildAsync(DayPictureFilterDto filter, CancellationToken ct)
    {
        var dayStart = filter.Day.ToDateTime(TimeOnly.MinValue);
        var dayEndExclusive = filter.Day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var maxObservations = Math.Clamp(filter.MaxObservations, 1, 1000);
        var minLinkScore = Math.Clamp(Convert.ToInt16(filter.MinLinkScore), Convert.ToInt16(1), Convert.ToInt16(20));
        var queryText = (filter.Query ?? string.Empty).Trim();
        var layerFilter = Normalize(filter.Layer);
        var rmFilter = Normalize(filter.RmRaw);
        var queryNorm = Normalize(queryText);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var observationRows = await db.Observations
            .AsNoTracking()
            .Where(x => x.ObservedDate >= dayStart && x.ObservedDate < dayEndExclusive)
            .OrderBy(x => x.ObservedDate)
            .ThenBy(x => x.CreatedAtUtc)
            .Select(x => new ObservationRow(
                x.Id,
                x.ObservedDate,
                x.ActionRaw,
                x.ActionNorm,
                x.ObservationActionId,
                x.ObservationAction != null ? x.ObservationAction.Name : null,
                x.Layer,
                x.RmRaw,
                x.LocationRaw,
                x.DistrictRaw,
                x.SubdivisionRaw,
                x.SubdivisionNorm,
                x.Note))
            .ToListAsync(ct);

        var filteredRows = observationRows
            .Where(x => layerFilter is null || Normalize(x.Layer) == layerFilter)
            .Where(x => rmFilter is null || Normalize(x.RmRaw) == rmFilter)
            .Where(x => queryNorm is null || MatchesQuery(x, queryNorm))
            .Take(maxObservations)
            .ToList();

        if (filteredRows.Count == 0)
            return new DayPictureReportDto(filter.Day, DateTime.UtcNow, 0, 0, []);

        var observationIds = filteredRows.Select(x => x.Id).ToList();

        var participants = await db.ObservationParticipants
            .AsNoTracking()
            .Where(x => observationIds.Contains(x.ObservationId))
            .OrderBy(x => x.ObservationId)
            .ThenBy(x => x.Ordinal)
            .Select(x => new ParticipantRow(
                x.Id,
                x.ObservationId,
                x.LabelRaw,
                x.LabelNorm,
                x.IsUnknown,
                x.RoleRaw,
                x.Ordinal))
            .ToListAsync(ct);

        var tags = await db.ObservationTags
            .AsNoTracking()
            .Where(x => observationIds.Contains(x.ObservationId))
            .Select(x => new TagRow(
                x.ObservationId,
                x.RawValue,
                x.RawValueNorm,
                x.Kind))
            .ToListAsync(ct);

        var probableActions = await db.ObservationProbableActions
            .AsNoTracking()
            .Where(x => observationIds.Contains(x.ObservationId))
            .Select(x => new ProbableActionRow(
                x.ObservationId,
                x.ObservationActionId,
                x.ObservationAction.Name,
                x.Confidence))
            .ToListAsync(ct);

        var participantIds = participants.Select(x => x.Id).ToList();

        var actorLinks = participantIds.Count == 0
            ? []
            : await db.UnknownClusterMembers
                .AsNoTracking()
                .Where(x => participantIds.Contains(x.ObservationParticipantId))
                .Select(x => new ActorLinkRow(
                    x.ObservationParticipantId,
                    x.UnknownCluster.Title,
                    x.UnknownCluster.ResolvedActorId,
                    x.UnknownCluster.ResolvedActor != null ? x.UnknownCluster.ResolvedActor.DisplayName : null))
                .ToListAsync(ct);

        var subdivisionLinks = await db.UnknownSubdivisionObservations
            .AsNoTracking()
            .Where(x => observationIds.Contains(x.ObservationId))
            .Select(x => new SubdivisionLinkRow(
                x.ObservationId,
                x.UnknownSubdivisionClusterId,
                x.UnknownSubdivisionCluster.LabelRaw,
                x.UnknownSubdivisionCluster.ResolvedSubdivisionId,
                x.UnknownSubdivisionCluster.ResolvedSubdivision != null ? x.UnknownSubdivisionCluster.ResolvedSubdivision.Name : null))
            .ToListAsync(ct);

        var actorLinkLookup = actorLinks
            .GroupBy(x => x.ObservationParticipantId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var subdivisionLookup = subdivisionLinks
            .GroupBy(x => x.ObservationId)
            .ToDictionary(x => x.Key, x => x.First());

        var participantLookup = participants
            .GroupBy(x => x.ObservationId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var tagLookup = tags
            .GroupBy(x => x.ObservationId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var probableLookup = probableActions
            .GroupBy(x => x.ObservationId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var nodes = filteredRows
            .Select(row => BuildNode(
                row,
                participantLookup.GetValueOrDefault(row.Id) ?? [],
                actorLinkLookup,
                tagLookup.GetValueOrDefault(row.Id) ?? [],
                probableLookup.GetValueOrDefault(row.Id) ?? [],
                subdivisionLookup.GetValueOrDefault(row.Id)))
            .OrderBy(x => x.ObservedDate)
            .ToList();

        var links = BuildLinks(nodes, minLinkScore);
        var threads = BuildThreads(nodes, links);

        return new DayPictureReportDto(
            filter.Day,
            DateTime.UtcNow,
            nodes.Count,
            threads.Count,
            threads);
    }

    private static bool MatchesQuery(ObservationRow row, string queryNorm)
    {
        return Contains(row.ActionNorm, queryNorm)
            || Contains(row.Layer, queryNorm)
            || Contains(row.RmRaw, queryNorm)
            || Contains(row.LocationRaw, queryNorm)
            || Contains(row.DistrictRaw, queryNorm)
            || Contains(row.SubdivisionRaw, queryNorm)
            || Contains(row.Note, queryNorm)
            || Contains(row.ObservationActionName, queryNorm);
    }

    private static DayPictureNode BuildNode(
        ObservationRow row,
        IReadOnlyList<ParticipantRow> participants,
        IReadOnlyDictionary<Guid, List<ActorLinkRow>> actorLinkLookup,
        IReadOnlyList<TagRow> tags,
        IReadOnlyList<ProbableActionRow> probableActions,
        SubdivisionLinkRow? subdivisionLink)
    {
        var resolvedActorKeys = new HashSet<string>(StringComparer.Ordinal);
        var resolvedActorNames = new HashSet<string>(StringComparer.Ordinal);
        var clusterKeys = new HashSet<string>(StringComparer.Ordinal);
        var clusterNames = new HashSet<string>(StringComparer.Ordinal);
        var knownPeopleKeys = new HashSet<string>(StringComparer.Ordinal);
        var knownPeopleNames = new HashSet<string>(StringComparer.Ordinal);
        var peopleDisplay = new List<string>();

        foreach (var participant in participants)
        {
            var links = actorLinkLookup.GetValueOrDefault(participant.Id) ?? [];
            var participantDisplay = string.IsNullOrWhiteSpace(participant.LabelRaw)
                ? $"НВ {participant.Ordinal}"
                : participant.LabelRaw!;

            var resolvedLink = links.FirstOrDefault(x => x.ResolvedActorId != null && !string.IsNullOrWhiteSpace(x.ResolvedActorDisplayName));
            if (resolvedLink is not null)
            {
                var key = $"ra:{resolvedLink.ResolvedActorId}";
                if (resolvedActorKeys.Add(key))
                    resolvedActorNames.Add(resolvedLink.ResolvedActorDisplayName!);

                peopleDisplay.Add(resolvedLink.ResolvedActorDisplayName!);
                continue;
            }

            var clusterLink = links.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ClusterTitle));
            if (clusterLink is not null)
            {
                var key = $"uc:{Normalize(clusterLink.ClusterTitle)}";
                if (clusterKeys.Add(key))
                    clusterNames.Add(clusterLink.ClusterTitle!);

                peopleDisplay.Add(clusterLink.ClusterTitle!);
                continue;
            }

            if (!participant.IsUnknown && participant.LabelNorm is not null)
            {
                var key = $"kp:{participant.LabelNorm}";
                if (knownPeopleKeys.Add(key))
                    knownPeopleNames.Add(participant.LabelRaw!);
            }

            peopleDisplay.Add(participantDisplay);
        }

        var personTagKeys = tags
            .Where(x => x.Kind is TagKind.Person or TagKind.Callsign)
            .Select(x => $"pt:{x.RawValueNorm}")
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var personTagNames = tags
            .Where(x => x.Kind is TagKind.Person or TagKind.Callsign)
            .Select(x => x.RawValue)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var probableActionKeys = probableActions
            .Where(x => x.Confidence >= 0.40m)
            .Select(x => $"pa:{x.ObservationActionId}")
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var probableActionNames = probableActions
            .Where(x => x.Confidence >= 0.40m)
            .OrderByDescending(x => x.Confidence)
            .Select(x => x.ObservationActionName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        string? effectiveSubdivision = null;
        string? effectiveSubdivisionKey = null;
        var subdivisionResolved = false;

        if (subdivisionLink is not null && subdivisionLink.ResolvedSubdivisionId != null && !string.IsNullOrWhiteSpace(subdivisionLink.ResolvedSubdivisionName))
        {
            effectiveSubdivision = subdivisionLink.ResolvedSubdivisionName;
            effectiveSubdivisionKey = $"rs:{subdivisionLink.ResolvedSubdivisionId}";
            subdivisionResolved = true;
        }
        else if (!string.IsNullOrWhiteSpace(row.SubdivisionRaw) && row.SubdivisionNorm is not null)
        {
            effectiveSubdivision = row.SubdivisionRaw;
            effectiveSubdivisionKey = $"sr:{row.SubdivisionNorm}";
        }
        else if (subdivisionLink is not null && !string.IsNullOrWhiteSpace(subdivisionLink.LabelRaw))
        {
            effectiveSubdivision = subdivisionLink.LabelRaw;
            effectiveSubdivisionKey = $"uc:{Normalize(subdivisionLink.LabelRaw)}";
        }

        var actionKeys = new HashSet<string>(StringComparer.Ordinal);
        if (row.ObservationActionId is not null)
            actionKeys.Add($"ba:{row.ObservationActionId}");

        foreach (var probableActionKey in probableActionKeys)
            actionKeys.Add(probableActionKey);

        return new DayPictureNode(
            row.Id,
            row.ObservedDate,
            row.ActionRaw,
            row.ObservationActionId,
            row.ObservationActionName,
            row.Layer,
            row.RmRaw,
            row.LocationRaw,
            row.DistrictRaw,
            effectiveSubdivision,
            effectiveSubdivisionKey,
            subdivisionResolved,
            [.. peopleDisplay.Distinct(StringComparer.Ordinal)],
            [.. tags.OrderBy(x => x.Kind).ThenBy(x => x.RawValue).Select(x => x.RawValue).Distinct(StringComparer.Ordinal)],
            probableActionNames,
            row.Note,
            resolvedActorKeys,
            resolvedActorNames,
            clusterKeys,
            clusterNames,
            knownPeopleKeys,
            knownPeopleNames,
            personTagKeys,
            personTagNames,
            actionKeys,
            Normalize(row.Layer),
            Normalize(row.RmRaw),
            Normalize(row.LocationRaw),
            Normalize(row.DistrictRaw));
    }

    private static List<DayPictureLinkDto> BuildLinks(IReadOnlyList<DayPictureNode> nodes, short minLinkScore)
    {
        var links = new List<DayPictureLinkDto>();

        for (var i = 0; i < nodes.Count; i++)
        {
            for (var j = i + 1; j < nodes.Count; j++)
            {
                var left = nodes[i];
                var right = nodes[j];
                var reasons = new List<string>();
                var score = 0;

                if (!string.IsNullOrWhiteSpace(left.EffectiveSubdivisionKey)
                    && string.Equals(left.EffectiveSubdivisionKey, right.EffectiveSubdivisionKey, StringComparison.Ordinal))
                {
                    score += left.SubdivisionResolved && right.SubdivisionResolved ? 4 : 2;
                    reasons.Add($"спільний підрозділ: {left.EffectiveSubdivision}");
                }

                var resolvedActors = Intersect(left.ResolvedActorKeys, right.ResolvedActorKeys);
                if (resolvedActors.Count > 0)
                {
                    score += Math.Min(6, resolvedActors.Count * 3);
                    foreach (var name in Intersect(left.ResolvedActorNames, right.ResolvedActorNames).Take(2))
                        reasons.Add($"спільна встановлена особа: {name}");
                }

                var clusters = Intersect(left.ClusterKeys, right.ClusterKeys);
                if (clusters.Count > 0)
                {
                    score += Math.Min(4, clusters.Count * 2);
                    foreach (var name in Intersect(left.ClusterNames, right.ClusterNames).Take(2))
                        reasons.Add($"спільна гіпотеза особи: {name}");
                }

                var knownPeople = Intersect(left.KnownPeopleKeys, right.KnownPeopleKeys);
                if (knownPeople.Count > 0)
                {
                    score += Math.Min(4, knownPeople.Count * 2);
                    foreach (var name in Intersect(left.KnownPeopleNames, right.KnownPeopleNames).Take(2))
                        reasons.Add($"спільна відома особа: {name}");
                }

                var personTags = Intersect(left.PersonTagKeys, right.PersonTagKeys);
                if (personTags.Count > 0)
                {
                    score += Math.Min(4, personTags.Count * 2);
                    foreach (var name in Intersect(left.PersonTagNames, right.PersonTagNames).Take(2))
                        reasons.Add($"спільна мітка: {name}");
                }

                var sharedActions = Intersect(left.ActionKeys, right.ActionKeys);
                if (sharedActions.Count > 0)
                {
                    score += sharedActions.Count * 2;
                    reasons.Add("спільний тип дії");
                }

                if (left.LayerNorm is not null && string.Equals(left.LayerNorm, right.LayerNorm, StringComparison.Ordinal))
                {
                    score += 1;
                    reasons.Add($"спільний шар: {left.Layer}");
                }

                if (left.RmNorm is not null && string.Equals(left.RmNorm, right.RmNorm, StringComparison.Ordinal))
                {
                    score += 1;
                    reasons.Add($"спільний RM: {left.RmRaw}");
                }

                if (left.DistrictNorm is not null && string.Equals(left.DistrictNorm, right.DistrictNorm, StringComparison.Ordinal))
                {
                    score += 1;
                    reasons.Add($"спільний район: {left.DistrictRaw}");
                }

                if (left.LocationNorm is not null && string.Equals(left.LocationNorm, right.LocationNorm, StringComparison.Ordinal))
                {
                    score += 2;
                    reasons.Add($"спільна локація: {left.LocationRaw}");
                }

                if (score < minLinkScore)
                    continue;

                links.Add(new DayPictureLinkDto(left.Id, right.Id, (short)score, [.. reasons.Distinct(StringComparer.Ordinal)]));
            }
        }

        return links;
    }

    private static List<DayPictureThreadDto> BuildThreads(
        IReadOnlyList<DayPictureNode> nodes,
        IReadOnlyList<DayPictureLinkDto> links)
    {
        var adjacency = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var node in nodes)
            adjacency[node.Id] = [];

        foreach (var link in links)
        {
            adjacency[link.LeftObservationId].Add(link.RightObservationId);
            adjacency[link.RightObservationId].Add(link.LeftObservationId);
        }

        var nodeLookup = nodes.ToDictionary(x => x.Id);
        var visited = new HashSet<Guid>();
        var groups = new List<List<Guid>>();

        foreach (var node in nodes)
        {
            if (!visited.Add(node.Id))
                continue;

            var queue = new Queue<Guid>();
            queue.Enqueue(node.Id);
            var component = new List<Guid>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                foreach (var next in adjacency[current])
                {
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            groups.Add(component);
        }

        return [.. groups
            .Select((ids, index) =>
            {
                var componentNodes = ids.Select(id => nodeLookup[id]).OrderBy(x => x.ObservedDate).ToList();
                var componentIdSet = ids.ToHashSet();
                var componentLinks = links
                    .Where(x => componentIdSet.Contains(x.LeftObservationId) && componentIdSet.Contains(x.RightObservationId))
                    .OrderByDescending(x => x.Score)
                    .ToList();

                var headline = BuildHeadline(componentNodes);
                var signals = BuildSignals(componentNodes);

                var observationDtos = componentNodes
                    .Select(x => new DayPictureObservationDto(
                        x.Id,
                        x.ObservedDate,
                        x.ActionRaw,
                        x.ObservationActionId,
                        x.ObservationActionName,
                        x.Layer,
                        x.RmRaw,
                        x.LocationRaw,
                        x.DistrictRaw,
                        x.EffectiveSubdivision,
                        x.SubdivisionResolved,
                        x.PeopleDisplay,
                        x.TagDisplay,
                        x.ProbableActionDisplay,
                        x.Note))
                    .ToList();

                return new DayPictureThreadDto(
                    index + 1,
                    headline,
                    observationDtos,
                    componentLinks,
                    signals);
            })
            .OrderBy(x => x.Observations.Min(o => o.ObservedDate))];
    }

    private static string BuildHeadline(IReadOnlyList<DayPictureNode> nodes)
    {
        var subdivision = nodes
            .Where(x => !string.IsNullOrWhiteSpace(x.EffectiveSubdivision))
            .GroupBy(x => x.EffectiveSubdivision!)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefault();

        var action = nodes
            .Select(x => x.ObservationActionName ?? x.ActionRaw)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x!)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefault();

        var location = nodes
            .Select(x => x.LocationRaw ?? x.DistrictRaw)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x!)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefault();

        var anchor = subdivision ?? action ?? location ?? "Події";
        return $"{anchor} · {nodes.Count} под.";
    }

    private static List<DayPictureSignalDto> BuildSignals(IReadOnlyList<DayPictureNode> nodes)
    {
        var signals = new List<DayPictureSignalDto>();

        signals.AddRange(nodes
            .Where(x => !string.IsNullOrWhiteSpace(x.EffectiveSubdivision))
            .GroupBy(x => x.EffectiveSubdivision!)
            .Select(x => new DayPictureSignalDto("subdivision", x.Key, x.Count())));

        signals.AddRange(nodes
            .SelectMany(x => x.PeopleDisplay)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x)
            .Where(x => x.Count() > 1)
            .Select(x => new DayPictureSignalDto("person", x.Key, x.Count())));

        signals.AddRange(nodes
            .SelectMany(x => x.TagDisplay)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x)
            .Where(x => x.Count() > 1)
            .Select(x => new DayPictureSignalDto("tag", x.Key, x.Count())));

        return [.. signals
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Kind)
            .ThenBy(x => x.Value)
            .Take(10)];
    }

    private static HashSet<string> Intersect(HashSet<string> left, HashSet<string> right)
        => left.Intersect(right, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> Intersect(HashSet<string> left, IReadOnlyCollection<string> right)
        => left.Intersect(right, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

    private static bool Contains(string? value, string queryNorm)
    {
        var norm = Normalize(value);
        return norm is not null && norm.Contains(queryNorm, StringComparison.Ordinal);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();

    private sealed record ObservationRow(
        Guid Id,
        DateTime ObservedDate,
        string ActionRaw,
        string ActionNorm,
        Guid? ObservationActionId,
        string? ObservationActionName,
        string? Layer,
        string? RmRaw,
        string? LocationRaw,
        string? DistrictRaw,
        string? SubdivisionRaw,
        string? SubdivisionNorm,
        string? Note);

    private sealed record ParticipantRow(
        Guid Id,
        Guid ObservationId,
        string? LabelRaw,
        string? LabelNorm,
        bool IsUnknown,
        string? RoleRaw,
        int Ordinal);

    private sealed record TagRow(
        Guid ObservationId,
        string RawValue,
        string RawValueNorm,
        TagKind Kind);

    private sealed record ProbableActionRow(
        Guid ObservationId,
        Guid ObservationActionId,
        string ObservationActionName,
        decimal Confidence);

    private sealed record ActorLinkRow(
        Guid ObservationParticipantId,
        string? ClusterTitle,
        Guid? ResolvedActorId,
        string? ResolvedActorDisplayName);

    private sealed record SubdivisionLinkRow(
        Guid ObservationId,
        Guid UnknownSubdivisionClusterId,
        string LabelRaw,
        Guid? ResolvedSubdivisionId,
        string? ResolvedSubdivisionName);

    private sealed record DayPictureNode(
        Guid Id,
        DateTime ObservedDate,
        string ActionRaw,
        Guid? ObservationActionId,
        string? ObservationActionName,
        string? Layer,
        string? RmRaw,
        string? LocationRaw,
        string? DistrictRaw,
        string? EffectiveSubdivision,
        string? EffectiveSubdivisionKey,
        bool SubdivisionResolved,
        IReadOnlyList<string> PeopleDisplay,
        IReadOnlyList<string> TagDisplay,
        IReadOnlyList<string> ProbableActionDisplay,
        string? Note,
        HashSet<string> ResolvedActorKeys,
        HashSet<string> ResolvedActorNames,
        HashSet<string> ClusterKeys,
        HashSet<string> ClusterNames,
        HashSet<string> KnownPeopleKeys,
        HashSet<string> KnownPeopleNames,
        HashSet<string> PersonTagKeys,
        HashSet<string> PersonTagNames,
        HashSet<string> ActionKeys,
        string? LayerNorm,
        string? RmNorm,
        string? LocationNorm,
        string? DistrictNorm);
}
