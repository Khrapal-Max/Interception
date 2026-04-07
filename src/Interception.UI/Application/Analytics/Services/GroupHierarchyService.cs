//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Будує ієрархію груп без зміни домену:
/// якщо учасник опорної групи є центром іншої стійкої групи,
/// це трактується як ієрархічний перехід у дочірній контур.
/// </summary>
public sealed class GroupHierarchyService(ILinkMapService linkMapService) : IGroupHierarchyService
{
    private readonly ILinkMapService _linkMapService = linkMapService;

    /// <inheritdoc />
    public async Task<GroupHierarchyDto> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var map = await _linkMapService.BuildAsync(dateFrom, dateTo, ct);
        if (map.Groups.Count == 0)
            return new GroupHierarchyDto([]);

        var groups = map.Groups
            .Where(x => !string.IsNullOrWhiteSpace(x.KeyPersonName))
            .ToList();

        var groupByCenter = groups
            .GroupBy(x => x.KeyPersonName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(g => g.Members.Count)
                      .ThenByDescending(g => g.InternalConnectionWeight)
                      .ThenByDescending(g => g.MentionCount)
                      .ThenBy(g => g.GroupKey, StringComparer.OrdinalIgnoreCase)
                      .First(),
                StringComparer.OrdinalIgnoreCase);

        var candidates = BuildEdgeCandidates(groups, groupByCenter);
        var parentByChild = SelectBestParents(candidates);
        RemoveCycles(parentByChild);

        var childrenByParent = parentByChild.Values
            .GroupBy(x => x.ParentGroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(c => c.Score)
                      .ThenByDescending(c => c.ChildGroup.Members.Count)
                      .ThenBy(c => c.ChildGroup.KeyPersonName, StringComparer.OrdinalIgnoreCase)
                      .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var rootGroups = groups
            .Where(x => !parentByChild.ContainsKey(x.GroupKey))
            .OrderByDescending(x => childrenByParent.TryGetValue(x.GroupKey, out var children) ? children.Count : 0)
            .ThenByDescending(x => x.Members.Count)
            .ThenByDescending(x => x.BridgeWeight)
            .ThenBy(x => x.KeyPersonName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var clusters = new List<GroupHierarchyClusterDto>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in rootGroups)
        {
            var cluster = BuildCluster(root, childrenByParent, visited);
            if (cluster is not null)
                clusters.Add(cluster);
        }

        foreach (var group in groups.Where(x => !visited.Contains(x.GroupKey)))
        {
            var cluster = BuildCluster(group, childrenByParent, visited);
            if (cluster is not null)
                clusters.Add(cluster);
        }

        var ordered = clusters
            .OrderByDescending(x => x.TransitionCount)
            .ThenByDescending(x => x.TotalGroups)
            .ThenByDescending(x => x.TotalUniqueMembers)
            .ThenBy(x => x.RootCenterName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GroupHierarchyDto(ordered);
    }

    private static List<EdgeCandidate> BuildEdgeCandidates(
        IReadOnlyList<LinkMapGroupDto> groups,
        IReadOnlyDictionary<string, LinkMapGroupDto> groupByCenter)
    {
        var result = new List<EdgeCandidate>();

        foreach (var parent in groups)
        {
            foreach (var member in parent.MemberDetails.Where(x => !x.IsKeyPerson))
            {
                if (!groupByCenter.TryGetValue(member.Name, out var child))
                    continue;

                if (string.Equals(parent.GroupKey, child.GroupKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                var hasDirectBridge = parent.Bridges.Any(x =>
                    string.Equals(x.TargetGroupKey, child.GroupKey, StringComparison.OrdinalIgnoreCase));

                var score = member.ConnectionWeight * 100
                            + member.MentionCount * 10
                            + child.Members.Count * 3
                            + child.InternalConnectionWeight
                            + (hasDirectBridge ? 50 : 0);

                result.Add(new EdgeCandidate(
                    parent,
                    child,
                    member.Name,
                    member.Role,
                    hasDirectBridge,
                    score));
            }
        }

        return [.. result
            .GroupBy(x => x.ParentGroupKey + "||" + x.ChildGroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(y => y.Score)
                          .ThenByDescending(y => y.ParentGroup.Members.Count)
                          .ThenBy(y => y.ChildGroup.KeyPersonName, StringComparer.OrdinalIgnoreCase)
                          .First())];
    }

    private static Dictionary<string, EdgeCandidate> SelectBestParents(IReadOnlyList<EdgeCandidate> candidates)
    {
        return candidates
            .GroupBy(x => x.ChildGroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.Score)
                      .ThenByDescending(y => y.ParentGroup.Members.Count)
                      .ThenBy(y => y.ParentGroup.KeyPersonName, StringComparer.OrdinalIgnoreCase)
                      .First(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static void RemoveCycles(Dictionary<string, EdgeCandidate> parentByChild)
    {
        while (true)
        {
            var cycle = FindCycle(parentByChild);
            if (cycle.Count == 0)
                return;

            var weakest = cycle
                .OrderBy(x => x.Score)
                .ThenBy(x => x.ParentGroup.Members.Count)
                .ThenBy(x => x.ChildGroup.Members.Count)
                .First();

            parentByChild.Remove(weakest.ChildGroupKey);
        }
    }

    private static List<EdgeCandidate> FindCycle(IReadOnlyDictionary<string, EdgeCandidate> parentByChild)
    {
        foreach (var start in parentByChild.Keys)
        {
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var path = new List<EdgeCandidate>();
            var currentChild = start;

            while (parentByChild.TryGetValue(currentChild, out var edge))
            {
                if (seen.TryGetValue(currentChild, out var index))
                    return [.. path.Skip(index)];

                seen[currentChild] = path.Count;
                path.Add(edge);
                currentChild = edge.ParentGroupKey;
            }
        }

        return [];
    }

    private static GroupHierarchyClusterDto? BuildCluster(
        LinkMapGroupDto root,
        IReadOnlyDictionary<string, List<EdgeCandidate>> childrenByParent,
        ISet<string> visited)
    {
        var nodes = new List<GroupHierarchyNodeDto>();
        var transitions = new List<GroupHierarchyTransitionDto>();
        var uniqueMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Traverse(
            root,
            level: 0,
            parent: null,
            transition: null,
            childrenByParent,
            visited,
            nodes,
            transitions,
            uniqueMembers);

        if (nodes.Count == 0)
            return null;

        var directBridgeCount = transitions.Count(x => x.HasDirectBridge);
        var orderedNodes = nodes
            .OrderBy(x => x.Level)
            .ThenByDescending(x => x.ChildCount)
            .ThenByDescending(x => x.MemberCount)
            .ThenBy(x => x.CenterName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new GroupHierarchyClusterDto(
            ClusterKey: root.GroupKey,
            RootGroupKey: root.GroupKey,
            RootCenterName: root.KeyPersonName,
            RootCenterRole: root.KeyPersonRole,
            Division: root.Division,
            Frequencies: root.Frequencies,
            PrimaryAction: root.PrimaryAction,
            TopActions: root.TopActions,
            TotalGroups: orderedNodes.Count,
            TotalUniqueMembers: uniqueMembers.Count,
            TransitionCount: transitions.Count,
            DirectBridgeCount: directBridgeCount,
            Nodes: orderedNodes,
            Transitions: transitions);
    }

    private static void Traverse(
        LinkMapGroupDto current,
        int level,
        LinkMapGroupDto? parent,
        EdgeCandidate? transition,
        IReadOnlyDictionary<string, List<EdgeCandidate>> childrenByParent,
        ISet<string> visited,
        ICollection<GroupHierarchyNodeDto> nodes,
        ICollection<GroupHierarchyTransitionDto> transitions,
        ISet<string> uniqueMembers)
    {
        if (!visited.Add(current.GroupKey))
            return;

        foreach (var member in current.Members)
            uniqueMembers.Add(member);

        var childEdges = childrenByParent.TryGetValue(current.GroupKey, out var foundChildren)
            ? foundChildren
            : [];

        nodes.Add(new GroupHierarchyNodeDto(
            GroupKey: current.GroupKey,
            Level: level,
            CenterName: current.KeyPersonName,
            CenterRole: current.KeyPersonRole,
            Division: current.Division,
            Frequencies: current.Frequencies,
            PrimaryAction: current.PrimaryAction,
            TopActions: current.TopActions,
            MemberCount: current.Members.Count,
            BridgeCount: current.Bridges.Count,
            IsRoot: parent is null,
            ParentGroupKey: parent?.GroupKey,
            ParentCenterName: parent?.KeyPersonName,
            TransitionMemberName: transition?.TransitionMemberName,
            TransitionMemberRole: transition?.TransitionMemberRole,
            HasDirectBridgeToParent: transition?.HasDirectBridge ?? false,
            ChildCount: childEdges.Count));

        if (transition is not null && parent is not null)
        {
            transitions.Add(new GroupHierarchyTransitionDto(
                ParentGroupKey: parent.GroupKey,
                ParentCenterName: parent.KeyPersonName,
                ChildGroupKey: current.GroupKey,
                ChildCenterName: current.KeyPersonName,
                TransitionMemberName: transition.TransitionMemberName,
                TransitionMemberRole: transition.TransitionMemberRole,
                HasDirectBridge: transition.HasDirectBridge,
                Score: transition.Score));
        }

        foreach (var childEdge in childEdges)
            Traverse(childEdge.ChildGroup, level + 1, current, childEdge, childrenByParent, visited, nodes, transitions, uniqueMembers);
    }

    private sealed record EdgeCandidate(
        LinkMapGroupDto ParentGroup,
        LinkMapGroupDto ChildGroup,
        string TransitionMemberName,
        string? TransitionMemberRole,
        bool HasDirectBridge,
        int Score)
    {
        public string ParentGroupKey => ParentGroup.GroupKey;
        public string ChildGroupKey => ChildGroup.GroupKey;
    }
}
