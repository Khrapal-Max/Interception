//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Builders.LinkMap;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Domain.Entities;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Будує карту зв'язків за логікою communication-first.
/// Спочатку визначаються стійкі комунікаційні групи, а вже потім
/// для них робиться спроба вивести підрозділ та міжгрупові мости.
/// Додатково агрегуються характерні дії групи й моста.
/// </summary>
public sealed partial class LinkMapService(IDbContextFactory<AppDbContext> dbFactory) : ILinkMapService
{
    private const string UnknownDivision = "НВ підрозділ";
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<LinkMapDto> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.InterceptionMessages
            .AsNoTracking()
            .Include(x => x.Participants)
            .Include(x => x.InterceptionAction)
            .AsQueryable();

        if (dateFrom.HasValue)
            query = query.Where(x => x.ObservedDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.ObservedDate <= dateTo.Value);

        var messages = await query.ToListAsync(ct);
        if (messages.Count == 0)
            return new LinkMapDto([]);

        var canonicalDisplayByNameMap = await LoadCanonicalDisplayByNameMapAsync(db, ct);
        var frequencyDivisionMap = BuildFrequencyDivisionMap(messages);

        var messageRows = messages
            .Select(message => new MessageRow(
                message,
                ResolveEffectiveDivision(message.Division, message.Frequency, frequencyDivisionMap),
                GetKnownParticipants(message, canonicalDisplayByNameMap)))
            .Where(x => x.KnownParticipants.Count >= 2)
            .ToList();

        if (messageRows.Count == 0)
            return new LinkMapDto([]);

        var groups = BuildGroupsByCommunication(messageRows, frequencyDivisionMap);
        if (groups.Count == 0)
            return new LinkMapDto([]);

        groups = MergeStableGroupsAcrossFrequencies(groups);
        if (groups.Count == 0)
            return new LinkMapDto([]);

        ApplyBridges(groups, messageRows);

        var groupCountByMember = groups.Values
            .SelectMany(group => group.Members)
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        var models = groups.Values
            .Select(x => x.ToModel(groupCountByMember))
            .OrderByDescending(x => x.Bridges.Count)
            .ThenByDescending(x => x.Members.Count)
            .ThenBy(x => x.Division ?? string.Empty)
            .ThenBy(x => x.KeyPersonName)
            .ToList();

        return new LinkMapDto(models);
    }

    /// <summary>
    /// Будує карту частота → домінуючий підрозділ.
    /// </summary>
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

    /// <summary>
    /// Визначає effective division для message, якщо його вже можна впевнено вивести.
    /// </summary>
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

    /// <summary>
    /// Повертає відомих учасників повідомлення без дублікатів по імені.
    /// </summary>
    private static List<ParticipantSnapshot> GetKnownParticipants(
        InterceptionMessage message,
        IReadOnlyDictionary<string, string> canonicalDisplayByNameMap)
    {
        return [.. message.Participants
            .Where(x => !x.IsUnknown && !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new ParticipantSnapshot(
                ResolveParticipantName(x.Name!, canonicalDisplayByNameMap),
                NormalizeMeaningfulOrNull(x.Role),
                message.ObservedDate,
                NormalizeMeaningfulOrNull(message.Frequency)))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())];
    }

    /// <summary>
    /// Будує стійкі комунікаційні групи. Базовий carrier групи — частота, а не підрозділ.
    /// В межах однієї частоти формується одна агрегована група з усіма відомими учасниками
    /// цієї частоти, навіть якщо всередині є кілька незв'язаних компонент.
    /// </summary>
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

            var frequency = frequencyGroup.Key;
            if (people.Count < 3)
                continue;

            var inferredDivision = componentRows
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

            foreach (var row in componentRows)
            {
                group.AddMessageCount(1);
                group.AddAction(GetActionName(row.Message));
            }

            foreach (var person in people.Values)
                group.AddPerson(person);

            group.AddInternalConnectionWeight(people.Values.Sum(x => x.ConnectionWeight));
            group.FinalizeCoreProperties();

            var tempGroupKey = BuildFrequencyScopedGroupKey(
                frequency,
                inferredDivision,
                people.Keys,
                group.KeyPersonName);

            groups[tempGroupKey] = group;
        }

        return groups;
    }

    /// <summary>
    /// Додає міжгрупові мости через observation, у яких одночасно присутні
    /// представники різних груп. Для показу групи лишається один KeyPerson,
    /// але для перевірки зв'язків використовується ширше ядро представників групи.
    /// </summary>
    private static void ApplyBridges(
        IReadOnlyDictionary<string, GroupAccumulator> groups,
        IReadOnlyList<MessageRow> messageRows)
    {
        if (groups.Count < 2)
            return;

        var groupCountByMember = groups.Values
            .SelectMany(group => group.Members)
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        var bridgeGroups = groups.Values
            .Select(group =>
            {
                var representatives = group
                    .GetBridgeRepresentatives(3)
                    .Where(name => groupCountByMember.TryGetValue(name, out var count) && count == 1)
                    .ToList();

                if (representatives.Count == 0 && !string.IsNullOrWhiteSpace(group.KeyPersonName))
                    representatives.Add(group.KeyPersonName.Trim());

                var representativePriority = representatives
                    .Select((name, index) => new { Name = name, Index = index })
                    .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);

                return new
                {
                    group.GroupKey,
                    group.Division,
                    Representatives = representatives,
                    RepresentativePriority = representativePriority
                };
            })
            .Where(group => group.Representatives.Count > 0)
            .ToList();

        var bridgeMap = new Dictionary<string, PairBridgeAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in messageRows)
        {
            var frequency = NormalizeMeaningfulOrNull(row.Message.Frequency);
            if (frequency is null)
                continue;

            var participantNamesInRow = row.KnownParticipants
                .Select(p => p.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (participantNamesInRow.Count < 2)
                continue;

            var representedGroups = bridgeGroups
                .Select(group =>
                {
                    var matchedRepresentatives = group.Representatives
                        .Where(participantNamesInRow.Contains)
                        .OrderBy(name => group.RepresentativePriority[name])
                        .ToList();

                    if (matchedRepresentatives.Count == 0)
                        return null;

                    return new
                    {
                        group.GroupKey,
                        group.Division,
                        ContactName = matchedRepresentatives[0]
                    };
                })
                .Where(x => x is not null)
                .OrderBy(x => x!.GroupKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (representedGroups.Count < 2)
                continue;

            for (var i = 0; i < representedGroups.Count; i++)
            {
                for (var j = i + 1; j < representedGroups.Count; j++)
                {
                    var left = representedGroups[i]!;
                    var right = representedGroups[j]!;

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
                    frequencyAccumulator.LeftContacts.Increment(left.ContactName);
                    frequencyAccumulator.RightContacts.Increment(right.ContactName);
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

    /// <summary>
    /// Зливає групи, які мають однакове стале ядро, але були знайдені на різних частотах.
    /// Це дає більш стабільну модель, де carrier-frequency не створює зайву дубль-групу.
    /// </summary>
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
        return GroupKeyBuilder.BuildFrequencyScopedGroupKey(frequency, division, members, keyPersonName);
    }

    private static string BuildStableGroupKey(string? division, IEnumerable<string> members, string keyPersonName)
    {
        return GroupKeyBuilder.BuildStableGroupKey(division, members, keyPersonName);
    }

    private static string BuildPairKey(string leftGroupKey, string rightGroupKey)
    {
        return string.Compare(leftGroupKey, rightGroupKey, StringComparison.OrdinalIgnoreCase) <= 0
            ? $"{leftGroupKey}::{rightGroupKey}"
            : $"{rightGroupKey}::{leftGroupKey}";
    }

    private static string ResolveParticipantName(
        string observedName,
        IReadOnlyDictionary<string, string> canonicalDisplayByNameMap)
    {
        var normalizedName = StringTextNormExtensions.NormalizeOption(observedName);
        if (!string.IsNullOrWhiteSpace(normalizedName)
            && canonicalDisplayByNameMap.TryGetValue(normalizedName, out var canonicalDisplayName)
            && !string.IsNullOrWhiteSpace(canonicalDisplayName))
        {
            return canonicalDisplayName.Trim();
        }

        return observedName.Trim();
    }

    private static async Task<Dictionary<string, string>> LoadCanonicalDisplayByNameMapAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var rows = await db.CanonicalPersonMembers
            .AsNoTracking()
            .Join(
                db.ResolvedParticipants.AsNoTracking(),
                member => member.ResolvedParticipantId,
                resolved => resolved.Id,
                (member, resolved) => new
                {
                    member.CanonicalPersonId,
                    resolved.Name
                })
            .Join(
                db.CanonicalPersons.AsNoTracking(),
                member => member.CanonicalPersonId,
                canonical => canonical.Id,
                (member, canonical) => new
                {
                    member.Name,
                    canonical.DisplayName
                })
            .ToListAsync(ct);

        return rows
            .Select(x => new
            {
                NormalizedName = StringTextNormExtensions.NormalizeOption(x.Name),
                CanonicalDisplayName = NormalizeMeaningfulOrNull(x.DisplayName)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedName) && !string.IsNullOrWhiteSpace(x.CanonicalDisplayName))
            .GroupBy(x => x.NormalizedName!, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Select(v => v.CanonicalDisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
            .ToDictionary(
                x => x.Key,
                x => x.First().CanonicalDisplayName!,
                StringComparer.OrdinalIgnoreCase);
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

    private static string? ExtractCenterFromGroupKey(string? groupKey)
    {
        if (string.IsNullOrWhiteSpace(groupKey))
            return null;

        var stablePart = groupKey;
        var frequencySeparator = stablePart.IndexOf("::", StringComparison.Ordinal);
        if (frequencySeparator >= 0)
            stablePart = stablePart[(frequencySeparator + 2)..];

        var firstPipe = stablePart.IndexOf('|');
        if (firstPipe < 0)
            return null;

        var secondPipe = stablePart.IndexOf('|', firstPipe + 1);
        if (secondPipe < 0)
            return null;

        var center = stablePart.Substring(firstPipe + 1, secondPipe - firstPipe - 1).Trim();
        return string.IsNullOrWhiteSpace(center) ? null : center;
    }
}
