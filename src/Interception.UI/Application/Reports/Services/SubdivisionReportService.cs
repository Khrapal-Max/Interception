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
/// Builds aggregated subdivision report with action, actor and tag summaries.
/// Effective subdivision is resolved through analytical clusters first, then raw subdivision hint.
/// </summary>
public sealed class SubdivisionReportService(IDbContextFactory<AppDbContext> dbFactory) : ISubdivisionReportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<SubdivisionReportDto> BuildAsync(SubdivisionReportFilterDto filter, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var queryNorm = Normalize(filter.Query);
        var from = filter.ObservedFrom;
        var to = filter.ObservedTo;

        var observations = await db.Observations
            .AsNoTracking()
            .Select(x => new ObservationRow(
                x.Id,
                x.ObservedDate,
                x.ActionRaw,
                x.ActionNorm,
                x.ObservationActionId,
                x.ObservationAction != null ? x.ObservationAction.Name : null,
                x.LocationRaw,
                x.DistrictRaw,
                x.SubdivisionRaw,
                x.SubdivisionNorm,
                x.Layer,
                x.RmRaw,
                x.Note))
            .ToListAsync(ct);

        var filteredObservations = observations
            .Where(x => from is null || x.ObservedDate >= from.Value)
            .Where(x => to is null || x.ObservedDate <= to.Value)
            .Where(x => queryNorm is null || MatchesQuery(x, queryNorm))
            .ToList();

        if (filteredObservations.Count == 0)
            return new SubdivisionReportDto(DateTime.UtcNow, from, to, 0, 0, Array.Empty<SubdivisionReportItemDto>());

        var observationIds = filteredObservations.Select(x => x.Id).ToList();

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

        var actorLinks = await db.UnknownClusterMembers
            .AsNoTracking()
            .Where(x => observationIds.Contains(x.ObservationParticipant.ObservationId))
            .Select(x => new ActorLinkRow(
                x.ObservationParticipantId,
                x.UnknownCluster.Title,
                x.UnknownCluster.ResolvedActorId,
                x.UnknownCluster.ResolvedActor != null ? x.UnknownCluster.ResolvedActor.DisplayName : null))
            .ToListAsync(ct);

        var resolvedSubdivisionLinks = await db.UnknownSubdivisionObservations
            .AsNoTracking()
            .Where(x => observationIds.Contains(x.ObservationId))
            .Select(x => new SubdivisionLinkRow(
                x.ObservationId,
                x.UnknownSubdivisionCluster.ResolvedSubdivisionId,
                x.UnknownSubdivisionCluster.ResolvedSubdivision != null ? x.UnknownSubdivisionCluster.ResolvedSubdivision.Name : null,
                x.UnknownSubdivisionCluster.ResolvedSubdivision != null ? x.UnknownSubdivisionCluster.ResolvedSubdivision.LayerHint : null,
                x.UnknownSubdivisionCluster.ResolvedSubdivision != null ? x.UnknownSubdivisionCluster.ResolvedSubdivision.RmHint : null,
                x.UnknownSubdivisionCluster.LabelRaw))
            .ToListAsync(ct);

        var participantLookup = participants
            .GroupBy(x => x.ObservationId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var tagLookup = tags
            .GroupBy(x => x.ObservationId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var actorLookup = actorLinks
            .GroupBy(x => x.ObservationParticipantId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var subdivisionLookup = resolvedSubdivisionLinks
            .GroupBy(x => x.ObservationId)
            .ToDictionary(x => x.Key, x => x.FirstOrDefault(x => x.ResolvedSubdivisionId != null) ?? x.First());

        var groups = new Dictionary<string, SubdivisionAccumulator>(StringComparer.Ordinal);

        foreach (var observation in filteredObservations.OrderBy(x => x.ObservedDate))
        {
            var groupDescriptor = ResolveGroup(observation, subdivisionLookup.GetValueOrDefault(observation.Id), filter);
            if (groupDescriptor is null)
                continue;

            if (!groups.TryGetValue(groupDescriptor.GroupKey, out var accumulator))
            {
                accumulator = new SubdivisionAccumulator(groupDescriptor);
                groups[groupDescriptor.GroupKey] = accumulator;
            }

            var observationParticipants = participantLookup.GetValueOrDefault(observation.Id) ?? [];
            var observationTags = tagLookup.GetValueOrDefault(observation.Id) ?? [];

            accumulator.ObservationIds.Add(observation.Id);
            accumulator.ObservationsCount++;
            accumulator.FirstObservedAt = accumulator.FirstObservedAt is null || observation.ObservedDate < accumulator.FirstObservedAt
                ? observation.ObservedDate
                : accumulator.FirstObservedAt;
            accumulator.LastObservedAt = accumulator.LastObservedAt is null || observation.ObservedDate > accumulator.LastObservedAt
                ? observation.ObservedDate
                : accumulator.LastObservedAt;

            var actionName = observation.ObservationActionName ?? observation.ActionRaw;
            var actionKey = observation.ObservationActionId is null
                ? $"raw:{observation.ActionNorm}"
                : $"cat:{observation.ObservationActionId}";

            if (accumulator.Actions.TryGetValue(actionKey, out var actionCounter))
                accumulator.Actions[actionKey] = actionCounter with { Count = actionCounter.Count + 1 };
            else
                accumulator.Actions[actionKey] = new ActionCounter(actionName, observation.ObservationActionId is not null, 1);

            var actorKeysPerObservation = new HashSet<string>(StringComparer.Ordinal);
            foreach (var participant in observationParticipants)
            {
                if (participant.IsUnknown)
                    accumulator.UnknownParticipantsCount++;

                var actorDescriptor = ResolveActor(participant, actorLookup.GetValueOrDefault(participant.Id) ?? []);
                if (!actorKeysPerObservation.Add(actorDescriptor.Key))
                    continue;

                if (accumulator.Actors.TryGetValue(actorDescriptor.Key, out var actorCounter))
                    accumulator.Actors[actorDescriptor.Key] = actorCounter with { Count = actorCounter.Count + 1 };
                else
                    accumulator.Actors[actorDescriptor.Key] = new ActorCounter(actorDescriptor.DisplayName, actorDescriptor.IsResolved, actorDescriptor.Source, 1);
            }

            foreach (var tag in observationTags)
            {
                var key = $"{(short)tag.Kind}:{tag.RawValueNorm}";
                if (accumulator.Tags.TryGetValue(key, out var tagCounter))
                    accumulator.Tags[key] = tagCounter with { Count = tagCounter.Count + 1 };
                else
                    accumulator.Tags[key] = new TagCounter(tag.RawValue, tag.Kind, 1);
            }

            var samplePeople = observationParticipants
                .Select(x => ResolveActor(x, actorLookup.GetValueOrDefault(x.Id) ?? []).DisplayName)
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToList();

            accumulator.Samples.Add(new SubdivisionObservationSampleDto(
                observation.Id,
                observation.ObservedDate,
                observation.ActionRaw,
                observation.ObservationActionName,
                observation.LocationRaw,
                observation.DistrictRaw,
                samplePeople));
        }

        var items = groups.Values
            .Select(x => new SubdivisionReportItemDto(
                x.Descriptor.ResolvedSubdivisionId,
                x.Descriptor.GroupKey,
                x.Descriptor.DisplayName,
                x.Descriptor.IsResolved,
                x.Descriptor.LayerHint,
                x.Descriptor.RmHint,
                x.ObservationsCount,
                x.Actors.Count,
                x.UnknownParticipantsCount,
                x.FirstObservedAt,
                x.LastObservedAt,
                x.Actions.Values
                    .OrderByDescending(v => v.Count)
                    .ThenBy(v => v.Action, StringComparer.Ordinal)
                    .Take(Math.Clamp(filter.TopActions, 1, 50))
                    .Select(v => new SubdivisionActionStatDto(v.Action, v.Count, v.IsCatalogAction))
                    .ToList(),
                x.Actors.Values
                    .OrderByDescending(v => v.Count)
                    .ThenBy(v => v.DisplayName, StringComparer.Ordinal)
                    .Take(Math.Clamp(filter.TopActors, 1, 50))
                    .Select(v => new SubdivisionActorStatDto(v.DisplayName, v.Count, v.IsResolved, v.Source))
                    .ToList(),
                x.Tags.Values
                    .OrderByDescending(v => v.Count)
                    .ThenBy(v => v.Value, StringComparer.Ordinal)
                    .Take(Math.Clamp(filter.TopTags, 1, 50))
                    .Select(v => new SubdivisionTagStatDto(v.Value, v.Kind, v.Count))
                    .ToList(),
                x.Samples
                    .OrderByDescending(v => v.ObservedDate)
                    .Take(Math.Clamp(filter.SampleObservations, 1, 20))
                    .ToList()))
            .OrderByDescending(x => x.ObservationsCount)
            .ThenBy(x => x.DisplayName, StringComparer.Ordinal)
            .ToList();

        return new SubdivisionReportDto(
            DateTime.UtcNow,
            from,
            to,
            filteredObservations.Count,
            items.Count,
            items);
    }

    private static GroupDescriptor? ResolveGroup(
        ObservationRow observation,
        SubdivisionLinkRow? subdivisionLink,
        SubdivisionReportFilterDto filter)
    {
        if (subdivisionLink is not null && subdivisionLink.ResolvedSubdivisionId != null && !string.IsNullOrWhiteSpace(subdivisionLink.ResolvedSubdivisionName))
        {
            return new GroupDescriptor(
                subdivisionLink.ResolvedSubdivisionId,
                $"resolved:{subdivisionLink.ResolvedSubdivisionId}",
                subdivisionLink.ResolvedSubdivisionName!,
                true,
                subdivisionLink.LayerHint,
                subdivisionLink.RmHint);
        }

        if (filter.IncludeRawUnresolved && observation.SubdivisionNorm is not null && !string.IsNullOrWhiteSpace(observation.SubdivisionRaw))
        {
            return new GroupDescriptor(
                null,
                $"raw:{observation.SubdivisionNorm}",
                observation.SubdivisionRaw!,
                false,
                observation.Layer,
                observation.RmRaw);
        }

        if (filter.IncludeWithoutSubdivision)
        {
            return new GroupDescriptor(
                null,
                "none",
                "Без визначеного підрозділу",
                false,
                observation.Layer,
                observation.RmRaw);
        }

        return null;
    }

    private static ActorDescriptor ResolveActor(ParticipantRow participant, IReadOnlyList<ActorLinkRow> links)
    {
        var resolved = links.FirstOrDefault(x => x.ResolvedActorId != null && !string.IsNullOrWhiteSpace(x.ResolvedActorDisplayName));
        if (resolved is not null)
        {
            return new ActorDescriptor(
                $"resolved:{resolved.ResolvedActorId}",
                resolved.ResolvedActorDisplayName!,
                true,
                "resolved");
        }

        var cluster = links.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ClusterTitle));
        if (cluster is not null)
        {
            return new ActorDescriptor(
                $"cluster:{Normalize(cluster.ClusterTitle)}",
                cluster.ClusterTitle!,
                false,
                "hypothesis");
        }

        if (!participant.IsUnknown && participant.LabelNorm is not null && !string.IsNullOrWhiteSpace(participant.LabelRaw))
        {
            return new ActorDescriptor(
                $"known:{participant.LabelNorm}",
                participant.LabelRaw!,
                false,
                "known");
        }

        return new ActorDescriptor(
            $"unknown:{participant.ObservationId}:{participant.Ordinal}",
            string.IsNullOrWhiteSpace(participant.LabelRaw) ? $"НВ {participant.Ordinal}" : participant.LabelRaw!,
            false,
            "unknown");
    }

    private static bool MatchesQuery(ObservationRow row, string queryNorm)
    {
        return Contains(row.ActionNorm, queryNorm)
            || Contains(row.LocationRaw, queryNorm)
            || Contains(row.DistrictRaw, queryNorm)
            || Contains(row.SubdivisionRaw, queryNorm)
            || Contains(row.Note, queryNorm)
            || Contains(row.Layer, queryNorm)
            || Contains(row.RmRaw, queryNorm)
            || Contains(row.ObservationActionName, queryNorm);
    }

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
        string? LocationRaw,
        string? DistrictRaw,
        string? SubdivisionRaw,
        string? SubdivisionNorm,
        string? Layer,
        string? RmRaw,
        string? Note);

    private sealed record ParticipantRow(
        Guid Id,
        Guid ObservationId,
        string? LabelRaw,
        string? LabelNorm,
        bool IsUnknown,
        int Ordinal);

    private sealed record TagRow(
        Guid ObservationId,
        string RawValue,
        string RawValueNorm,
        TagKind Kind);

    private sealed record ActorLinkRow(
        Guid ObservationParticipantId,
        string? ClusterTitle,
        Guid? ResolvedActorId,
        string? ResolvedActorDisplayName);

    private sealed record SubdivisionLinkRow(
        Guid ObservationId,
        Guid? ResolvedSubdivisionId,
        string? ResolvedSubdivisionName,
        string? LayerHint,
        string? RmHint,
        string LabelRaw);

    private sealed record GroupDescriptor(
        Guid? ResolvedSubdivisionId,
        string GroupKey,
        string DisplayName,
        bool IsResolved,
        string? LayerHint,
        string? RmHint);

    private sealed record ActorDescriptor(
        string Key,
        string DisplayName,
        bool IsResolved,
        string Source);

    private sealed record ActionCounter(
        string Action,
        bool IsCatalogAction,
        int Count);

    private sealed record ActorCounter(
        string DisplayName,
        bool IsResolved,
        string Source,
        int Count);

    private sealed record TagCounter(
        string Value,
        TagKind Kind,
        int Count);

    private sealed class SubdivisionAccumulator(GroupDescriptor descriptor)
    {
        public GroupDescriptor Descriptor { get; } = descriptor;
        public int ObservationsCount { get; set; }
        public int UnknownParticipantsCount { get; set; }
        public DateTime? FirstObservedAt { get; set; }
        public DateTime? LastObservedAt { get; set; }
        public HashSet<Guid> ObservationIds { get; } = [];
        public Dictionary<string, ActionCounter> Actions { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ActorCounter> Actors { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, TagCounter> Tags { get; } = new(StringComparer.Ordinal);
        public List<SubdivisionObservationSampleDto> Samples { get; } = [];
    }
}
