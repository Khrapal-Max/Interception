//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Observations.Services;

public sealed partial class ObservationRegistryService(IDbContextFactory<AppDbContext> dbFactory) : IObservationRegistryService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<ObservationRegistryPageDto> SearchAsync(ObservationRegistryFilter filter, CancellationToken ct)
    {
        await using var db = _dbFactory.CreateDbContext();

        var q = db.Set<Observation>()
            .AsNoTracking()
            .AsQueryable();

        if (filter.DateFrom is not null)
            q = q.Where(x => x.ObservedDate >= filter.DateFrom.Value);

        if (filter.DateTo is not null)
            q = q.Where(x => x.ObservedDate <= filter.DateTo.Value);

        if (filter.DayPart is not null)
            q = q.Where(x => (short)x.DayPart == filter.DayPart.Value);

        var actionNorm = TextNorm.Normalize(filter.Action);
        if (actionNorm is not null)
            q = q.Where(x => x.ActionNorm.Contains(actionNorm));

        var locationNorm = TextNorm.Normalize(filter.Location);
        if (locationNorm is not null)
            q = q.Where(x => x.LocationRaw != null && x.LocationRaw.ToLower().Contains(locationNorm));

        var districtNorm = TextNorm.Normalize(filter.District);
        if (districtNorm is not null)
            q = q.Where(x => x.DistrictRaw != null && x.DistrictRaw.ToLower().Contains(districtNorm));

        var rmNorm = TextNorm.Normalize(filter.Rm);
        if (rmNorm is not null)
            q = q.Where(x => x.RmRaw != null && x.RmRaw.ToLower().Contains(rmNorm));

        var personNorm = TextNorm.Normalize(filter.Person);
        if (personNorm is not null)
        {
            var unknownQuery = personNorm is "нв" or "nv" or "unknown";
            q = q.Where(x => x.Participants.Any(p => unknownQuery ? p.IsUnknown : p.LabelNorm == personNorm));
        }

        q = q.OrderByDescending(x => x.ObservedDate)
             .ThenByDescending(x => x.DayPart)
             .ThenByDescending(x => x.CreatedAtUtc);

        var total = await q.CountAsync(ct);

        var pageEntities = await q
            .Skip(Math.Max(0, filter.Skip))
            .Take(Math.Clamp(filter.Take, 1, 500))
            .Include(x => x.Participants)
            .AsSplitQuery()
            .ToListAsync(ct);

        var participants = pageEntities
            .SelectMany(x => x.Participants)
            .ToList();

        var participantResolutionMap = await LoadResolutionMapAsync(db, participants.Select(x => x.Id).ToList(), ct);

        var items = pageEntities
            .Select(x => new ObservationRegistryItemDto(
                x.Id,
                x.ObservedDate,
                (short)x.DayPart,
                x.Layer,
                x.RmRaw,
                x.PointRaw,
                x.LocationRaw,
                x.DistrictRaw,
                x.ActionRaw,
                x.Note,
                x.Participants.Count,
                [.. x.Participants
                    .OrderBy(p => p.Ordinal)
                    .Select(p => CreateParticipantDto(p, participantResolutionMap))]
            ))
            .ToList();

        return new ObservationRegistryPageDto(total, items);
    }

    public async Task<ObservationDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var db = _dbFactory.CreateDbContext();

        var x = await db.Set<Observation>()
            .AsNoTracking()
            .Include(o => o.Participants)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (x is null)
            return null;

        var participantResolutionMap = await LoadResolutionMapAsync(db, x.Participants.Select(p => p.Id).ToList(), ct);

        return new ObservationDetailsDto(
            x.Id,
            x.ObservedDate,
            (short)x.DayPart,
            x.Layer,
            x.RmRaw,
            x.PointRaw,
            x.LocationRaw,
            x.DistrictRaw,
            x.ActionRaw,
            x.Note,
            x.Source,
            x.SourceFileId,
            x.SourceRow,
            x.ContentHash,
            x.CreatedAtUtc,
            x.CreatedBy,
            [.. x.Participants
                .OrderBy(p => p.Ordinal)
                .Select(p => CreateParticipantDto(p, participantResolutionMap))]
        );
    }

    private static async Task<Dictionary<Guid, ParticipantResolutionReadModel>> LoadResolutionMapAsync(
        AppDbContext db,
        IReadOnlyCollection<Guid> participantIds,
        CancellationToken ct)
    {
        if (participantIds.Count == 0)
            return [];

        var rows = await db.UnknownClusterMembers
            .AsNoTracking()
            .Where(x => participantIds.Contains(x.ObservationParticipantId))
            .Select(x => new ParticipantResolutionReadModel(
                x.ObservationParticipantId,
                x.UnknownClusterId,
                x.UnknownCluster.Code,
                x.UnknownCluster.DisplayName,
                x.UnknownCluster.Role,
                x.UnknownCluster.ArchiveReason,
                x.UnknownCluster.ResolvedActorId,
                x.UnknownCluster.ResolvedActor != null ? x.UnknownCluster.ResolvedActor.DisplayName : null,
                x.UnknownCluster.ResolvedActor != null ? x.UnknownCluster.ResolvedActor.Role : null))
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.ObservationParticipantId)
            .ToDictionary(x => x.Key, x => x.First());
    }

    private static ObservationParticipantDto CreateParticipantDto(
        ObservationParticipant participant,
        IReadOnlyDictionary<Guid, ParticipantResolutionReadModel> resolutionMap)
    {
        if (participant.IsUnknown && resolutionMap.TryGetValue(participant.Id, out var resolution))
        {
            if (resolution.ResolvedActorId is not null)
            {
                var actorDisplay = string.IsNullOrWhiteSpace(resolution.ResolvedActorDisplayName)
                    ? resolution.ClusterDisplayName ?? resolution.ClusterCode
                    : resolution.ResolvedActorDisplayName;

                return new ObservationParticipantDto(
                    participant.Id,
                    participant.LabelRaw,
                    participant.IsUnknown,
                    participant.RoleRaw,
                    participant.Ordinal,
                    $"actor:{resolution.ResolvedActorId}",
                    actorDisplay ?? $"Актор {resolution.ResolvedActorId}",
                    resolution.UnknownClusterId,
                    resolution.ClusterCode,
                    resolution.ClusterRole,
                    resolution.ResolvedActorId,
                    resolution.ResolvedActorDisplayName,
                    resolution.ConfirmedRole);
            }

            var clusterDisplay = resolution.ClusterDisplayName ?? resolution.ClusterCode ?? FallbackUnknownDisplay(participant);
            return new ObservationParticipantDto(
                participant.Id,
                participant.LabelRaw,
                participant.IsUnknown,
                participant.RoleRaw,
                participant.Ordinal,
                $"cluster:{resolution.UnknownClusterId}",
                clusterDisplay,
                resolution.UnknownClusterId,
                resolution.ClusterCode,
                resolution.ClusterRole,
                null,
                null,
                null);
        }

        if (participant.IsUnknown)
        {
            return new ObservationParticipantDto(
                participant.Id,
                participant.LabelRaw,
                participant.IsUnknown,
                participant.RoleRaw,
                participant.Ordinal,
                $"unknown-participant:{participant.Id}",
                FallbackUnknownDisplay(participant),
                null,
                null,
                null,
                null,
                null,
                null);
        }

        return new ObservationParticipantDto(
            participant.Id,
            participant.LabelRaw,
            participant.IsUnknown,
            participant.RoleRaw,
            participant.Ordinal,
            $"known:{participant.LabelNorm}",
            participant.LabelRaw ?? participant.LabelNorm ?? $"known:{participant.Id}",
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static string FallbackUnknownDisplay(ObservationParticipant participant)
        => string.IsNullOrWhiteSpace(participant.LabelRaw) ? $"НВ {participant.Ordinal}" : participant.LabelRaw!;
}