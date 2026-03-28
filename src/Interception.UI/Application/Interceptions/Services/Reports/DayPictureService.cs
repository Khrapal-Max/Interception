//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Reports;
using Interception.UI.Application.Interceptions.Models.Reports;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services.Reports;

/// <summary>
/// Будує денну картину пов'язаних спостережень.
/// </summary>
public sealed class DayPictureService(IDbContextFactory<AppDbContext> dbFactory)
    : IDayPictureService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<DayPictureModel> BuildAsync(DateOnly day, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        _ = db;
        _ = day;

        return new DayPictureModel(day, 0, []);
    }
}
