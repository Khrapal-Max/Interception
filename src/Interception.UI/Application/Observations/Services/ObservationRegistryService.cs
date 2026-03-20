//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Observations.Services;

/// <summary>
/// EF-backed observation registry and details read service.
/// </summary>
public sealed class ObservationRegistryService(IDbContextFactory<AppDbContext> dbFactory) : IObservationRegistryService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<ObservationRegistryPageDto> SearchAsync(ObservationRegistryFilterDto filter, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.Observations.AsNoTracking();
        query = ApplyFilter(query, filter);

        var totalCount = await query.CountAsync(ct);

        var take = Math.Clamp(filter.Take, 1, 200);
        var skip = Math.Max(0, filter.Skip);

        var rows = await query
            .OrderByDescending(x => x.ObservedDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.ObservedDate,
                x.ActionRaw,
                x.ObservationActionId,
                ObservationActionName = x.ObservationAction != null ? x.ObservationAction.Name : null,
                x.Layer,
                x.RmRaw,
                x.LocationRaw,
                x.DistrictRaw,
                x.SubdivisionRaw,
                HasUnknownParticipants = x.Participants.Any(p => p.IsUnknown),
                ParticipantsCount = x.Participants.Count,
                TagsCount = x.Tags.Count,
                ProbableActionsCount = x.ProbableActions.Count
            })
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        var observationIds = rows.Select(x => x.Id).ToList();

        var participantPreviewRows = observationIds.Count == 0
            ? []
            : await db.ObservationParticipants
                .AsNoTracking()
                .Where(x => observationIds.Contains(x.ObservationId))
                .OrderBy(x => x.ObservationId)
                .ThenBy(x => x.Ordinal)
                .Select(x => new
                {
                    x.ObservationId,
                    x.Ordinal,
                    x.LabelRaw,
                    x.IsUnknown
                })
                .ToListAsync(ct);

        var previewLookup = participantPreviewRows
            .GroupBy(x => x.ObservationId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<string>)[.. x
                    .Take(3)
                    .Select(p => string.IsNullOrWhiteSpace(p.LabelRaw)
                        ? $"НВ {p.Ordinal}"
                        : p.LabelRaw!)]);

        var items = rows
            .Select(x => new ObservationRegistryItemDto(
                x.Id,
                x.ObservedDate,
                x.ActionRaw,
                x.ObservationActionId,
                x.ObservationActionName,
                x.Layer,
                x.RmRaw,
                x.LocationRaw,
                x.DistrictRaw,
                x.SubdivisionRaw,
                x.HasUnknownParticipants,
                x.ParticipantsCount,
                x.TagsCount,
                x.ProbableActionsCount,
                previewLookup.GetValueOrDefault(x.Id) ?? []))
            .ToList();

        return new ObservationRegistryPageDto(items, totalCount);
    }

    public async Task<ObservationDetailsDto?> GetByIdAsync(Guid observationId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var observation = await db.Observations
            .AsNoTracking()
            .Where(x => x.Id == observationId)
            .Select(x => new
            {
                x.Id,
                x.ObservedDate,
                x.ObservationActionId,
                ObservationActionName = x.ObservationAction != null ? x.ObservationAction.Name : null,
                x.ActionRaw,
                x.ActionNorm,
                x.Layer,
                x.RmRaw,
                x.PointRaw,
                x.LocationRaw,
                x.DistrictRaw,
                x.SubdivisionRaw,
                x.SubdivisionStrength,
                x.SubdivisionSource,
                x.Note,
                x.Source,
                x.SourceFileId,
                x.SourceRow,
                x.ContentHash,
                x.CreatedAtUtc,
                x.CreatedBy
            })
            .FirstOrDefaultAsync(ct);

        if (observation is null)
            return null;

        var participants = await db.ObservationParticipants
            .AsNoTracking()
            .Where(x => x.ObservationId == observationId)
            .OrderBy(x => x.Ordinal)
            .Select(x => new ObservationParticipantDto(
                x.Id,
                x.LabelRaw,
                x.IsUnknown,
                x.StartedAsUnknown,
                x.RoleRaw,
                x.Ordinal))
            .ToListAsync(ct);

        var tags = await db.ObservationTags
            .AsNoTracking()
            .Where(x => x.ObservationId == observationId)
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.RawValue)
            .Select(x => new ObservationTagDto(
                x.Id,
                x.RawValue,
                x.Kind,
                x.Source,
                x.TagCatalogId,
                x.TagCatalog != null ? x.TagCatalog.Name : null))
            .ToListAsync(ct);

        var probableActions = await db.ObservationProbableActions
            .AsNoTracking()
            .Where(x => x.ObservationId == observationId)
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.ObservationAction.Name)
            .Select(x => new ObservationProbableActionDto(
                x.Id,
                x.ObservationActionId,
                x.ObservationAction.Name,
                x.Confidence,
                x.Reason,
                x.Source,
                x.CreatedAtUtc,
                x.CreatedBy))
            .ToListAsync(ct);

        return new ObservationDetailsDto(
            observation.Id,
            observation.ObservedDate,
            observation.ObservationActionId,
            observation.ObservationActionName,
            observation.ActionRaw,
            observation.ActionNorm,
            observation.Layer,
            observation.RmRaw,
            observation.PointRaw,
            observation.LocationRaw,
            observation.DistrictRaw,
            observation.SubdivisionRaw,
            observation.SubdivisionStrength,
            observation.SubdivisionSource,
            observation.Note,
            observation.Source,
            observation.SourceFileId,
            observation.SourceRow,
            observation.ContentHash,
            observation.CreatedAtUtc,
            observation.CreatedBy,
            participants,
            tags,
            probableActions);
    }

    private static IQueryable<Observation> ApplyFilter(
        IQueryable<Observation> query,
        ObservationRegistryFilterDto filter)
    {
        if (filter.ObservedFrom is not null)
            query = query.Where(x => x.ObservedDate >= filter.ObservedFrom.Value);

        if (filter.ObservedTo is not null)
            query = query.Where(x => x.ObservedDate <= filter.ObservedTo.Value);

        if (filter.ObservationActionId is not null)
            query = query.Where(x => x.ObservationActionId == filter.ObservationActionId.Value);

        if (filter.TagKind is not null)
            query = query.Where(x => x.Tags.Any(t => t.Kind == filter.TagKind.Value));

        if (filter.SubdivisionStrength is not null)
            query = query.Where(x => x.SubdivisionStrength == filter.SubdivisionStrength.Value);

        if (filter.OnlyUnknownParticipants)
            query = query.Where(x => x.Participants.Any(p => p.IsUnknown));

        if (filter.OnlyBoundAction)
            query = query.Where(x => x.ObservationActionId != null);

        var rawQuery = (filter.Query ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(rawQuery))
        {
            var pattern = $"%{rawQuery}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.ActionRaw, pattern) ||
                (x.Layer != null && EF.Functions.ILike(x.Layer, pattern)) ||
                (x.RmRaw != null && EF.Functions.ILike(x.RmRaw, pattern)) ||
                (x.PointRaw != null && EF.Functions.ILike(x.PointRaw, pattern)) ||
                (x.LocationRaw != null && EF.Functions.ILike(x.LocationRaw, pattern)) ||
                (x.DistrictRaw != null && EF.Functions.ILike(x.DistrictRaw, pattern)) ||
                (x.SubdivisionRaw != null && EF.Functions.ILike(x.SubdivisionRaw, pattern)) ||
                (x.Note != null && EF.Functions.ILike(x.Note, pattern)) ||
                x.Participants.Any(p =>
                    (p.LabelRaw != null && EF.Functions.ILike(p.LabelRaw, pattern)) ||
                    (p.RoleRaw != null && EF.Functions.ILike(p.RoleRaw, pattern))) ||
                x.Tags.Any(t => EF.Functions.ILike(t.RawValue, pattern)) ||
                (x.ObservationAction != null && EF.Functions.ILike(x.ObservationAction.Name, pattern)));
        }

        return query;
    }
}
