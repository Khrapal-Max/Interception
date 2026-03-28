//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Candidates;
using Interception.UI.Application.Interceptions.Models.PatternRecognition;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services.Candidates;

/// <summary>
/// Будує аналітичну карту зв'язків між особами.
/// </summary>
public sealed class LinkMapService(IDbContextFactory<AppDbContext> dbFactory) : ILinkMapService
{
    private const string UnknownDivision = "НВ підрозділ";
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<LinkMapModel> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.InterceptionMessages
            .AsNoTracking()
            .Include(x => x.Participants)
            .Include(x => x.Labels)
            .AsQueryable();

        if (dateFrom.HasValue)
            query = query.Where(x => x.ObservedDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.ObservedDate <= dateTo.Value);

        var messages = await query.ToListAsync(ct);
        if (messages.Count == 0)
            return new LinkMapModel([], []);

        var frequencyDivisionMap = BuildFrequencyDivisionMap(messages);

        var messageRows = messages
            .Select(x => new
            {
                Message = x,
                EffectiveDivision = ResolveEffectiveDivision(x.Division, x.Frequency, frequencyDivisionMap)
            })
            .ToList();

        var nodes = new Dictionary<string, NodeAccumulator>(StringComparer.OrdinalIgnoreCase);
        var edges = new Dictionary<string, EdgeAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in messageRows)
        {
            var knownParticipants = row.Message.Participants
                .Where(x => !x.IsUnknown && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => new
                {
                    Name = x.Name!.Trim(),
                    x.Role
                })
                .GroupBy(x => ToPersonKey(x.Name), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            foreach (var participant in knownParticipants)
            {
                var key = ToPersonKey(participant.Name);

                if (!nodes.TryGetValue(key, out var node))
                {
                    node = new NodeAccumulator
                    {
                        PersonKey = key,
                        Name = participant.Name
                    };
                    nodes[key] = node;
                }

                if (!string.IsNullOrWhiteSpace(row.EffectiveDivision))
                    node.Divisions.Add(row.EffectiveDivision!);

                if (!string.IsNullOrWhiteSpace(row.Message.Frequency))
                    node.Frequencies.Add(row.Message.Frequency!.Trim());

                if (!string.IsNullOrWhiteSpace(participant.Role))
                {
                    if (node.Role is null || row.Message.ObservedDate >= node.RoleObservedAt)
                    {
                        node.Role = participant.Role!.Trim();
                        node.RoleObservedAt = row.Message.ObservedDate;
                    }
                }
            }

            for (var i = 0; i < knownParticipants.Count; i++)
            {
                for (var j = i + 1; j < knownParticipants.Count; j++)
                {
                    var leftKey = ToPersonKey(knownParticipants[i].Name);
                    var rightKey = ToPersonKey(knownParticipants[j].Name);

                    if (string.Compare(leftKey, rightKey, StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        (leftKey, rightKey) = (rightKey, leftKey);
                    }

                    var edgeKey = $"{leftKey}||{rightKey}";
                    if (!edges.TryGetValue(edgeKey, out var edge))
                    {
                        edge = new EdgeAccumulator
                        {
                            FromPersonKey = leftKey,
                            ToPersonKey = rightKey
                        };
                        edges[edgeKey] = edge;
                    }

                    edge.Weight++;

                    if (!string.IsNullOrWhiteSpace(row.EffectiveDivision))
                        edge.Divisions.Add(row.EffectiveDivision!);

                    if (!string.IsNullOrWhiteSpace(row.Message.Frequency))
                        edge.Frequencies.Add(row.Message.Frequency!.Trim());

                    foreach (var label in row.Message.Labels)
                    {
                        if (!string.IsNullOrWhiteSpace(label.NameLabel))
                            edge.Labels.Add(label.NameLabel.Trim());
                    }
                }
            }
        }

        if (nodes.Count == 0)
            return new LinkMapModel([], []);

        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var nodeKey in nodes.Keys)
            adjacency[nodeKey] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in edges.Values)
        {
            adjacency[edge.FromPersonKey].Add(edge.ToPersonKey);
            adjacency[edge.ToPersonKey].Add(edge.FromPersonKey);
        }

        var nodeModels = nodes.Values
            .Select(node =>
            {
                var neighborKeys = adjacency.TryGetValue(node.PersonKey, out var set)
                    ? set
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var connectionCount = neighborKeys.Count;
                var neighborEdgeCount = CountNeighborEdges(neighborKeys, edges);

                var neighborDivisions = neighborKeys
                    .Where(x => nodes.ContainsKey(x))
                    .SelectMany(x => nodes[x].Divisions)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var isMultiDivisionCandidate = node.Divisions.Count > 1;
                var isBridgeCandidate = isMultiDivisionCandidate && neighborDivisions.Count > 1;
                var isLocalCenterCandidate = connectionCount >= 3 && neighborEdgeCount <= 1;

                return new LinkMapNodeModel(
                    PersonKey: node.PersonKey,
                    Name: node.Name,
                    Role: node.Role,
                    Divisions: node.Divisions.OrderBy(x => x).ToList(),
                    Frequencies: node.Frequencies.OrderBy(x => x).ToList(),
                    ConnectionCount: connectionCount,
                    IsLocalCenterCandidate: isLocalCenterCandidate,
                    IsBridgeCandidate: isBridgeCandidate,
                    IsMultiDivisionCandidate: isMultiDivisionCandidate);
            })
            .OrderBy(x => x.Name)
            .ToList();

        var edgeModels = edges.Values
            .Select(edge => new LinkMapEdgeModel(
                FromPersonKey: edge.FromPersonKey,
                ToPersonKey: edge.ToPersonKey,
                Weight: edge.Weight,
                Divisions: edge.Divisions.OrderBy(x => x).ToList(),
                Frequencies: edge.Frequencies.OrderBy(x => x).ToList(),
                Labels: edge.Labels.OrderBy(x => x).ToList()))
            .OrderBy(x => x.FromPersonKey)
            .ThenBy(x => x.ToPersonKey)
            .ToList();

        return new LinkMapModel(nodeModels, edgeModels);
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
                    .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(x => x.Count())
                    .ThenBy(x => x.Key)
                    .Select(x => x.Key)
                    .FirstOrDefault()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Division))
            .ToDictionary(x => x.Frequency, x => x.Division!, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Повертає ефективний підрозділ для observation.
    /// </summary>
    private static string? ResolveEffectiveDivision(
        string? observedDivision,
        string? frequency,
        IReadOnlyDictionary<string, string> frequencyDivisionMap)
    {
        if (IsMeaningfulDivision(observedDivision))
            return observedDivision!.Trim();

        if (!string.IsNullOrWhiteSpace(frequency)
            && frequencyDivisionMap.TryGetValue(frequency.Trim(), out var division)
            && IsMeaningfulDivision(division))
        {
            return division;
        }

        return null;
    }

    /// <summary>
    /// Рахує кількість ребер між сусідами вузла.
    /// </summary>
    private static int CountNeighborEdges(
        IEnumerable<string> neighbors,
        IReadOnlyDictionary<string, EdgeAccumulator> edges)
    {
        var list = neighbors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var count = 0;

        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                var left = list[i];
                var right = list[j];

                if (string.Compare(left, right, StringComparison.OrdinalIgnoreCase) > 0)
                    (left, right) = (right, left);

                var edgeKey = $"{left}||{right}";
                if (edges.ContainsKey(edgeKey))
                    count++;
            }
        }

        return count;
    }

    private static string ToPersonKey(string name)
        => name.Trim().ToUpperInvariant();

    private static bool IsMeaningfulDivision(string? division)
        => !string.IsNullOrWhiteSpace(division)
           && !string.Equals(division.Trim(), UnknownDivision, StringComparison.OrdinalIgnoreCase);

    private sealed class NodeAccumulator
    {
        public string PersonKey { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Role { get; set; }
        public DateTime RoleObservedAt { get; set; }
        public HashSet<string> Divisions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Frequencies { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class EdgeAccumulator
    {
        public string FromPersonKey { get; init; } = string.Empty;
        public string ToPersonKey { get; init; } = string.Empty;
        public int Weight { get; set; }
        public HashSet<string> Divisions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Frequencies { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Labels { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
