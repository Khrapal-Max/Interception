//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Registry;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services.Registry;

/// <summary>
/// Простий сервіс частот і закріплених за ними підрозділів.
/// </summary>
public sealed class FrequencyDivisionService(IDbContextFactory<AppDbContext> dbFactory)
    : IFrequencyDivisionService
{
    private const string UnknownDivision = "НВ підрозділ";
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<IReadOnlyList<FrequencySuggestionDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rows = await db.InterceptionMessages
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.Frequency))
            .Select(x => new
            {
                Frequency = x.Frequency!,
                x.Division
            })
            .ToListAsync(ct);

        var result = rows
            .GroupBy(x => x.Frequency, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var division = group
                    .Select(x => x.Division)
                    .Where(x => !string.IsNullOrWhiteSpace(x) && x != "НВ підрозділ")
                    .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(x => x.Count())
                    .ThenBy(x => x.Key)
                    .Select(x => x.Key)
                    .FirstOrDefault();

                return new FrequencySuggestionDto
                {
                    Frequency = group.Key,
                    Division = division
                };
            })
            .OrderBy(x => x.Frequency)
            .ToList();

        return result;
    }

    /// <inheritdoc />
    public async Task CorrectDivisionAsync(
        string frequency,
        string division,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(frequency))
            throw new ArgumentException("Частота не вказана.", nameof(frequency));

        if (string.IsNullOrWhiteSpace(division))
            throw new ArgumentException("Підрозділ не вказано.", nameof(division));

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var normalizedFrequency = frequency.Trim();
        var normalizedDivision = division.Trim();

        var messages = await db.InterceptionMessages
            .Where(x => x.Frequency == normalizedFrequency)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Division) ||
                message.Division == "НВ підрозділ")
            {
                message.Division = normalizedDivision;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Перевіряє, чи значення підрозділу ще не зафіксоване.
    /// </summary>
    private static bool IsUnknownDivision(string? division)
        => string.IsNullOrWhiteSpace(division)
           || string.Equals(division.Trim(), UnknownDivision, StringComparison.OrdinalIgnoreCase);
}
