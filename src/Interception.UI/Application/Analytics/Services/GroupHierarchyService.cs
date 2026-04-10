//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Domain.Enums;
using Interception.UI.Domain.Policies;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Будує операторську ієрархію груп поверх уже зібраної карти зв'язків.
/// Додатково враховує explicit structural links з <see cref="PersonDirectiveRelation"/> як сильний direction hint.
/// </summary>
public sealed class GroupHierarchyService(
    ILinkMapService linkMapService,
    IDbContextFactory<AppDbContext> dbFactory) : IGroupHierarchyService
{
    private readonly ILinkMapService _linkMapService = linkMapService;
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<GroupHierarchyMapDto> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var map = await _linkMapService.BuildAsync(dateFrom, dateTo, ct);
        if (map.Groups.Count == 0)
            return new GroupHierarchyMapDto([], 0, 0, 0, 0);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var canonicalByName = await LoadCanonicalByNameMapAsync(db, map.Groups, ct);
        var directiveHints = await LoadDirectiveHintsAsync(db, ct);

        var groupsByKey = map.Groups.ToDictionary(x => x.GroupKey, StringComparer.OrdinalIgnoreCase);
        var candidateEdges = BuildCandidateEdges(map.Groups, canonicalByName, directiveHints);
        var reviewGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var selectedEdges = SelectBestEdges(candidateEdges, reviewGroups);
        selectedEdges = RemoveCycles(selectedEdges, reviewGroups);

        var incomingByChild = selectedEdges
            .GroupBy(x => x.ChildGroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

        var childrenByParent = selectedEdges
            .GroupBy(x => x.ParentGroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.Score).ThenByDescending(y => y.IsDirective).ThenBy(y => y.ChildTitle).ToList(),
                StringComparer.OrdinalIgnoreCase);

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
    /// Шукає усі можливі parent → child зв'язки.
    /// Базова евристика: центр іншої групи входить до складу parent-групи.
    /// Додатковий підсилювач: явний structural link з PersonDirectiveRelation.
    /// </summary>
    private static List<CandidateEdge> BuildCandidateEdges(
        IReadOnlyList<LinkMapGroupDto> groups,
        IReadOnlyDictionary<string, Guid> canonicalByName,
        IReadOnlyList<DirectiveHint> directiveHints)
    {
        var byKeyPersonName = groups
            .Where(x => !string.IsNullOrWhiteSpace(x.KeyPersonName))
            .GroupBy(x => NormalizePersonName(x.KeyPersonName), StringComparer.OrdinalIgnoreCase)
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

        var byKeyPersonCanonical = groups
            .Where(x => !string.IsNullOrWhiteSpace(x.KeyPersonName))
            .Select(x => new
            {
                Group = x,
                CanonicalId = canonicalByName.TryGetValue(NormalizePersonName(x.KeyPersonName), out var canonicalId)
                    ? canonicalId
                    : Guid.Empty
            })
            .Where(x => x.CanonicalId != Guid.Empty)
            .GroupBy(x => x.CanonicalId)
            .ToDictionary(x => x.Key, x => x.Select(v => v.Group).ToList());

        var result = new List<CandidateEdge>();

        // 1. Базові candidate edges з карти зв'язків.
        foreach (var parent in groups)
        {
            foreach (var member in parent.MemberDetails.Where(x => !x.IsKeyPerson && !string.IsNullOrWhiteSpace(x.Name)))
            {
                var matchedGroups = ResolveMatchedGroups(
                    NormalizePersonName(member.Name),
                    canonicalByName,
                    byKeyPersonName,
                    byKeyPersonCanonical);

                foreach (var child in matchedGroups
                             .Where(x => !x.GroupKey.Equals(parent.GroupKey, StringComparison.OrdinalIgnoreCase))
                             .DistinctBy(x => x.GroupKey, StringComparer.OrdinalIgnoreCase))
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
                        IsAmbiguous: matchedGroups.Count > 1,
                        IsDirective: false,
                        DirectiveLabel: null));
                }
            }
        }

        // 2. Явні relations між особами як сильний direction hint.
        foreach (var directive in directiveHints)
        {
            var parents = ResolveMatchedGroups(
                directive.FromNormalizedName,
                canonicalByName,
                byKeyPersonName,
                byKeyPersonCanonical,
                directive.FromCanonicalPersonId);

            var children = ResolveMatchedGroups(
                directive.ToNormalizedName,
                canonicalByName,
                byKeyPersonName,
                byKeyPersonCanonical,
                directive.ToCanonicalPersonId);

            var distinctParents = parents.DistinctBy(x => x.GroupKey, StringComparer.OrdinalIgnoreCase).ToList();
            var distinctChildren = children.DistinctBy(x => x.GroupKey, StringComparer.OrdinalIgnoreCase).ToList();

            if (distinctParents.Count == 0 || distinctChildren.Count == 0)
                continue;

            var isAmbiguous = distinctParents.Count > 1 || distinctChildren.Count > 1;

            foreach (var parent in distinctParents)
            {
                foreach (var child in distinctChildren.Where(x => !x.GroupKey.Equals(parent.GroupKey, StringComparison.OrdinalIgnoreCase)))
                {
                    var score = 1_000_000
                                + DirectiveRelationPolicy.GetConfidenceScore(directive.Confidence)
                                + DirectiveRelationPolicy.GetTypeScore(directive.RelationType)
                                + (HasDirectBridge(parent, child.GroupKey) ? 25 : 0)
                                + (HasMeaningfulSharedDivision(parent.Division, child.Division) ? 5 : 0);

                    result.Add(new CandidateEdge(
                        ParentGroupKey: parent.GroupKey,
                        ParentTitle: BuildGroupTitle(parent),
                        ChildGroupKey: child.GroupKey,
                        ChildTitle: BuildGroupTitle(child),
                        ViaMemberName: directive.FromDisplayName,
                        ViaMemberRole: null,
                        Score: score,
                        IsAmbiguous: isAmbiguous,
                        IsDirective: true,
                        DirectiveLabel: DirectiveRelationPolicy.BuildLabel(directive.RelationType, directive.Confidence)));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Завантажує явні structural links для підсилення напряму parent → child.
    /// </summary>
    private static async Task<List<DirectiveHint>> LoadDirectiveHintsAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var relations = await db.PersonDirectiveRelations
            .AsNoTracking()
            .ToListAsync(ct);

        if (relations.Count == 0)
            return [];

        var canonicalIds = relations
            .SelectMany(x => new[] { x.FromCanonicalPersonId, x.ToCanonicalPersonId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var resolvedIds = relations
            .SelectMany(x => new[] { x.FromResolvedParticipantId, x.ToResolvedParticipantId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var canonicalMap = await db.CanonicalPersons
            .AsNoTracking()
            .Where(x => canonicalIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => SemanticValueExtensions.NormalizeMeaningfulOrNull(x.DisplayName) ?? "—",
                ct);

        var resolvedRows = await db.ResolvedParticipants
            .AsNoTracking()
            .Where(x => resolvedIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Name
            })
            .ToListAsync(ct);

        var resolvedMap = resolvedRows.ToDictionary(
            x => x.Id,
            x => SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Name) ?? "—");

        var resolvedToCanonicalRows = await db.CanonicalPersonMembers
            .AsNoTracking()
            .Where(x => resolvedIds.Contains(x.ResolvedParticipantId))
            .Select(x => new
            {
                x.ResolvedParticipantId,
                x.CanonicalPersonId
            })
            .ToListAsync(ct);

        var resolvedToCanonicalMap = resolvedToCanonicalRows
            .GroupBy(x => x.ResolvedParticipantId)
            .Where(x => x.Select(v => v.CanonicalPersonId).Distinct().Count() == 1)
            .ToDictionary(x => x.Key, x => x.First().CanonicalPersonId);

        return relations
            .Select(x => BuildDirectiveHint(x, canonicalMap, resolvedMap, resolvedToCanonicalMap))
            .Where(x => x is not null)
            .Cast<DirectiveHint>()
            .ToList();
    }

    /// <summary>
    /// Завантажує однозначну canonical-прив'язку для імен, які вже зведені в аналітиці.
    /// </summary>
    private static async Task<Dictionary<string, Guid>> LoadCanonicalByNameMapAsync(
        AppDbContext db,
        IReadOnlyList<LinkMapGroupDto> groups,
        CancellationToken ct)
    {
        var names = groups
            .Select(x => x.KeyPersonName)
            .Concat(groups.SelectMany(x => x.MemberDetails.Select(v => v.Name)))
            .Select(NormalizePersonName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (names.Count == 0)
            return [];

        var rows = await (
                from member in db.CanonicalPersonMembers.AsNoTracking()
                join resolved in db.ResolvedParticipants.AsNoTracking() on member.ResolvedParticipantId equals resolved.Id
                where !string.IsNullOrWhiteSpace(resolved.Name)
                select new
                {
                    member.CanonicalPersonId,
                    resolved.Name
                })
            .ToListAsync(ct);

        return rows
            .Select(x => new
            {
                x.CanonicalPersonId,
                Name = NormalizePersonName(x.Name)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && names.Contains(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Select(v => v.CanonicalPersonId).Distinct().Count() == 1)
            .ToDictionary(x => x.Key, x => x.First().CanonicalPersonId, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizePersonName(string? name)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(name)?.Trim().ToUpperInvariant() ?? string.Empty;

    /// <summary>
    /// Вибирає одного основного батька для кожної дочірньої групи.
    /// Explicit relation має пріоритет над звичайною graph-евристикою.
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
                .ThenByDescending(x => x.IsDirective)
                .ThenBy(x => x.ParentTitle)
                .ToList();

            var chosen = ordered[0];
            selected.Add(chosen);

            if (ordered.Count > 1)
                reviewGroups.Add(chosen.ChildGroupKey);

            if (ordered.Any(x => x.IsAmbiguous))
                reviewGroups.Add(chosen.ChildGroupKey);

            if (ordered.Any(x => x.IsDirective) && ordered.Any(x => !x.IsDirective))
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
                     .ThenByDescending(x => x.IsDirective)
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
                    Depth: depth + 1,
                    IsDirective: child.IsDirective,
                    DirectiveLabel: child.DirectiveLabel);

                edges.Add(edge);

                if (child.IsAmbiguous || child.IsDirective)
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

        if (directChildren.Any(x => x.IsDirective))
            return $"Група {root.Title} має явні зв'язки структурного керування з дочірніми контурами. Їхній напрямок підсилений ручно підтвердженим контуром керування.";

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
        if (edge is null)
            return "контур";

        return edge.IsDirective
            ? $"дочірня група через {edge.DirectiveLabel ?? "явний зв'язок"}"
            : $"дочірня група через {edge.ViaMemberName}";
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

    private static List<LinkMapGroupDto> ResolveMatchedGroups(
        string? normalizedName,
        IReadOnlyDictionary<string, Guid> canonicalByName,
        IReadOnlyDictionary<string, List<LinkMapGroupDto>> byKeyPersonName,
        IReadOnlyDictionary<Guid, List<LinkMapGroupDto>> byKeyPersonCanonical,
        Guid? explicitCanonicalId = null)
    {
        var matchedGroups = new List<LinkMapGroupDto>();

        if (!string.IsNullOrWhiteSpace(normalizedName)
            && byKeyPersonName.TryGetValue(normalizedName, out var byName))
        {
            matchedGroups.AddRange(byName);
        }

        var canonicalId = explicitCanonicalId;
        if (!canonicalId.HasValue
            && !string.IsNullOrWhiteSpace(normalizedName)
            && canonicalByName.TryGetValue(normalizedName, out var resolvedCanonicalId))
        {
            canonicalId = resolvedCanonicalId;
        }

        if (canonicalId.HasValue
            && byKeyPersonCanonical.TryGetValue(canonicalId.Value, out var byCanonical))
        {
            matchedGroups.AddRange(byCanonical);
        }

        return [.. matchedGroups.DistinctBy(x => x.GroupKey, StringComparer.OrdinalIgnoreCase)];
    }

    private static DirectiveHint? BuildDirectiveHint(
        Interception.UI.Domain.PersonDirectiveRelation relation,
        IReadOnlyDictionary<Guid, string> canonicalMap,
        IReadOnlyDictionary<Guid, string> resolvedMap,
        IReadOnlyDictionary<Guid, Guid> resolvedToCanonicalMap)
    {
        var fromCanonicalId = relation.FromCanonicalPersonId;
        var toCanonicalId = relation.ToCanonicalPersonId;

        string fromDisplayName;
        string toDisplayName;

        if (fromCanonicalId.HasValue)
        {
            if (!canonicalMap.TryGetValue(fromCanonicalId.Value, out fromDisplayName!))
                return null;
        }
        else if (relation.FromResolvedParticipantId.HasValue)
        {
            if (!resolvedMap.TryGetValue(relation.FromResolvedParticipantId.Value, out fromDisplayName!))
                return null;

            if (resolvedToCanonicalMap.TryGetValue(relation.FromResolvedParticipantId.Value, out var mappedCanonical))
                fromCanonicalId = mappedCanonical;
        }
        else
        {
            return null;
        }

        if (toCanonicalId.HasValue)
        {
            if (!canonicalMap.TryGetValue(toCanonicalId.Value, out toDisplayName!))
                return null;
        }
        else if (relation.ToResolvedParticipantId.HasValue)
        {
            if (!resolvedMap.TryGetValue(relation.ToResolvedParticipantId.Value, out toDisplayName!))
                return null;

            if (resolvedToCanonicalMap.TryGetValue(relation.ToResolvedParticipantId.Value, out var mappedCanonical))
                toCanonicalId = mappedCanonical;
        }
        else
        {
            return null;
        }

        return new DirectiveHint(
            FromCanonicalPersonId: fromCanonicalId,
            FromNormalizedName: NormalizePersonName(fromDisplayName),
            FromDisplayName: fromDisplayName,
            ToCanonicalPersonId: toCanonicalId,
            ToNormalizedName: NormalizePersonName(toDisplayName),
            ToDisplayName: toDisplayName,
            RelationType: relation.RelationType,
            Confidence: relation.Confidence,
            Comment: SemanticValueExtensions.NormalizeMeaningfulOrNull(relation.Comment));
    }

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
        bool IsAmbiguous,
        bool IsDirective,
        string? DirectiveLabel);

    private sealed record DirectiveHint(
        Guid? FromCanonicalPersonId,
        string FromNormalizedName,
        string FromDisplayName,
        Guid? ToCanonicalPersonId,
        string ToNormalizedName,
        string ToDisplayName,
        DirectiveRelationType RelationType,
        DirectiveRelationConfidence Confidence,
        string? Comment);
}
