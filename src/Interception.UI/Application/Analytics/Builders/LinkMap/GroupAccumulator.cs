//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Analytics.Services;

namespace Interception.UI.Application.Analytics.Builders.LinkMap;

internal sealed class GroupAccumulator
{
    private readonly Dictionary<string, PersonAccumulator> _people = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _divisionHints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _actionCounts = new(StringComparer.OrdinalIgnoreCase);

    public string? Division { get; private set; }
    public string KeyPersonName { get; private set; } = string.Empty;
    public string? KeyPersonRole { get; private set; }
    public HashSet<string> Frequencies { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Members { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<BridgeAccumulator> Bridges { get; } = [];
    public int MessageCount { get; private set; }
    public int InternalConnectionWeight { get; private set; }
    public IReadOnlyDictionary<string, PersonAccumulator> People => _people;

    public string GroupKey => BuildGroupKey(Division, KeyPersonName, Members);

    public int BridgeWeight => Bridges.Sum(x => x.Weight);

    public void AddDivisionHint(string? division)
    {
        if (string.IsNullOrWhiteSpace(division))
            return;

        var key = division.Trim();
        _divisionHints[key] = _divisionHints.TryGetValue(key, out var current)
            ? current + 1
            : 1;
    }

    public void AddAction(string? actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
            return;

        var key = actionName.Trim();
        _actionCounts[key] = _actionCounts.TryGetValue(key, out var current)
            ? current + 1
            : 1;
    }

    public void AddFrequency(string? frequency)
    {
        if (!string.IsNullOrWhiteSpace(frequency))
            Frequencies.Add(frequency.Trim());
    }

    public void AddMessageCount(int count)
    {
        if (count > 0)
            MessageCount += count;
    }

    public void AddInternalConnectionWeight(int weight)
    {
        if (weight > 0)
            InternalConnectionWeight += weight;
    }

    public void AddPerson(PersonAccumulator person)
    {
        Members.Add(person.Name);

        if (_people.TryGetValue(person.Name, out var existing))
        {
            existing.MergeFrom(person);
            return;
        }

        _people[person.Name] = person.Clone();
    }

    public void MergeFrom(GroupAccumulator other)
    {
        foreach (var frequency in other.Frequencies)
            Frequencies.Add(frequency);

        foreach (var member in other.Members)
            Members.Add(member);

        foreach (var hint in other._divisionHints)
        {
            _divisionHints[hint.Key] = _divisionHints.TryGetValue(hint.Key, out var current)
                ? current + hint.Value
                : hint.Value;
        }

        foreach (var action in other._actionCounts)
        {
            _actionCounts[action.Key] = _actionCounts.TryGetValue(action.Key, out var current)
                ? current + action.Value
                : action.Value;
        }

        foreach (var person in other._people.Values)
            AddPerson(person);

        MessageCount += other.MessageCount;
        InternalConnectionWeight += other.InternalConnectionWeight;

        Division = null;
        KeyPersonName = string.Empty;
        KeyPersonRole = null;
    }

    public void FinalizeCoreProperties()
    {
        Division = _divisionHints
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefault();

        var rankedPeople = GetRankedPeople();
        var keyPerson = rankedPeople.FirstOrDefault();

        if (keyPerson is null)
        {
            KeyPersonName = string.Empty;
            KeyPersonRole = null;
            return;
        }

        KeyPersonName = keyPerson.Name;
        KeyPersonRole = keyPerson.Role;
    }

    /// <summary>
    /// Повертає ядро представників групи, через яких дозволяється шукати міжгрупові зв'язки.
    /// KeyPerson залишається display-центром групи, але міст не обмежується лише ним.
    /// </summary>
    public IReadOnlyList<string> GetBridgeRepresentatives(int take = 3)
    {
        FinalizeCoreProperties();

        return [.. GetBridgeRepresentativePeople()
            .Take(Math.Max(1, take))
            .Select(x => x.Name)];
    }

    public LinkMapGroupDto ToModel(IReadOnlyDictionary<string, int> groupCountByMember)
    {
        FinalizeCoreProperties();

        var orderedBridges = Bridges
            .OrderByDescending(x => x.Weight)
            .ThenBy(x => x.TargetDivision ?? string.Empty)
            .ThenBy(x => x.ContactPersonName)
            .Select(x => new LinkMapBridgeDto(
                x.TargetGroupKey,
                x.TargetDivision,
                x.ContactPersonName,
                x.BridgeFrequency,
                x.Weight,
                x.PrimaryAction,
                x.TopActions))
            .ToList();

        var orderedMembers = Members
            .OrderByDescending(x => string.Equals(x, KeyPersonName, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => _people.TryGetValue(x, out var person) ? person.UniquePartnerCount : 0)
            .ThenBy(x => x)
            .ToList();

        var memberDetails = orderedMembers
            .Select(name =>
            {
                var person = _people[name];
                var groupCount = groupCountByMember.TryGetValue(name, out var count) ? count : 1;

                return new LinkMapMemberDto(
                    name,
                    person.Role,
                    person.Mentions,
                    person.UniquePartnerCount,
                    person.ConnectionWeight,
                    person.LastSeenAt,
                    groupCount,
                    groupCount > 1,
                    string.Equals(name, KeyPersonName, StringComparison.OrdinalIgnoreCase));
            })
            .ToList();

        var topActions = _actionCounts
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Take(5)
            .Select(x => x.Key)
            .ToList();

        return new LinkMapGroupDto(
            GroupKey,
            Division,
            [.. Frequencies.OrderBy(x => x)],
            KeyPersonName,
            KeyPersonRole,
            orderedMembers,
            memberDetails,
            MessageCount,
            InternalConnectionWeight,
            BridgeWeight,
            topActions.FirstOrDefault(),
            topActions,
            orderedBridges);
    }

    private List<PersonAccumulator> GetRankedPeople()
    {
        return [.. _people.Values
            .OrderByDescending(x => x.UniquePartnerCount)
            .ThenByDescending(x => x.Frequencies.Count)
            .ThenByDescending(x => LinkMapService.IsCenterCandidate(x.Name, x.Role))
            .ThenByDescending(x => x.ConnectionWeight)
            .ThenByDescending(x => x.Mentions)
            .ThenBy(x => x.Name.Length)
            .ThenByDescending(x => x.LastSeenAt)
            .ThenBy(x => x.Name)];
    }

    private List<PersonAccumulator> GetBridgeRepresentativePeople()
    {
        return [.. _people.Values
            .OrderByDescending(x => string.Equals(x.Name, KeyPersonName, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.UniquePartnerCount)
            .ThenByDescending(x => x.Mentions)
            .ThenByDescending(x => x.ConnectionWeight)
            .ThenByDescending(x => LinkMapService.IsCenterCandidate(x.Name, x.Role))
            .ThenBy(x => x.Name.Length)
            .ThenByDescending(x => x.Frequencies.Count)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static string BuildGroupKey(string? division, string keyPersonName, IEnumerable<string> members)
    {
        var divisionPart = string.IsNullOrWhiteSpace(division)
            ? "NO-DIVISION"
            : NormalizeKey(division);

        var keyPersonPart = string.IsNullOrWhiteSpace(keyPersonName)
            ? "NO-KEY-PERSON"
            : NormalizeKey(keyPersonName);

        var membersAnchor = members
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(NormalizeKey)
            .ToList();

        return $"{divisionPart}|{keyPersonPart}|{string.Join("+", membersAnchor)}";
    }

    private static string NormalizeKey(string value)
        => value.Trim().ToUpperInvariant();
}
