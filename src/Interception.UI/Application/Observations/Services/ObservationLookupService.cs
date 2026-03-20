//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Observations.Services;

/// <summary>
/// EF-backed suggestions for observation create/edit flows.
/// </summary>
public sealed class ObservationLookupService(IDbContextFactory<AppDbContext> dbFactory) : IObservationLookupService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<IReadOnlyList<LayerRmSuggestionDto>> GetLayerSuggestionsAsync(string query, int take, CancellationToken ct)
    {
        var rawQuery = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawQuery))
            return [];

        var takeClamped = Math.Clamp(take, 1, 50);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rows = await db.Observations
            .AsNoTracking()
            .Where(x => x.Layer != null && EF.Functions.ILike(x.Layer, $"%{rawQuery}%"))
            .GroupBy(x => new { x.Layer, x.RmRaw })
            .Select(x => new
            {
                Layer = x.Key.Layer!,
                x.Key.RmRaw,
                SeenCount = x.Count()
            })
            .OrderByDescending(x => x.SeenCount)
            .ThenBy(x => x.Layer)
            .ThenBy(x => x.RmRaw)
            .Take(takeClamped)
            .ToListAsync(ct);

        return [.. rows.Select(x => new LayerRmSuggestionDto(x.Layer, x.RmRaw, x.SeenCount))];
    }

    public async Task<IReadOnlyList<string>> GetDistrictSuggestionsAsync(string query, int take, CancellationToken ct)
    {
        var rawQuery = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawQuery))
            return [];

        var takeClamped = Math.Clamp(take, 1, 50);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.Observations
            .AsNoTracking()
            .Where(x => x.DistrictRaw != null && EF.Functions.ILike(x.DistrictRaw, $"%{rawQuery}%"))
            .GroupBy(x => x.DistrictRaw!)
            .Select(x => new
            {
                Value = x.Key,
                SeenCount = x.Count()
            })
            .OrderByDescending(x => x.SeenCount)
            .ThenBy(x => x.Value)
            .Take(takeClamped)
            .Select(x => x.Value)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ParticipantSuggestionDto>> SearchParticipantSuggestionsAsync(string query, int take, CancellationToken ct)
    {
        var rawQuery = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawQuery))
            return [];

        var takeClamped = Math.Clamp(take, 1, 30);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var groupedRows = await db.ObservationParticipants
            .AsNoTracking()
            .Where(x => !x.IsUnknown && x.LabelRaw != null && EF.Functions.ILike(x.LabelRaw, $"%{rawQuery}%"))
            .GroupBy(x => new { x.LabelNorm, x.LabelRaw })
            .Select(x => new
            {
                LabelNorm = x.Key.LabelNorm!,
                LabelRaw = x.Key.LabelRaw!,
                SeenCount = x.Count(),
                LastSeenAtUtc = x.Max(p => (DateTime?)p.Observation.ObservedDate)
            })
            .OrderByDescending(x => x.SeenCount)
            .ThenByDescending(x => x.LastSeenAtUtc)
            .ThenBy(x => x.LabelRaw)
            .Take(takeClamped)
            .ToListAsync(ct);

        var labelNorms = groupedRows.Select(x => x.LabelNorm).Distinct().ToList();

        var roleRows = labelNorms.Count == 0
            ? []
            : await db.ObservationParticipants
                .AsNoTracking()
                .Where(x => !x.IsUnknown && x.LabelNorm != null && labelNorms.Contains(x.LabelNorm) && x.RoleRaw != null)
                .Select(x => new { x.LabelNorm, x.RoleRaw })
                .ToListAsync(ct);

        var roleLookup = roleRows
            .GroupBy(x => x.LabelNorm!)
            .ToDictionary(
                x => x.Key,
                x => x.GroupBy(r => r.RoleRaw!)
                    .OrderByDescending(r => r.Count())
                    .ThenBy(r => r.Key)
                    .Select(r => r.Key)
                    .FirstOrDefault());

        return [.. groupedRows
            .Select(x => new ParticipantSuggestionDto(
                x.LabelRaw,
                x.LabelNorm,
                roleLookup.GetValueOrDefault(x.LabelNorm),
                x.SeenCount,
                x.LastSeenAtUtc))];
    }

    public async Task<IReadOnlyList<ActionTextSuggestionDto>> SearchActionTextSuggestionsAsync(string query, int take, CancellationToken ct)
    {
        var rawQuery = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawQuery))
            return [];

        var takeClamped = Math.Clamp(take, 1, 30);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rows = await db.Observations
            .AsNoTracking()
            .Where(x => EF.Functions.ILike(x.ActionRaw, $"%{rawQuery}%"))
            .GroupBy(x => new { x.ActionNorm, x.ActionRaw, x.ObservationActionId })
            .Select(x => new
            {
                x.Key.ActionNorm,
                x.Key.ActionRaw,
                x.Key.ObservationActionId,
                BoundActionName = x.Max(o => o.ObservationAction != null ? o.ObservationAction.Name : null),
                SeenCount = x.Count(),
                LastSeenAtUtc = x.Max(o => (DateTime?)o.ObservedDate)
            })
            .OrderByDescending(x => x.SeenCount)
            .ThenByDescending(x => x.LastSeenAtUtc)
            .ThenBy(x => x.ActionRaw)
            .Take(takeClamped)
            .ToListAsync(ct);

        return [.. rows
            .Select(x => new ActionTextSuggestionDto(
                x.ActionRaw,
                x.ActionNorm,
                x.SeenCount,
                x.ObservationActionId,
                x.BoundActionName,
                x.LastSeenAtUtc))];
    }

    public async Task<IReadOnlyList<ActionCatalogSuggestionDto>> SearchActionCatalogSuggestionsAsync(string query, int take, CancellationToken ct)
    {
        var rawQuery = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawQuery))
            return [];

        var takeClamped = Math.Clamp(take, 1, 30);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.ObservationActions
            .AsNoTracking()
            .Where(x => x.IsActive && EF.Functions.ILike(x.Name, $"%{rawQuery}%"))
            .OrderBy(x => x.Name)
            .Take(takeClamped)
            .Select(x => new ActionCatalogSuggestionDto(
                x.Id,
                x.Name,
                x.Category,
                x.InitiatorRoleName,
                x.ResponderRoleName,
                x.TypicalParticipantsCount,
                x.RequiresCounterparty))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SubdivisionSuggestionDto>> SearchSubdivisionSuggestionsAsync(string query, int take, CancellationToken ct)
    {
        var rawQuery = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawQuery))
            return [];

        var takeClamped = Math.Clamp(take, 1, 30);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rows = await db.Observations
            .AsNoTracking()
            .Where(x => x.SubdivisionRaw != null && EF.Functions.ILike(x.SubdivisionRaw, $"%{rawQuery}%"))
            .GroupBy(x => new { x.SubdivisionRaw, x.SubdivisionNorm, x.SubdivisionStrength })
            .Select(x => new
            {
                RawValue = x.Key.SubdivisionRaw!,
                NormValue = x.Key.SubdivisionNorm!,
                Strength = x.Key.SubdivisionStrength,
                SeenCount = x.Count(),
                LastSeenAtUtc = x.Max(o => (DateTime?)o.ObservedDate)
            })
            .OrderByDescending(x => x.SeenCount)
            .ThenByDescending(x => x.LastSeenAtUtc)
            .ThenBy(x => x.RawValue)
            .Take(takeClamped)
            .ToListAsync(ct);

        return [.. rows
            .Select(x => new SubdivisionSuggestionDto(
                x.RawValue,
                x.NormValue,
                x.Strength,
                x.SeenCount,
                x.LastSeenAtUtc))];
    }
}
