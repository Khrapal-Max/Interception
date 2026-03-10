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
/// Сервіс реєстру аналітичних кластерів невизначених осіб.
/// </summary>
public sealed class AnalyticsUnknownClusterRegistryService(IDbContextFactory<AppDbContext> dbFactory) : IAnalyticsUnknownClusterRegistryService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<AnalyticsUnknownClusterRegistryPageDto> SearchAsync(AnalyticsUnknownClusterRegistryFilter filter, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var take = Math.Clamp(filter.Take, 1, 100);
        var skip = Math.Max(0, filter.Skip);
        var query = (filter.Query ?? string.Empty).Trim();
        var status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim().ToLowerInvariant();

        var clusters = db.UnknownClusters
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            clusters = clusters.Where(x =>
                EF.Functions.ILike(x.Code, $"%{query}%") ||
                (x.DisplayName != null && EF.Functions.ILike(x.DisplayName, $"%{query}%")) ||
                (x.ResolvedActor != null && EF.Functions.ILike(x.ResolvedActor.DisplayName, $"%{query}%")));
        }

        if (!string.IsNullOrWhiteSpace(status))
            clusters = clusters.Where(x => x.Status == status);

        var total = await clusters.CountAsync(ct);

        var itemsRaw = await clusters
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.Members
                .Select(m => (DateOnly?)m.ObservationParticipant.Observation.ObservedDate)
                .OrderByDescending(d => d)
                .FirstOrDefault())
            .ThenBy(x => x.Code)
            .Skip(skip)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.DisplayName,
                x.Status,
                ParticipantsCount = x.Members.Count,
                LastSeenDate = x.Members
                    .Select(m => (DateOnly?)m.ObservationParticipant.Observation.ObservedDate)
                    .OrderByDescending(d => d)
                    .FirstOrDefault(),
                LinkedActorDisplayName = x.ResolvedActor != null ? x.ResolvedActor.DisplayName : null
            })
            .ToListAsync(ct);

        var items = itemsRaw
            .Select(x => new AnalyticsUnknownClusterRegistryItemDto(
                x.Id,
                x.Code,
                x.DisplayName,
                x.Status,
                x.ParticipantsCount,
                x.LastSeenDate,
                x.LinkedActorDisplayName))
            .ToList();

        return new AnalyticsUnknownClusterRegistryPageDto(items, total);
    }
}
