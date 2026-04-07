//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Builders.LinkMap;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Будує та зберігає snapshot-и топології карти зв'язків.
/// </summary>
public sealed class TopologySnapshotBuilder(IDbContextFactory<AppDbContext> dbFactory) : ITopologySnapshotBuilder
{
    private const string UnknownDivision = "НВ підрозділ";
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<TopologySnapshotStateDto> GetStateAsync(
        DateTime? dateFromUtc = null,
        DateTime? dateToUtc = null,
        CancellationToken ct = default)
    {
        var (normalizedFromUtc, normalizedToUtc) = TopologySnapshotPeriodNormalizer.Normalize(dateFromUtc, dateToUtc);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var baseQuery = db.TopologySnapshotRuns
            .AsNoTracking()
            .Where(x => x.DateFromUtc == normalizedFromUtc && x.DateToUtc == normalizedToUtc);

        var latestRun = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var latestCompleted = await baseQuery
            .Where(x => x.Status == TopologySnapshotRunStatus.Completed)
            .OrderByDescending(x => x.CompletedAt)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (latestRun is null && latestCompleted is null)
        {
            return new TopologySnapshotStateDto(
                HasSnapshot: false,
                IsStale: true,
                IsBuilding: false,
                Status: "missing",
                LastCompletedAt: null,
                GroupCount: 0,
                ErrorMessage: null);
        }

        var status = latestRun?.Status switch
        {
            TopologySnapshotRunStatus.Building => "building",
            TopologySnapshotRunStatus.Failed => "failed",
            TopologySnapshotRunStatus.Completed => "completed",
            _ => "missing"
        };

        return new TopologySnapshotStateDto(
            HasSnapshot: latestCompleted is not null,
            IsStale: latestCompleted?.IsStale ?? true,
            IsBuilding: latestRun?.Status == TopologySnapshotRunStatus.Building,
            Status: status,
            LastCompletedAt: latestCompleted?.CompletedAt,
            GroupCount: latestCompleted?.GroupCount ?? 0,
            ErrorMessage: latestRun?.Status == TopologySnapshotRunStatus.Failed ? latestRun.ErrorMessage : null);
    }

    /// <inheritdoc />
    public async Task<TopologySnapshotRebuildResultDto> RebuildAsync(
        DateTime? dateFromUtc = null,
        DateTime? dateToUtc = null,
        CancellationToken ct = default)
    {
        var (normalizedFromUtc, normalizedToUtc) = TopologySnapshotPeriodNormalizer.Normalize(dateFromUtc, dateToUtc);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var run = TopologySnapshotRun.Create(normalizedFromUtc, normalizedToUtc);
        db.TopologySnapshotRuns.Add(run);
        await db.SaveChangesAsync(ct);

        try
        {
            var models = await BuildModelsAsync(db, normalizedFromUtc, normalizedToUtc, ct);

            for (var groupIndex = 0; groupIndex < models.Count; groupIndex++)
            {
                var model = models[groupIndex];
                var group = TopologySnapshotGroup.Create(
                    run.Id,
                    model.GroupKey,
                    model.Division,
                    model.KeyPersonName,
                    model.KeyPersonRole,
                    model.MentionCount,
                    model.InternalConnectionWeight,
                    model.BridgeWeight,
                    groupIndex);

                for (var frequencyIndex = 0; frequencyIndex < model.Frequencies.Count; frequencyIndex++)
                    group.AddFrequency(model.Frequencies[frequencyIndex], frequencyIndex);

                for (var memberIndex = 0; memberIndex < model.MemberDetails.Count; memberIndex++)
                {
                    var member = model.MemberDetails[memberIndex];
                    group.AddMember(
                        member.Name,
                        member.Role,
                        member.MentionCount,
                        member.UniquePartnerCount,
                        member.ConnectionWeight,
                        member.LastSeenAt,
                        member.GroupCount,
                        member.IsCrossGroup,
                        member.IsKeyPerson,
                        memberIndex);
                }

                for (var actionIndex = 0; actionIndex < model.TopActions.Count; actionIndex++)
                {
                    var action = model.TopActions[actionIndex];
                    group.AddAction(action, actionIndex == 0, actionIndex);
                }

                for (var bridgeIndex = 0; bridgeIndex < model.Bridges.Count; bridgeIndex++)
                {
                    var bridgeModel = model.Bridges[bridgeIndex];
                    var bridge = group.AddBridge(
                        bridgeModel.TargetGroupKey,
                        bridgeModel.TargetDivision,
                        bridgeModel.ContactPersonName,
                        bridgeModel.BridgeFrequency,
                        bridgeModel.Weight,
                        bridgeModel.PrimaryAction,
                        bridgeIndex);

                    for (var actionIndex = 0; actionIndex < bridgeModel.TopActions.Count; actionIndex++)
                    {
                        var action = bridgeModel.TopActions[actionIndex];
                        bridge.AddAction(action, actionIndex == 0, actionIndex);
                    }
                }

                run.AddGroup(group);
            }

            run.Complete(models.Count);
            await db.SaveChangesAsync(ct);

            return new TopologySnapshotRebuildResultDto(run.Id, run.GroupCount, run.CompletedAt ?? run.CreatedAt);
        }
        catch (Exception ex)
        {
            run.Fail(ex.Message);
            await db.SaveChangesAsync(ct);
            throw;
        }
    }

    private static async Task<List<LinkMapGroupDto>> BuildModelsAsync(
        AppDbContext db,
        DateTime? dateFromUtc,
        DateTime? dateToUtc,
        CancellationToken ct)
    {
        var query = db.InterceptionMessages
            .AsNoTracking()
            .Include(x => x.Participants)
            .Include(x => x.InterceptionAction)
            .AsQueryable();

        if (dateFromUtc.HasValue)
            query = query.Where(x => x.ObservedDate >= dateFromUtc.Value);

        if (dateToUtc.HasValue)
            query = query.Where(x => x.ObservedDate <= dateToUtc.Value);

        var messages = await query.ToListAsync(ct);
        if (messages.Count == 0)
            return [];

        var frequencyDivisionMap = BuildFrequencyDivisionMap(messages);

        var messageRows = messages
            .Select(message => new MessageRow(
                message,
                ResolveEffectiveDivision(message.Division, message.Frequency, frequencyDivisionMap),
                GetKnownParticipants(message)))
            .Where(x => x.KnownParticipants.Count >= 2)
            .ToList();

        if (messageRows.Count == 0)
            return [];

        var groups = BuildGroupsByCommunication(messageRows, frequencyDivisionMap);
        if (groups.Count == 0)
            return [];

        groups = MergeStableGroupsAcrossFrequencies(groups);
        if (groups.Count == 0)
            return [];

        ApplyBridges(groups, messageRows);

        var groupCountByMember = groups.Values
            .SelectMany(group => group.Members)
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        return groups.Values
            .Select(x => x.ToModel(groupCountByMember))
            .OrderByDescending(x => x.Bridges.Count)
            .ThenByDescending(x => x.Members.Count)
            .ThenBy(x => x.Division ?? string.Empty)
            .ThenBy(x => x.KeyPersonName)
            .ToList();
    }

    private static Dictionary<string, string> BuildFrequencyDivisionMap(List<InterceptionMessage> messages)
    {
        return messages
            .Where(x => !string.IsNullOrWhiteSpace(x.Frequency))
            .GroupBy(x => x.Frequency!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Frequency = group.Key,
                Division = group
                    .Select(x => x.Division)
                    .Where(IsMeaningfulDivision)
                    .GroupBy(x => x!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(x => x.Count())
                    .ThenBy(x => x.Key)
                    .Select(x => x.Key)
                    .FirstOrDefault()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Division))
            .ToDictionary(x => x.Frequency, x => x.Division!, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveEffectiveDivision(
        string? observedDivision,
        string? frequency,
        Dictionary<string, string> frequencyDivisionMap)
    {
        if (IsMeaningfulDivision(observedDivision))
            return observedDivision!.Trim();

        if (!string.IsNullOrWhiteSpace(frequency)
            && frequencyDivisionMap.TryGetValue(frequency.Trim(), out var inferred)
            && IsMeaningfulDivision(inferred))
        {
            return inferred.Trim();
        }

        return null;
    }

    private static List<ParticipantSnapshot> GetKnownParticipants(InterceptionMessage message)
    {
        return [.. message.Participants
            .Where(x => !x.IsUnknown && !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new ParticipantSnapshot(
                x.Name!.Trim(),
                NormalizeMeaningfulOrNull(x.Role),
                message.ObservedDate,
                NormalizeMeaningfulOrNull(message.Frequency)))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())];
    }

    private static Dictionary<string, GroupAccumulator> BuildGroupsByCommunication(
        IReadOnlyList<MessageRow> messageRows,
        Dictionary<string, string> frequencyDivisionMap)
    {
        var groups = new Dictionary<string, GroupAccumulator>(StringComparer.OrdinalIgnoreCase);

        var byFrequency = messageRows
            .Where(x => !string.IsNullOrWhiteSpace(x.Message.Frequency))
            .GroupBy(x => x.Message.Frequency!.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key)
            .ToList();

        foreach (var frequencyGroup in byFrequency)
        {
            var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var people = new Dictionary<string, PersonAccumulator>(StringComparer.OrdinalIgnoreCase);
            var componentRows = frequencyGroup.ToList();

            foreach (var row in componentRows)
            {
                foreach (var participant in row.KnownParticipants)
                {
                    if (!people.TryGetValue(participant.Name, out var person))
                    {
                        person = new PersonAccumulator(participant.Name);
                        people[participant.Name] = person;
                    }

                    person.RegisterMention(participant.ObservedAt, participant.Role, participant.Frequency);

                    if (!adjacency.ContainsKey(participant.Name))
                        adjacency[participant.Name] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                for (var i = 0; i < row.KnownParticipants.Count; i++)
                {
                    for (var j = i + 1; j < row.KnownParticipants.Count; j++)
                    {
                        var left = row.KnownParticipants[i].Name;
                        var right = row.KnownParticipants[j].Name;

                        adjacency[left].Add(right);
                        adjacency[right].Add(left);

                        people[left].AddConnectionWeight();
                        people[right].AddConnectionWeight();
                    }
                }
            }

            foreach (var person in people.Values)
            {
                if (adjacency.TryGetValue(person.Name, out var neighbours))
                    person.SetUniquePartnerCount(neighbours.Count);
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var frequency = frequencyGroup.Key;

            foreach (var personName in people.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (visited.Contains(personName))
                    continue;

                var component = TraverseComponent(personName, adjacency, visited);
                if (component.Count < 3)
                    continue;

                var componentPeople = component
                    .Select(name => people[name])
                    .ToList();

                var inferredDivision = componentRows
                    .Where(row => row.KnownParticipants.Any(p => component.Contains(p.Name)))
                    .Select(row => row.EffectiveDivision)
                    .Where(IsMeaningfulDivision)
                    .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(x => x.Count())
                    .ThenBy(x => x.Key)
                    .Select(x => x.Key)
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(inferredDivision)
                    && frequencyDivisionMap.TryGetValue(frequency, out var frequencyDivision)
                    && IsMeaningfulDivision(frequencyDivision))
                {
                    inferredDivision = frequencyDivision.Trim();
                }

                var group = new GroupAccumulator();
                group.AddDivisionHint(NormalizeMeaningfulOrNull(inferredDivision));
                group.AddFrequency(frequency);

                foreach (var row in componentRows.Where(r => r.KnownParticipants.Any(p => component.Contains(p.Name))))
                {
                    group.AddMessageCount(1);
                    group.AddAction(GetActionName(row.Message));
                }

                foreach (var person in componentPeople)
                    group.AddPerson(person);

                group.AddInternalConnectionWeight(componentPeople.Sum(x => x.ConnectionWeight));
                group.FinalizeCoreProperties();

                var tempGroupKey = BuildFrequencyScopedGroupKey(
                    frequency,
                    inferredDivision,
                    componentPeople.Select(x => x.Name),
                    group.KeyPersonName);

                groups[tempGroupKey] = group;
            }
        }

        return groups;
    }

    private static void ApplyBridges(
        IReadOnlyDictionary<string, GroupAccumulator> groups,
        IReadOnlyList<MessageRow> messageRows)
    {
        if (groups.Count < 2)
            return;

        var bridgeGroups = groups.Values
            .Where(group => !string.IsNullOrWhiteSpace(group.KeyPersonName))
            .Select(group => new
            {
                group.GroupKey,
                group.Division,
                CenterName = group.KeyPersonName.Trim()
            })
            .ToList();

        var bridgeMap = new Dictionary<string, PairBridgeAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in messageRows)
        {
            var frequency = NormalizeMeaningfulOrNull(row.Message.Frequency);
            if (frequency is null)
                continue;

            var centerParticipantsInRow = row.KnownParticipants
                .Where(p => IsCenterCandidate(p.Name, p.Role))
                .Select(p => p.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (centerParticipantsInRow.Count < 2)
                continue;

            var representedGroups = bridgeGroups
                .Where(group => centerParticipantsInRow.Contains(group.CenterName))
                .OrderBy(group => group.GroupKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (representedGroups.Count < 2)
                continue;

            for (var i = 0; i < representedGroups.Count; i++)
            {
                for (var j = i + 1; j < representedGroups.Count; j++)
                {
                    var left = representedGroups[i];
                    var right = representedGroups[j];

                    if (!groups.TryGetValue(left.GroupKey, out var leftGroup)
                        || !groups.TryGetValue(right.GroupKey, out var rightGroup))
                    {
                        continue;
                    }

                    var pairKey = BuildPairKey(left.GroupKey, right.GroupKey);
                    if (!bridgeMap.TryGetValue(pairKey, out var pairAccumulator))
                    {
                        pairAccumulator = new PairBridgeAccumulator(left.GroupKey, right.GroupKey);
                        bridgeMap[pairKey] = pairAccumulator;
                    }

                    pairAccumulator.TotalWeight++;
                    pairAccumulator.AddAction(GetActionName(row.Message));

                    if (!pairAccumulator.ByFrequency.TryGetValue(frequency, out var frequencyAccumulator))
                    {
                        frequencyAccumulator = new FrequencyBridgeAccumulator(frequency);
                        pairAccumulator.ByFrequency[frequency] = frequencyAccumulator;
                    }

                    frequencyAccumulator.Weight++;
                    frequencyAccumulator.LeftContacts.Increment(left.CenterName);
                    frequencyAccumulator.RightContacts.Increment(right.CenterName);
                }
            }
        }

        foreach (var pair in bridgeMap.Values)
        {
            if (!groups.TryGetValue(pair.LeftGroupKey, out var leftGroup)
                || !groups.TryGetValue(pair.RightGroupKey, out var rightGroup))
            {
                continue;
            }

            var chosenFrequency = pair.ByFrequency.Values
                .OrderByDescending(x => x.Weight)
                .ThenBy(x => x.Frequency)
                .FirstOrDefault();

            if (chosenFrequency is null)
                continue;

            var leftContact = chosenFrequency.LeftContacts.GetTopName();
            var rightContact = chosenFrequency.RightContacts.GetTopName();
            if (string.IsNullOrWhiteSpace(leftContact) || string.IsNullOrWhiteSpace(rightContact))
                continue;

            var primaryAction = pair.GetPrimaryAction();
            var topActions = pair.GetTopActions();

            leftGroup.Bridges.Add(new BridgeAccumulator(
                rightGroup.GroupKey,
                rightGroup.Division,
                rightContact,
                chosenFrequency.Frequency,
                pair.TotalWeight,
                primaryAction,
                topActions));

            rightGroup.Bridges.Add(new BridgeAccumulator(
                leftGroup.GroupKey,
                leftGroup.Division,
                leftContact,
                chosenFrequency.Frequency,
                pair.TotalWeight,
                primaryAction,
                topActions));
        }
    }

    private static Dictionary<string, GroupAccumulator> MergeStableGroupsAcrossFrequencies(
        IReadOnlyDictionary<string, GroupAccumulator> groups)
    {
        if (groups.Count <= 1)
            return groups.Values.ToDictionary(x => x.GroupKey, x => x, StringComparer.OrdinalIgnoreCase);

        var source = groups.Values
            .OrderBy(x => x.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var nodeIds = source
            .Select((group, index) => new { NodeId = $"node:{index}", Group = group })
            .ToList();

        var adjacency = nodeIds.ToDictionary(
            x => x.NodeId,
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < nodeIds.Count; i++)
        {
            for (var j = i + 1; j < nodeIds.Count; j++)
            {
                if (!ShouldMergeGroups(nodeIds[i].Group, nodeIds[j].Group))
                    continue;

                adjacency[nodeIds[i].NodeId].Add(nodeIds[j].NodeId);
                adjacency[nodeIds[j].NodeId].Add(nodeIds[i].NodeId);
            }
        }

        var merged = new Dictionary<string, GroupAccumulator>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in nodeIds)
        {
            if (visited.Contains(item.NodeId))
                continue;

            var componentNodeIds = TraverseComponent(item.NodeId, adjacency, visited);
            var componentGroups = nodeIds
                .Where(x => componentNodeIds.Contains(x.NodeId))
                .Select(x => x.Group)
                .ToList();

            var mergedGroup = MergeGroupComponent(componentGroups);
            mergedGroup.FinalizeCoreProperties();
            merged[mergedGroup.GroupKey] = mergedGroup;
        }

        return merged;
    }

    private static bool ShouldMergeGroups(GroupAccumulator left, GroupAccumulator right)
    {
        left.FinalizeCoreProperties();
        right.FinalizeCoreProperties();

        if (!string.Equals(left.KeyPersonName, right.KeyPersonName, StringComparison.OrdinalIgnoreCase))
            return false;

        var sharedMembers = left.Members.Intersect(right.Members, StringComparer.OrdinalIgnoreCase).Count();
        if (sharedMembers < 2)
            return false;

        var smallerGroupSize = Math.Min(left.Members.Count, right.Members.Count);
        if (smallerGroupSize == 0)
            return false;

        return sharedMembers * 2 >= smallerGroupSize;
    }

    private static GroupAccumulator MergeGroupComponent(IReadOnlyList<GroupAccumulator> componentGroups)
    {
        if (componentGroups.Count == 1)
            return componentGroups[0];

        var merged = new GroupAccumulator();

        foreach (var group in componentGroups)
            merged.MergeFrom(group);

        return merged;
    }

    private static HashSet<string> TraverseComponent(
        string start,
        Dictionary<string, HashSet<string>> adjacency,
        HashSet<string> visited)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
                continue;

            result.Add(current);

            if (!adjacency.TryGetValue(current, out var neighbours))
                continue;

            foreach (var neighbour in neighbours)
            {
                if (!visited.Contains(neighbour))
                    stack.Push(neighbour);
            }
        }

        return result;
    }

    private static string BuildFrequencyScopedGroupKey(
        string frequency,
        string? division,
        IEnumerable<string> members,
        string keyPersonName)
    {
        var stablePart = BuildStableGroupKey(division, members, keyPersonName);
        return $"{frequency.Trim().ToUpperInvariant()}::{stablePart}";
    }

    private static string BuildStableGroupKey(string? division, IEnumerable<string> members, string keyPersonName)
    {
        var divisionPart = string.IsNullOrWhiteSpace(division) ? "NO-DIVISION" : division.Trim().ToUpperInvariant();
        var membersPart = string.Join(";", members
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        return $"{divisionPart}|{keyPersonName.Trim().ToUpperInvariant()}|{membersPart}";
    }

    private static string BuildPairKey(string leftGroupKey, string rightGroupKey)
    {
        return string.Compare(leftGroupKey, rightGroupKey, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{leftGroupKey}::{rightGroupKey}"
            : $"{rightGroupKey}::{leftGroupKey}";
    }

    private static string? NormalizeMeaningfulOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? GetActionName(InterceptionMessage message)
        => NormalizeMeaningfulOrNull(message.InterceptionAction?.Name);

    private static bool IsMeaningfulDivision(string? division)
        => !string.IsNullOrWhiteSpace(division)
           && !string.Equals(division.Trim(), UnknownDivision, StringComparison.OrdinalIgnoreCase);

    internal static bool IsCenterCandidate(string name, string? role)
    {
        return name.Contains("ЦЕНТР", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(role)
                && role.Contains("координ", StringComparison.OrdinalIgnoreCase));
    }
}
