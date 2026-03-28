//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Candidates;
using Interception.UI.Application.Interceptions.Models.PatternRecognition;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services.Candidates;

/// <summary>
/// Будує аналітичну карту зв'язків між особами.
/// </summary>
public sealed class LinkMapService(IDbContextFactory<AppDbContext> dbFactory) : ILinkMapService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<LinkMapModel> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        _ = db;
        _ = dateFrom;
        _ = dateTo;

        return new LinkMapModel([], []);
    }
}
