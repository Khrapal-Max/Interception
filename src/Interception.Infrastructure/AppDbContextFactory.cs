//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Interception.Infrastructure;

public sealed class AppDbContextFactory(IDbContextFactory<AppDbContext> factory) : IAppDbContextFactory
{
    public async Task<IAppDbContext> CreateDbContextAsync(CancellationToken ct = default)
        => await factory.CreateDbContextAsync(ct);
}
