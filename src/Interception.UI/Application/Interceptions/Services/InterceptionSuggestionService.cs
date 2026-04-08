//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services;

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
        string? frequency = null,
        string? division = null,
        int take = 15,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rawQuery = db.InterceptionMessageParticipants
            .Where(p => !p.IsUnknown && p.Name != null);

        if (!string.IsNullOrWhiteSpace(query))
            rawQuery = rawQuery.Where(p => p.Name!.StartsWith(query));

        var rawRows = await rawQuery
            .Select(p => new ParticipantSuggestionRow(
                p.Name!,
                p.Role,
                p.InterceptionMessage.Frequency,
                p.InterceptionMessage.Division,
                p.InterceptionMessage.ObservedDate,
                IsCanonical: false))
            .ToListAsync(ct);

        var resolvedQuery = db.ResolvedParticipants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
            resolvedQuery = resolvedQuery.Where(p => p.Name.StartsWith(query));

        var resolvedRows = await resolvedQuery
            .Select(p => new ParticipantSuggestionRow(
                p.Name,
                p.Role,
                p.Frequency,
                p.Division,
                p.ConfirmedAt,
                IsCanonical: true))
            .ToListAsync(ct);

        var normalizedFrequency = NormalizeKey(frequency);
        var normalizedDivision = NormalizeKey(division);

        return [.. rawRows
            .Concat(resolvedRows)
            .GroupBy(x => new ParticipantSuggestionKey(
                NormalizeKey(x.Name),
                NormalizeKey(x.Frequency),
                NormalizeKey(x.Division)))
            .Select(g =>
            {
                var role = g.Where(x => !string.IsNullOrWhiteSpace(x.Role))
                    .GroupBy(x => NormalizeKey(x.Role))
                    .OrderByDescending(x => x.Any(v => v.IsCanonical))
                    .ThenByDescending(x => x.Count())
                    .ThenByDescending(x => x.Max(v => v.ObservedDate))
                    .Select(x => x.First().Role)
                    .FirstOrDefault();

                var latestObservedDate = g.Max(x => x.ObservedDate);
                var contextScore = 0;
                var hasCanonical = g.Any(x => x.IsCanonical);

                if (!string.IsNullOrWhiteSpace(normalizedFrequency)
                    && string.Equals(g.Key.Frequency, normalizedFrequency, StringComparison.Ordinal))
                    contextScore += 2;

                if (!string.IsNullOrWhiteSpace(normalizedDivision)
                    && string.Equals(g.Key.Division, normalizedDivision, StringComparison.Ordinal))
                    contextScore += 1;

                return new ParticipantSuggestionProjection(
                    new ParticipantSuggestionDto
                    {
                        Name = g.First().Name,
                        Frequency = g.First().Frequency,
                        Division = g.First().Division,
                        Role = role,
                        SeenCount = g.Count()
                    },
                    contextScore,
                    latestObservedDate,
                    hasCanonical);
            })
            .OrderByDescending(x => x.ContextScore)
            .ThenByDescending(x => x.HasCanonical)
            .ThenByDescending(x => x.Dto.SeenCount)
            .ThenByDescending(x => x.LatestObservedDate)
            .ThenBy(x => x.Dto.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Dto.Frequency, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Dto.Division, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(x => x.Dto)];
    }

    private static string NormalizeKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private sealed record ParticipantSuggestionRow(
        string Name,
        string? Role,
        string? Frequency,
        string? Division,
        DateTime ObservedDate,
        bool IsCanonical);

    private sealed record ParticipantSuggestionKey(
        string Name,
        string Frequency,
        string Division);

    private sealed record ParticipantSuggestionProjection(
        ParticipantSuggestionDto Dto,
        int ContextScore,
        DateTime LatestObservedDate,
        bool HasCanonical);
}
