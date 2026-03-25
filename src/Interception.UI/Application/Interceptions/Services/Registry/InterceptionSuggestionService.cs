//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Registry;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services.Registry;

/// <summary>
/// Реалізація контекстних підказок для операторського вводу перехоплень.
/// </summary>
public sealed class InterceptionSuggestionService(IDbContextFactory<AppDbContext> dbFactory) : IInterceptionSuggestionService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FrequencySuggestionDto>> GetFrequencyWithDivisionAsync(
        string? query = null,
        int take = 10,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var q = db.InterceptionMessages.Where(m => m.Frequency != null);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(m => m.Frequency!.StartsWith(query));

        var triples = await q
            .GroupBy(m => new { m.Frequency, m.Division, m.VectorSignal })
            .Select(g => new
            {
                g.Key.Frequency,
                g.Key.Division,
                g.Key.VectorSignal,
                Count = g.Count()
            })
            .ToListAsync(ct);

        return [.. triples
            .GroupBy(p => p.Frequency!)
            .Select(g => new FrequencySuggestionDto
            {
                Frequency = g.Key,
                Division = g.Where(p => p.Division != null)
                    .OrderByDescending(p => p.Count)
                    .FirstOrDefault()?.Division,
                VectorSignal = g.Where(p => p.VectorSignal != null)
                    .OrderByDescending(p => p.Count)
                    .FirstOrDefault()?.VectorSignal,
                Count = g.Sum(p => p.Count)
            })
            .OrderByDescending(s => s.Count)
            .Take(take)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetVectorSignalSuggestionsAsync(
        string? query = null,
        string? frequency = null,
        int take = 10,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var q = db.InterceptionMessages.Where(m => m.VectorSignal != null);

        if (!string.IsNullOrWhiteSpace(frequency))
            q = q.Where(m => m.Frequency == frequency);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(m => m.VectorSignal!.Contains(query));

        return await q
            .GroupBy(m => m.VectorSignal!)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ParticipantSuggestionDto>> GetParticipantSuggestionsAsync(
        string? query = null,
        int take = 15,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var q = db.InterceptionMessageParticipants
            .Where(p => !p.IsUnknown && p.Name != null);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Name!.StartsWith(query));

        return await q
            .GroupBy(p => p.Name!)
            .OrderByDescending(g => g.Count())
            .Take(take)
            .Select(g => new ParticipantSuggestionDto
            {
                Name = g.Key,
                Role = g.Where(p => p.Role != null)
                    .OrderByDescending(p => p.InterceptionMessage.ObservedDate)
                    .Select(p => p.Role)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }
}
