//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Observations.Services;

/// <summary>
/// EF Core-backed lookup service for observation field suggestions.
/// </summary>
public sealed class ObservationLookupService(IDbContextFactory<AppDbContext> dbFactory) : IObservationLookupService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<IReadOnlyList<LayerRmSuggestionDto>> GetLayerSuggestionsAsync(string query, int take, CancellationToken ct)
    {
        var q = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(q))
            return [];

        var takeClamped = Math.Clamp(take, 1, 50);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var raw = await db.Observations
            .AsNoTracking()
            .Where(o => o.Layer != null && EF.Functions.ILike(o.Layer, $"%{q}%"))
            .GroupBy(o => new { o.Layer, o.RmRaw })
            .Select(g => new
            {
                Layer = g.Key.Layer!,
                g.Key.RmRaw,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Layer)
            .ThenBy(x => x.RmRaw)
            .Take(takeClamped)
            .ToListAsync(ct);

        return [.. raw.Select(x => new LayerRmSuggestionDto(x.Layer, x.RmRaw, x.Count))];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetDistrictSuggestionsAsync(string query, int take, CancellationToken ct)
    {
        var q = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(q))
            return [];

        var takeClamped = Math.Clamp(take, 1, 50);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var raw = await db.Observations
            .AsNoTracking()
            .Where(o => o.DistrictRaw != null && EF.Functions.ILike(o.DistrictRaw, $"%{q}%"))
            .GroupBy(o => o.DistrictRaw)
            .Select(g => new
            {
                Value = g.Key!,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Value)
            .Take(takeClamped)
            .ToListAsync(ct);

        return [.. raw.Select(x => x.Value)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ParticipantSuggestionDto>> SearchParticipantSuggestionsAsync(string query, int take, CancellationToken ct)
    {
        var q = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(q))
            return [];

        var takeClamped = Math.Clamp(take, 1, 20);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rows = await db.ObservationParticipants
            .AsNoTracking()
            .Where(p => !p.IsUnknown
                        && p.LabelNorm != null
                        && (EF.Functions.ILike(p.LabelRaw!, $"%{q}%") || EF.Functions.ILike(p.LabelNorm!, $"%{q}%")))
            .Select(p => new
            {
                p.LabelRaw,
                p.LabelNorm,
                p.RoleRaw,
                p.Observation.ObservedDate
            })
            .ToListAsync(ct);

        var items = rows
            .GroupBy(x => x.LabelNorm!)
            .Select(g =>
            {
                var display = g
                    .Where(x => !string.IsNullOrWhiteSpace(x.LabelRaw))
                    .GroupBy(x => x.LabelRaw!.Trim())
                    .OrderByDescending(x => x.Count())
                    .ThenBy(x => x.Key)
                    .Select(x => x.Key)
                    .FirstOrDefault() ?? g.Key;

                var primaryRole = g
                    .Where(x => !string.IsNullOrWhiteSpace(x.RoleRaw))
                    .GroupBy(x => x.RoleRaw!.Trim())
                    .OrderByDescending(x => x.Count())
                    .ThenBy(x => x.Key)
                    .Select(x => x.Key)
                    .FirstOrDefault();

                var lastSeen = g.Max(x => x.ObservedDate);
                var observationsCount = g.Count();
                var subtitle = primaryRole is null
                    ? $"{observationsCount} спостережень"
                    : $"{primaryRole} · {observationsCount} спостережень";

                return new ParticipantSuggestionDto(
                    Key: $"known:{g.Key}",
                    Display: display,
                    Kind: "known",
                    Subtitle: subtitle,
                    ObservationsCount: observationsCount,
                    LastSeenDate: lastSeen,
                    PrimaryRole: primaryRole);
            })
            .OrderByDescending(x => x.ObservationsCount)
            .ThenByDescending(x => x.LastSeenDate)
            .ThenBy(x => x.Display)
            .Take(takeClamped)
            .ToList();

        return items;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActionContextSuggestionDto>> GetActionContextAsync(
        string? actionRaw,
        IReadOnlyCollection<string> participantKeys,
        int take,
        CancellationToken ct)
    {
        var normalizedAction = (actionRaw ?? string.Empty).Trim();
        var normalizedKeys = participantKeys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (string.IsNullOrWhiteSpace(normalizedAction) || normalizedKeys.Length == 0)
            return [];

        var knownKeys = normalizedKeys
            .Where(x => x.StartsWith("known:", StringComparison.OrdinalIgnoreCase))
            .Select(x => x[6..])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (knownKeys.Length == 0)
            return [];

        var takeClamped = Math.Clamp(take, 1, 12);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var observationRows = await db.Observations
            .AsNoTracking()
            .Where(o => EF.Functions.ILike(o.ActionRaw, $"%{normalizedAction}%"))
            .Select(o => new
            {
                o.Id,
                o.ActionRaw,
                o.DistrictRaw,
                o.ObservedDate
            })
            .ToListAsync(ct);

        if (observationRows.Count == 0)
            return [];

        var observationIds = observationRows.Select(x => x.Id).ToArray();
        var participantRows = await db.ObservationParticipants
            .AsNoTracking()
            .Where(p => observationIds.Contains(p.ObservationId))
            .Select(p => new
            {
                p.ObservationId,
                p.LabelRaw,
                p.LabelNorm,
                p.RoleRaw,
                p.IsUnknown
            })
            .ToListAsync(ct);

        var observationMap = observationRows.ToDictionary(x => x.Id);
        var targetObservationIds = participantRows
            .Where(p => p.LabelNorm != null && knownKeys.Contains(p.LabelNorm, StringComparer.OrdinalIgnoreCase))
            .Select(p => p.ObservationId)
            .Distinct()
            .ToArray();

        if (targetObservationIds.Length == 0)
            return [];

        var items = participantRows
            .Where(p => targetObservationIds.Contains(p.ObservationId))
            .Where(p => p.LabelNorm != null && !knownKeys.Contains(p.LabelNorm, StringComparer.OrdinalIgnoreCase))
            .GroupBy(p => p.LabelNorm!)
            .Select(g =>
            {
                var display = g.Where(x => !string.IsNullOrWhiteSpace(x.LabelRaw))
                    .GroupBy(x => x.LabelRaw!.Trim())
                    .OrderByDescending(x => x.Count())
                    .ThenBy(x => x.Key)
                    .Select(x => x.Key)
                    .FirstOrDefault() ?? g.Key;

                var lastObservation = g
                    .Select(x => observationMap[x.ObservationId])
                    .OrderByDescending(x => x.ObservedDate)
                    .First();

                var role = g.Where(x => !string.IsNullOrWhiteSpace(x.RoleRaw))
                    .GroupBy(x => x.RoleRaw!.Trim())
                    .OrderByDescending(x => x.Count())
                    .ThenBy(x => x.Key)
                    .Select(x => x.Key)
                    .FirstOrDefault();

                var relationType = string.Equals(lastObservation.ActionRaw, normalizedAction, StringComparison.OrdinalIgnoreCase)
                    ? "same-action"
                    : "direct";

                var description = role is null
                    ? $"Спільна дія: {lastObservation.ActionRaw}"
                    : $"{role} · спільна дія: {lastObservation.ActionRaw}";

                return new ActionContextSuggestionDto(
                    Display: display,
                    RelationType: relationType,
                    Description: description,
                    Weight: g.Count(),
                    LastSeenDate: lastObservation.ObservedDate);
            })
            .OrderByDescending(x => x.Weight)
            .ThenByDescending(x => x.LastSeenDate)
            .ThenBy(x => x.Display)
            .Take(takeClamped)
            .ToList();

        return items;
    }
}
