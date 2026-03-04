//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Observations;

public sealed class ObservationRegistryService(DbContext db) : IObservationRegistryService
{
    private readonly DbContext _db = db;

    public async Task<ObservationRegistryPageDto> SearchAsync(ObservationRegistryFilter filter, CancellationToken ct)
    {
        var q = _db.Set<Observation>()
            .AsNoTracking()
            .Include(x => x.Participants)
            .AsQueryable();

        if (filter.DateFrom is not null)
            q = q.Where(x => x.ObservedDate >= filter.DateFrom.Value);

        if (filter.DateTo is not null)
            q = q.Where(x => x.ObservedDate <= filter.DateTo.Value);

        if (filter.DayPart is not null)
            q = q.Where(x => (short)x.DayPart == filter.DayPart.Value);

        var actionNorm = TextNorm.Normalize(filter.Action);
        if (actionNorm is not null)
        {
            q = q.Where(x =>
                x.ActionNorm.Contains(actionNorm) ||
                (x.ActionRaw != null && x.ActionRaw.Contains(actionNorm, StringComparison.CurrentCultureIgnoreCase)));
        }

        var locationNorm = TextNorm.Normalize(filter.Location);
        if (locationNorm is not null)
            q = q.Where(x => x.LocationRaw != null && x.LocationRaw.Contains(locationNorm, StringComparison.CurrentCultureIgnoreCase));

        var districtNorm = TextNorm.Normalize(filter.District);
        if (districtNorm is not null)
            q = q.Where(x => x.DistrictRaw != null && x.DistrictRaw.Contains(districtNorm, StringComparison.CurrentCultureIgnoreCase));

        var companyNorm = TextNorm.Normalize(filter.Company);
        if (companyNorm is not null)
            q = q.Where(x => x.CompanyRaw != null && x.CompanyRaw.Contains(companyNorm, StringComparison.CurrentCultureIgnoreCase));

        var rmNorm = TextNorm.Normalize(filter.Rm);
        if (rmNorm is not null)
            q = q.Where(x => x.RmRaw != null && x.RmRaw.Contains(rmNorm, StringComparison.CurrentCultureIgnoreCase));

        var personNorm = TextNorm.Normalize(filter.Person);
        if (personNorm is not null)
        {
            var unknownQuery = personNorm is "нв" or "nv" or "unknown";

            q = q.Where(x => x.Participants.Any(p =>
                unknownQuery ? p.IsUnknown : p.LabelNorm == personNorm));
        }

        // newest first
        q = q.OrderByDescending(x => x.ObservedDate)
             .ThenByDescending(x => x.DayPart)
             .ThenByDescending(x => x.CreatedAtUtc);

        var total = await q.CountAsync(ct);

        var items = await q.Skip(Math.Max(0, filter.Skip))
            .Take(Math.Clamp(filter.Take, 1, 500))
            .Select(x => new ObservationRegistryItemDto(
                x.Id,
                x.ObservedDate,
                (short)x.DayPart,
                x.ActionRaw,
                x.LocationRaw,
                x.DistrictRaw,
                x.CompanyRaw,
                x.RmRaw,
                x.Layer,
                x.Participants.Count,
                x.Participants
                    .OrderBy(p => p.Ordinal)
                    .Select(p => new ObservationParticipantDto(p.LabelRaw, p.IsUnknown, p.RoleRaw, p.Ordinal))
                    .ToList()
            ))
            .ToListAsync(ct);

        return new ObservationRegistryPageDto(total, items);
    }

    public async Task<ObservationDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var x = await _db.Set<Observation>()
            .AsNoTracking()
            .Include(o => o.Participants)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (x is null) return null;

        return new ObservationDetailsDto(
            x.Id,
            x.ObservedDate,
            (short)x.DayPart,
            x.Layer,
            x.RmRaw,
            x.PointRaw,
            x.LocationRaw,
            x.DistrictRaw,
            x.CompanyRaw,
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
                .Select(p => new ObservationParticipantDto(p.LabelRaw, p.IsUnknown, p.RoleRaw, p.Ordinal))]
        );
    }
}
