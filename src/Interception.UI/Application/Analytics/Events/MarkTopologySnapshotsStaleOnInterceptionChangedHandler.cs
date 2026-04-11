//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Common.Events;
using Interception.UI.Application.Interceptions.Events;
using Interception.UI.Domain.Enums;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Events;

/// <summary>
/// Реакція analytics-контексту на зміну перехоплень:
/// completed snapshot-runs позначаються як stale.
/// </summary>
public sealed class MarkTopologySnapshotsStaleOnInterceptionChangedHandler(
    IDbContextFactory<AppDbContext> dbFactory)
    : IIntegrationEventHandler<InterceptionChangedIntegrationEvent>
{
    public async Task HandleAsync(InterceptionChangedIntegrationEvent integrationEvent, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var completedRuns = await db.TopologySnapshotRuns
            .Where(x => x.Status == TopologySnapshotRunStatus.Completed && !x.IsStale)
            .ToListAsync(ct);

        if (completedRuns.Count == 0)
            return;

        foreach (var run in completedRuns)
            run.MarkStale();

        await db.SaveChangesAsync(ct);
    }
}
