//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Будує операторську ієрархію груп поверх уже зібраної карти зв'язків.
/// </summary>
public sealed class GroupHierarchyService(ILinkMapService linkMapService) : IGroupHierarchyService
{
    private readonly ILinkMapService _linkMapService = linkMapService;

    /// <inheritdoc />
    public async Task<GroupHierarchyMapDto> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var map = await _linkMapService.BuildAsync(dateFrom, dateTo, ct);
        if (map.Groups.Count == 0)
            return new GroupHierarchyMapDto([], 0, 0, 0, 0);

        var groupsByKey = map.Groups.ToDictionary(x => x.GroupKey, StringComparer.OrdinalIgnoreCase);
        var candidateEdges = BuildCandidateEdges(map.Groups);
        var reviewGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var selectedEdges = SelectBestEdges(candidateEdges, reviewGroups);
        selectedEdges = RemoveCycles(selectedEdges, reviewGroups);

        var incomingByChild = selectedEdges
            .GroupBy(x => x.ChildGroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

        var childrenByParent = selectedEdges
            .GroupBy(x => x.ParentGroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Score).ThenBy(y => y.ChildTitle).ToList(), StringComparer.OrdinalIgnoreCase);

        var roots = map.Groups
            .Where(x => !incomingByChild.ContainsKey(x.GroupKey))
            .OrderByDescending(x => childrenByParent.TryGetValue(x.GroupKey, out var children) ? children.Count : 0)
            .ThenByDescending(x => x.Members.Count)
            .ThenBy(x => BuildGroupTitle(x))
            .ToList();

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clusters = new List<GroupHierarchyClusterDto>();

        foreach (var root in roots)
        {
            if (visited.Contains(root.GroupKey))
                continue;

            clusters.Add(BuildCluster(root, groupsByKey, childrenByParent, reviewGroups, visited));
        }

        foreach (var group in map.Groups.OrderByDescending(x => x.Members.Count).ThenBy(x => BuildGroupTitle(x)))
        {
            if (visited.Contains(group.GroupKey))
                continue;

            reviewGroups.Add(group.GroupKey);
            clusters.Add(BuildCluster(group, groupsByKey, childrenByParent, reviewGroups, visited));
        }

        var crossGroupNodeCount = selectedEdges
            .Select(x => x.ViaMemberName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new GroupHierarchyMapDto(
            Clusters: [.. clusters.OrderByDescending(x => x.Nodes.Count).ThenBy(x => x.Root.Title)],
            RootGroupCount: clusters.Count,
            ChildGroupCount: selectedEdges.Count,
            CrossGroupNodeCount: crossGroupNodeCount,
            NeedsReviewCount: reviewGroups.Count);
    }

    /// <summary>
    /// Шукає усі можливі parent → child зв'язки через центри інших груп.
    /// </summary>
    private static List<CandidateEdge> BuildCandidateEdges(IReadOnlyList<LinkMapGroupDto> groups)
    {
        var byKeyPerson = groups
            .GroupBy(x => x.KeyPersonName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

        var result = new List<CandidateEdge>();

        foreach (var parent in groups)
        {
            foreach (var member in parent.MemberDetails.Where(x => !x.IsKeyPerson && !string.IsNullOrWhiteSpace(x.Name)))
            {
                if (!byKeyPerson.TryGetValue(member.Name.Trim(), out var matchedGroups))
                    continue;

                foreach (var child in matchedGroups.Where(x => !x.GroupKey.Equals(parent.GroupKey, StringComparison.OrdinalIgnoreCase)))
                {
                    var score = member.MentionCount * 1000
                                + member.ConnectionWeight * 100
                                + member.UniquePartnerCount * 10
                                + (HasDirectBridge(parent, child.GroupKey) ? 25 : 0)
                                + (HasMeaningfulSharedDivision(parent.Division, child.Division) ? 5 : 0);

                    result.Add(new CandidateEdge(
                        ParentGroupKey: parent.GroupKey,
                        ParentTitle: BuildGroupTitle(parent),
                        ChildGroupKey: child.GroupKey,
                        ChildTitle: BuildGroupTitle(child),
                        ViaMemberName: member.Name,
                        ViaMemberRole: member.Role,
                        Score: score,
                        IsAmbiguous: matchedGroups.Count > 1));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Вибирає одного основного батька для кожної дочірньої групи.
    /// </summary>
    private static List<CandidateEdge> SelectBestEdges(
        IReadOnlyList<CandidateEdge> candidateEdges,
        HashSet<string> reviewGroups)
    {
        var selected = new List<CandidateEdge>();

        foreach (var childGroup in candidateEdges.GroupBy(x => x.ChildGroupKey, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = childGroup
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.ParentTitle)
                .ToList();

            var chosen = ordered[0];
            selected.Add(chosen);

            if (ordered.Count > 1)
                reviewGroups.Add(chosen.ChildGroupKey);

            if (ordered.Any(x => x.IsAmbiguous))
                reviewGroups.Add(chosen.ChildGroupKey);
        }

        return selected;
    }

    /// <summary>
    /// Забирає ребра, що створюють цикл у дереві підпорядкування.
    /// </summary>
    private static List<CandidateEdge> RemoveCycles(
        IReadOnlyList<CandidateEdge> selectedEdges,
        HashSet<string> reviewGroups)
    {
        var accepted = new List<CandidateEdge>();

        foreach (var edge in selectedEdges
                     .OrderByDescending(x => x.Score)
                     .ThenBy(x => x.ParentTitle)
                     .ThenBy(x => x.ChildTitle))
        {
            if (WouldCreateCycle(edge, accepted))
            {
                reviewGroups.Add(edge.ParentGroupKey);
                reviewGroups.Add(edge.ChildGroupKey);
                continue;
            }

            accepted.Add(edge);
        }

        return accepted;
    }

    /// <summary>
    /// Будує один завершений кластер від опорної групи вниз.
    /// </summary>
    private static GroupHierarchyClusterDto BuildCluster(
        LinkMapGroupDto root,
        IReadOnlyDictionary<string, LinkMapGroupDto> groupsByKey,
        IReadOnlyDictionary<string, List<CandidateEdge>> childrenByParent,
        IReadOnlySet<string> reviewGroups,
        HashSet<string> visited)
    {
        var nodes = new List<GroupHierarchyNodeDto>();
        var edges = new List<GroupHierarchyEdgeDto>();
        var actionRows = new List<GroupHierarchyActionRowDto>();
        var clusterReview = reviewGroups.Contains(root.GroupKey);

        Traverse(root, depth: 0);

        return new GroupHierarchyClusterDto(
            ClusterKey: root.GroupKey,
            Root: nodes[0],
            Nodes: nodes,
            Edges: edges,
            ActionRows: actionRows,
            Summary: BuildSummary(nodes, edges),
            NeedsReview: clusterReview || nodes.Any(x => x.NeedsReview));

        void Traverse(LinkMapGroupDto group, int depth)
        {
            if (!visited.Add(group.GroupKey))
                return;

            var node = ToNode(group, depth, isRoot: depth == 0, reviewGroups.Contains(group.GroupKey));
            nodes.Add(node);

            actionRows.Add(new GroupHierarchyActionRowDto(
                GroupKey: group.GroupKey,
                GroupTitle: node.Title,
                PrimaryAction: BuildActionLabel(group),
                Signal: depth == 0 ? "опорний контур" : BuildSignal(group.GroupKey, edges),
                Depth: depth));

            if (!childrenByParent.TryGetValue(group.GroupKey, out var children))
                return;

            foreach (var child in children)
            {
                if (!groupsByKey.TryGetValue(child.ChildGroupKey, out var childGroup))
                    continue;

                var edge = new GroupHierarchyEdgeDto(
                    ParentGroupKey: child.ParentGroupKey,
                    ChildGroupKey: child.ChildGroupKey,
                    ViaMemberName: child.ViaMemberName,
                    ViaMemberRole: child.ViaMemberRole,
                    IsAmbiguous: child.IsAmbiguous,
                    Depth: depth + 1);

                edges.Add(edge);

                if (child.IsAmbiguous)
                    clusterReview = true;

                Traverse(childGroup, depth + 1);
            }
        }
    }

    /// <summary>
    /// Перевіряє, чи додавання ребра створить цикл.
    /// </summary>
    private static bool WouldCreateCycle(CandidateEdge edge, IReadOnlyList<CandidateEdge> accepted)
    {
        var stack = new Stack<string>();
        stack.Push(edge.ChildGroupKey);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
                continue;

            if (current.Equals(edge.ParentGroupKey, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var next in accepted.Where(x => x.ParentGroupKey.Equals(current, StringComparison.OrdinalIgnoreCase)))
                stack.Push(next.ChildGroupKey);
        }

        return false;
    }

    /// <summary>
    /// Перетворює групу карти зв'язків у вузол ієрархії.
    /// </summary>
    private static GroupHierarchyNodeDto ToNode(LinkMapGroupDto group, int depth, bool isRoot, bool needsReview)
        => new(
            GroupKey: group.GroupKey,
            Title: BuildGroupTitle(group),
            Division: group.Division,
            KeyPersonName: group.KeyPersonName,
            KeyPersonRole: group.KeyPersonRole,
            Frequencies: group.Frequencies,
            PrimaryAction: group.PrimaryAction,
            TopActions: group.TopActions,
            MemberCount: group.Members.Count,
            BridgeCount: group.Bridges.Count,
            Depth: depth,
            IsRoot: isRoot,
            NeedsReview: needsReview);

    /// <summary>
    /// Будує короткий підсумок кластера для оператора.
    /// </summary>
    private static string BuildSummary(
        IReadOnlyList<GroupHierarchyNodeDto> nodes,
        IReadOnlyList<GroupHierarchyEdgeDto> edges)
    {
        var root = nodes[0];
        var directChildren = edges
            .Where(x => x.ParentGroupKey.Equals(root.GroupKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (directChildren.Count == 0)
            return $"Група {root.Title} поки не показує підлеглих контурів за поточним періодом.";

        var names = nodes
            .Where(x => directChildren.Any(e => e.ChildGroupKey.Equals(x.GroupKey, StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.KeyPersonName)
            .Take(3)
            .ToList();

        var childSuffix = directChildren.Count == 1 ? "дочірній контур" : "дочірні контури";
        return $"Група {root.Title} є опорною. Особи {string.Join(", ", names)} входять до її складу та є центрами власних груп. Це показує {childSuffix} навколо опорної групи.";
    }

    /// <summary>
    /// Формує коротку назву групи для операторського списку.
    /// </summary>
    private static string BuildGroupTitle(LinkMapGroupDto group)
    {
        if (!string.IsNullOrWhiteSpace(group.Division))
            return group.Division!.Trim();

        return $"Група {group.KeyPersonName}";
    }

    /// <summary>
    /// Формує стислий опис основної дії групи.
    /// </summary>
    private static string BuildActionLabel(LinkMapGroupDto group)
    {
        if (!string.IsNullOrWhiteSpace(group.PrimaryAction))
            return group.PrimaryAction!;

        if (group.TopActions.Count > 0)
            return string.Join(", ", group.TopActions.Take(2));

        return "дія не визначена";
    }

    /// <summary>
    /// Формує текст сигналу для дочірнього рядка.
    /// </summary>
    private static string BuildSignal(string groupKey, IReadOnlyList<GroupHierarchyEdgeDto> edges)
    {
        var edge = edges.LastOrDefault(x => x.ChildGroupKey.Equals(groupKey, StringComparison.OrdinalIgnoreCase));
        return edge is null ? "контур" : $"дочірня група через {edge.ViaMemberName}";
    }

    /// <summary>
    /// Перевіряє наявність прямого bridge-зв'язку між групами.
    /// </summary>
    private static bool HasDirectBridge(LinkMapGroupDto parent, string targetGroupKey)
        => parent.Bridges.Any(x => x.TargetGroupKey.Equals(targetGroupKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Перевіряє, чи в двох груп є однаковий осмислений підрозділ.
    /// </summary>
    private static bool HasMeaningfulSharedDivision(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
           && !string.IsNullOrWhiteSpace(right)
           && !left.Equals("НВ підрозділ", StringComparison.OrdinalIgnoreCase)
           && left.Equals(right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Технічна candidate-модель ребра до вибору найкращого батька.
    /// </summary>
    private sealed record CandidateEdge(
        string ParentGroupKey,
        string ParentTitle,
        string ChildGroupKey,
        string ChildTitle,
        string ViaMemberName,
        string? ViaMemberRole,
        int Score,
        bool IsAmbiguous);
}
