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
public sealed class DayPictureService(IDbContextFactory<AppDbContext> dbFactory) : IDayPictureService
{
    private const string UnknownDivision = "НВ підрозділ";
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<DayPictureModel> BuildAsync(DateOnly day, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var fromUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(1);

        var messages = await db.InterceptionMessages
            .AsNoTracking()
            .Include(x => x.InterceptionAction)
            .Include(x => x.Participants.OrderBy(p => p.Ordinal))
            .Where(x => x.ObservedDate >= fromUtc && x.ObservedDate < toUtc)
            .OrderBy(x => x.ObservedDate)
            .ToListAsync(ct);

        if (messages.Count == 0)
            return new DayPictureModel(day, 0, []);

        var frequencyDivisionMap = await BuildFrequencyDivisionMapAsync(db, ct);

        var rows = messages
            .Select(message => new
            {
                Message = message,
                EffectiveDivision = ResolveEffectiveDivision(message.Division, message.Frequency, frequencyDivisionMap)
            })
            .ToList();

        var groups = rows
            .GroupBy(x => new
            {
                Frequency = NormalizeMeaningfulOrNull(x.Message.Frequency),
                VectorSignal = NormalizeMeaningfulOrNull(x.Message.VectorSignal),
                Division = NormalizeMeaningfulOrNull(x.EffectiveDivision)
            })
            .Select(group => new DayPictureGroupModel(
                GroupKey: BuildGroupKey(group.Key.Frequency, group.Key.VectorSignal, group.Key.Division),
                Frequency: group.Key.Frequency,
                VectorSignal: group.Key.VectorSignal,
                Division: group.Key.Division,
                MessageCount: group.Count(),
                Entries: group
                    .OrderBy(x => x.Message.ObservedDate)
                    .Select(x => new DayPictureEntryModel(
                        MessageId: x.Message.Id,
                        ObservedDate: x.Message.ObservedDate,
                        Frequency: NormalizeMeaningfulOrNull(x.Message.Frequency),
                        Division: group.Key.Division,
                        VectorSignal: NormalizeMeaningfulOrNull(x.Message.VectorSignal),
                        ActionName: x.Message.InterceptionAction?.Name,
                        Note: x.Message.Note,
                        Participants: [.. x.Message.Participants
                            .OrderBy(p => p.Ordinal)
                            .Select(p => p.Name)
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .Select(p => p!.Trim())]))
                    .ToList()))
            .OrderBy(x => x.Division)
            .ThenBy(x => x.Frequency)
            .ThenBy(x => x.VectorSignal)
            .ToList();

        return new DayPictureModel(day, messages.Count, groups);
    }

    /// <summary>
    /// Будує карту частота → домінуючий підрозділ за всіма наявними спостереженнями.
    /// </summary>
    private static async Task<Dictionary<string, string>> BuildFrequencyDivisionMapAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var rows = await db.InterceptionMessages
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.Frequency))
            .Select(x => new
            {
                Frequency = x.Frequency!,
                x.Division
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.Frequency, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Frequency = group.Key,
                Division = group
                    .Select(x => NormalizeMeaningfulOrNull(x.Division))
                    .Where(x => x is not null)
                    .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(x => x.Count())
                    .ThenBy(x => x.Key)
                    .Select(x => x.Key)
                    .FirstOrDefault()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Division))
            .ToDictionary(x => x.Frequency, x => x.Division!, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Повертає effective division для повідомлення.
    /// </summary>
    private static string? ResolveEffectiveDivision(
        string? observedDivision,
        string? frequency,
        IReadOnlyDictionary<string, string> frequencyDivisionMap)
    {
        var normalizedObserved = NormalizeMeaningfulOrNull(observedDivision);
        if (normalizedObserved is not null)
            return normalizedObserved;

        var normalizedFrequency = NormalizeMeaningfulOrNull(frequency);
        if (normalizedFrequency is not null
            && frequencyDivisionMap.TryGetValue(normalizedFrequency, out var division)
            && !string.IsNullOrWhiteSpace(division))
        {
            return division;
        }

        return null;
    }

    /// <summary>
    /// Нормалізує значення для read-side.
    /// </summary>
    private static string? NormalizeMeaningfulOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return string.Equals(trimmed, UnknownDivision, StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
    }

    /// <summary>
    /// Будує стабільний ключ групи для UI.
    /// </summary>
    private static string BuildGroupKey(string? frequency, string? vectorSignal, string? division)
        => string.Join(" | ", new[] { frequency ?? "—", vectorSignal ?? "—", division ?? "—" });
}
