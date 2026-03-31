//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Reports.Abstractions;
using Interception.UI.Application.Reports.Models;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services.Reports;

/// <summary>
/// Будує картину дня у моделі:
/// підрозділ → епізоди дня → хронологічні записи.
/// </summary>
public sealed partial class DayPictureService(IDbContextFactory<AppDbContext> dbFactory) : IDayPictureService
{
    private const string UnknownDivision = "НВ підрозділ";
    private static readonly TimeSpan ConversationGap = TimeSpan.FromMinutes(10);
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
            .Select(message => new Row(
                Message: message,
                EffectiveDivision: ResolveEffectiveDivision(message.Division, message.Frequency, frequencyDivisionMap),
                ParticipantSet: message.Participants
                    .Select(p => p.Name)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => p!.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)))
            .ToList();

        var groups = rows
            .GroupBy(x => NormalizeMeaningfulOrNull(x.EffectiveDivision) ?? "—", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var orderedRows = group.OrderBy(x => x.Message.ObservedDate).ToList();
                var conversations = BuildConversations(group.Key, orderedRows);

                return new DayPictureGroupModel(
                    GroupKey: group.Key,
                    Division: group.Key == "—" ? null : group.Key,
                    MessageCount: orderedRows.Count,
                    Conversations: conversations);
            })
            .OrderBy(x => x.Division ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DayPictureModel(day, messages.Count, groups);
    }

    private static List<DayPictureConversationModel> BuildConversations(string divisionKey, List<Row> rows)
    {
        var result = new List<DayPictureConversationModel>();
        if (rows.Count == 0)
            return result;

        var current = new List<Row> { rows[0] };
        var startedAt = rows[0].Message.ObservedDate;

        for (var i = 1; i < rows.Count; i++)
        {
            var previous = current[^1];
            var next = rows[i];

            if (BelongsToSameConversation(previous, next))
            {
                current.Add(next);
                continue;
            }

            result.Add(ToConversation(divisionKey, startedAt, current));
            current = [next];
            startedAt = next.Message.ObservedDate;
        }

        result.Add(ToConversation(divisionKey, startedAt, current));
        return result;
    }

    private static bool BelongsToSameConversation(Row previous, Row next)
    {
        var gap = next.Message.ObservedDate - previous.Message.ObservedDate;
        if (gap > ConversationGap)
            return false;

        if (SameFrequency(previous, next))
            return true;

        if (HaveParticipantOverlap(previous, next))
            return true;

        return true;
    }

    private static bool SameFrequency(Row left, Row right)
        => string.Equals(
            NormalizeMeaningfulOrNull(left.Message.Frequency),
            NormalizeMeaningfulOrNull(right.Message.Frequency),
            StringComparison.OrdinalIgnoreCase);

    private static bool HaveParticipantOverlap(Row left, Row right)
        => left.ParticipantSet.Overlaps(right.ParticipantSet);

    private static DayPictureConversationModel ToConversation(string divisionKey, DateTime startedAtUtc, List<Row> rows)
    {
        var endedAtUtc = rows[^1].Message.ObservedDate;
        var key = $"{divisionKey} | {startedAtUtc:yyyy-MM-dd HH:mm:ss} | {endedAtUtc:yyyy-MM-dd HH:mm:ss}";

        return new DayPictureConversationModel(
            ConversationKey: key,
            StartedAtUtc: startedAtUtc,
            EndedAtUtc: endedAtUtc,
            MessageCount: rows.Count,
            Entries: [.. rows
                .OrderBy(x => x.Message.ObservedDate)
                .Select(x => new DayPictureEntryModel(
                    MessageId: x.Message.Id,
                    ObservedDate: x.Message.ObservedDate,
                    Frequency: NormalizeMeaningfulOrNull(x.Message.Frequency),
                    Division: NormalizeMeaningfulOrNull(x.EffectiveDivision),
                    VectorSignal: NormalizeMeaningfulOrNull(x.Message.VectorSignal),
                    ActionName: x.Message.InterceptionAction?.Name,
                    Note: x.Message.Note,
                    Participants: [.. x.Message.Participants
                        .OrderBy(p => p.Ordinal)
                        .Select(p => p.Name)
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(p => p!.Trim())]))]);
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
            .GroupBy(x => x.Frequency.Trim(), StringComparer.OrdinalIgnoreCase)
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
        Dictionary<string, string> frequencyDivisionMap)
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
}
