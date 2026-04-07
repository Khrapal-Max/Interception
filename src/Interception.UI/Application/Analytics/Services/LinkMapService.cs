//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Читає останній завершений snapshot карти зв'язків і мапить його в DTO для UI.
/// </summary>
public sealed class LinkMapService(IDbContextFactory<AppDbContext> dbFactory) : ILinkMapService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<LinkMapDto> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var (normalizedFromUtc, normalizedToUtc) = TopologySnapshotPeriodNormalizer.Normalize(dateFrom, dateTo);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var run = await db.TopologySnapshotRuns
            .AsNoTracking()
            .Where(x => x.DateFromUtc == normalizedFromUtc
                        && x.DateToUtc == normalizedToUtc
                        && x.Status == TopologySnapshotRunStatus.Completed)
            .OrderByDescending(x => x.CompletedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id })
            .FirstOrDefaultAsync(ct);

        if (run is null)
            return new LinkMapDto([]);

        var groups = await db.TopologySnapshotGroups
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x => x.RunId == run.Id)
            .Include(x => x.Frequencies)
            .Include(x => x.Members)
            .Include(x => x.Actions)
            .Include(x => x.Bridges)
                .ThenInclude(x => x.Actions)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        var models = groups
            .Select(group => new LinkMapGroupDto(
                group.GroupKey,
                group.Division,
                [.. group.Frequencies.OrderBy(x => x.SortOrder).Select(x => x.Frequency)],
                group.KeyPersonName,
                group.KeyPersonRole,
                [.. group.Members.OrderBy(x => x.SortOrder).Select(x => x.Name)],
                [.. group.Members.OrderBy(x => x.SortOrder).Select(x => new LinkMapMemberDto(
                    x.Name,
                    x.Role,
                    x.MentionCount,
                    x.UniquePartnerCount,
                    x.ConnectionWeight,
                    x.LastSeenAt,
                    x.GroupCount,
                    x.IsSharedAcrossGroups,
                    x.IsKeyPerson))],
                group.MentionCount,
                group.InternalConnectionWeight,
                group.BridgeWeight,
                group.Actions.OrderBy(x => x.SortOrder).FirstOrDefault(x => x.IsPrimary)?.Name,
                [.. group.Actions.OrderBy(x => x.SortOrder).Select(x => x.Name)],
                [.. group.Bridges.OrderBy(x => x.SortOrder).Select(bridge => new LinkMapBridgeDto(
                    bridge.TargetGroupKey,
                    bridge.TargetDivision,
                    bridge.ContactPersonName,
                    bridge.BridgeFrequency,
                    bridge.Weight,
                    bridge.Actions.OrderBy(x => x.SortOrder).FirstOrDefault(x => x.IsPrimary)?.Name ?? bridge.PrimaryAction,
                    [.. bridge.Actions.OrderBy(x => x.SortOrder).Select(x => x.Name)]))]))
            .ToList();

        return new LinkMapDto(models);
    }
}
