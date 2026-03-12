//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Analytics.Models;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Реєстр аналітичних припущень щодо невизначених осіб.
/// </summary>
public sealed class AnalyticsUnknownClusterRegistryService(IDbContextFactory<AppDbContext> dbFactory) : IAnalyticsUnknownClusterRegistryService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<AnalyticsUnknownClusterRegistryPageDto> SearchAsync(AnalyticsUnknownClusterRegistryFilter filter, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var q = db.UnknownClusters
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var text = filter.Query.Trim();
            q = q.Where(x =>
                EF.Functions.ILike(x.Code, $"%{text}%") ||
                (x.DisplayName != null && EF.Functions.ILike(x.DisplayName, $"%{text}%")) ||
                (x.Role != null && EF.Functions.ILike(x.Role, $"%{text}%")) ||
                (x.ResolvedActor != null && EF.Functions.ILike(x.ResolvedActor.DisplayName, $"%{text}%")));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim();
            q = q.Where(x => x.Status == status);
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Skip(Math.Max(0, filter.Skip))
            .Take(Math.Clamp(filter.Take, 1, 200))
            .Select(x => new AnalyticsUnknownClusterRegistryItemDto(
                x.Id,
                x.Code,
                x.DisplayName,
                x.Role,
                x.Status,
                x.ArchiveReason,
                x.Members.Count,
                x.Members
                    .Select(m => (DateOnly?)m.ObservationParticipant.Observation.ObservedDate)
                    .Max(),
                x.ResolvedActor != null ? x.ResolvedActor.DisplayName : null,
                x.ResolvedActor != null ? x.ResolvedActor.Role : null))
            .ToListAsync(ct);

        return new AnalyticsUnknownClusterRegistryPageDto(total, items);
    }
}