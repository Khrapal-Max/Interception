//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Extensions;

internal static class TopologySnapshotStateMarker
{
    public static async Task MarkAllCompletedSnapshotsAsStaleAsync(
        AppDbContext db,
        CancellationToken ct = default)
    {
        var runs = await db.TopologySnapshotRuns
            .Where(x => x.Status == TopologySnapshotRunStatus.Completed && !x.IsStale)
            .ToListAsync(ct);

        if (runs.Count == 0)
            return;

        foreach (var run in runs)
            run.MarkStale();
    }
}
