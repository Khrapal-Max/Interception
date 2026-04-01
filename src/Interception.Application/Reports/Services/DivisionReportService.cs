//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Reports.Abstractions;
using Interception.Application.Reports.Dtos;
using Interception.Domain.Entities;
using Interception.Domain.Enums;
using Interception.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Application.Reports.Services;

/// <summary>
/// Будує простий зведений звіт по підрозділах.
/// </summary>
public sealed class DivisionReportService(IDbContextFactory<AppDbContext> dbFactory)
    : IDivisionReportService
{
    private const string UnknownDivision = "НВ підрозділ";
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<DivisionReportDto> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var messagesQuery = db.InterceptionMessages
            .AsNoTracking()
            .Include(x => x.Participants)
            .AsQueryable();

        if (dateFrom.HasValue)
            messagesQuery = messagesQuery.Where(x => x.ObservedDate >= dateFrom.Value);

        if (dateTo.HasValue)
            messagesQuery = messagesQuery.Where(x => x.ObservedDate <= dateTo.Value);

        var messages = await messagesQuery.ToListAsync(ct);

        if (messages.Count == 0)
            return new DivisionReportDto([]);

        var frequencyDivisionMap = BuildFrequencyDivisionMap(messages);

        var messageRows = messages
            .Select(message => (
                Message: message,
                EffectiveDivision: ResolveEffectiveDivision(message.Division, message.Frequency, frequencyDivisionMap)))
            .Where(x => !string.IsNullOrWhiteSpace(x.EffectiveDivision))
            .ToList();

        if (messageRows.Count == 0)
            return new DivisionReportDto([]);

        var messageIds = messageRows
            .Select(x => x.Message.Id)
            .ToHashSet();

        var messageDates = messageRows
            .ToDictionary(x => x.Message.Id, x => x.Message.ObservedDate);

        var messageEffectiveDivisions = messageRows
            .ToDictionary(x => x.Message.Id, x => x.EffectiveDivision!, EqualityComparer<Guid>.Default);

        var unknownGroupsCountByDivision = await LoadUnknownGroupsCountByDivisionAsync(
            db,
            messageIds,
            messageEffectiveDivisions,
            ct);

        var confirmedPeople = await LoadConfirmedPeopleAsync(
            db,
            messageIds,
            messageDates,
            messageEffectiveDivisions,
            ct);

        var observedPeople = BuildObservedPeople(messageRows);

        var divisions = messageRows
            .Select(x => x.EffectiveDivision!)
            .Concat(confirmedPeople.Select(x => x.Division))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var groups = divisions
            .Select(division => BuildGroup(
                division,
                messageRows.Where(x => string.Equals(x.EffectiveDivision, division, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Message)
                    .ToList(),
                observedPeople.Where(x => string.Equals(x.Division, division, StringComparison.OrdinalIgnoreCase)).ToList(),
                confirmedPeople.Where(x => string.Equals(x.Division, division, StringComparison.OrdinalIgnoreCase)).ToList(),
                unknownGroupsCountByDivision))
            .OrderBy(x => x.Division)
            .ToList();

        return new DivisionReportDto(groups);
    }

    private static DivisionReportGroupDto BuildGroup(
        string division,
        List<InterceptionMessage> messages,
        List<(string Division, string Name, string? Role, DateTime LastSeenAt)> observedPeople,
        List<(string Division, string Name, string? Role, DateTime LastSeenAt)> confirmedPeople,
        Dictionary<string, int> unknownGroupsCountByDivision)
    {
        var frequencies = messages
            .Select(x => x.Frequency)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var unknownMentionsCount = messages
            .SelectMany(x => x.Participants)
            .Count(x => x.IsUnknown);

        var unknownGroupsCount = unknownGroupsCountByDivision.TryGetValue(division, out var count)
            ? count
            : 0;

        var reportPeople = BuildPeople(observedPeople, confirmedPeople);

        return new DivisionReportGroupDto(
            Division: division,
            Frequencies: frequencies,
            UnknownMentionsCount: unknownMentionsCount,
            UnknownGroupsCount: unknownGroupsCount,
            People: reportPeople);
    }

    private static List<DivisionReportPersonRowDto> BuildPeople(
        List<(string Division, string Name, string? Role, DateTime LastSeenAt)> observedPeople,
        List<(string Division, string Name, string? Role, DateTime LastSeenAt)> confirmedPeople)
    {
        var people = new List<(string Name, string? Role, DateTime LastSeenAt)>();
        people.AddRange(observedPeople.Select(x => (x.Name, x.Role, x.LastSeenAt)));
        people.AddRange(confirmedPeople.Select(x => (x.Name, x.Role, x.LastSeenAt)));

        return people
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(x => x.LastSeenAt)
                    .ToList();

                var latest = ordered.First();

                var lastNonEmptyRole = ordered
                    .Select(x => x.Role)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                return new DivisionReportPersonRowDto(
                    PersonKey: latest.Name.Trim().ToUpperInvariant(),
                    Name: latest.Name,
                    Role: lastNonEmptyRole,
                    LastSeenAt: latest.LastSeenAt);
            })
            .OrderBy(x => x.Name)
            .ToList();
    }

    /// <summary>
    /// Будує карту частота → домінуючий підрозділ за наявними спостереженнями.
    /// </summary>
    private static Dictionary<string, string> BuildFrequencyDivisionMap(List<InterceptionMessage> messages)
    {
        return messages
            .Where(x => !string.IsNullOrWhiteSpace(x.Frequency))
            .GroupBy(x => x.Frequency!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Frequency = group.Key,
                Division = group
                    .Select(x => x.Division)
                    .Where(IsMeaningfulDivision)
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
    /// Повертає ефективний підрозділ для повідомлення.
    /// </summary>
    private static string? ResolveEffectiveDivision(
        string? observedDivision,
        string? frequency,
        IReadOnlyDictionary<string, string> frequencyDivisionMap)
    {
        if (IsMeaningfulDivision(observedDivision))
            return observedDivision!.Trim();

        if (!string.IsNullOrWhiteSpace(frequency)
            && frequencyDivisionMap.TryGetValue(frequency.Trim(), out var division)
            && IsMeaningfulDivision(division))
        {
            return division;
        }

        return null;
    }

    /// <summary>
    /// Будує observed-known осіб і відносить кожну до домінуючого effective division.
    /// </summary>
    private static List<(string Division, string Name, string? Role, DateTime LastSeenAt)> BuildObservedPeople(
        List<(InterceptionMessage Message, string? EffectiveDivision)> messageRows)
    {
        var observations = messageRows
            .SelectMany(messageRow => messageRow.Message.Participants
                .Where(participant =>
                    !participant.IsUnknown &&
                    !string.IsNullOrWhiteSpace(participant.Name) &&
                    !string.IsNullOrWhiteSpace(messageRow.EffectiveDivision))
                .Select(participant => (
                    Division: messageRow.EffectiveDivision!,
                    Name: participant.Name!,
                    participant.Role,
                    LastSeenAt: messageRow.Message.ObservedDate)))
            .ToList();

        return observations
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var assignedDivision = group
                    .GroupBy(x => x.Division, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(x => x.Count())
                    .ThenByDescending(x => x.Max(y => y.LastSeenAt))
                    .ThenBy(x => x.Key)
                    .Select(x => x.Key)
                    .First();

                var ordered = group
                    .OrderByDescending(x => x.LastSeenAt)
                    .ToList();

                var latest = ordered.First();

                var lastNonEmptyRole = ordered
                    .Select(x => x.Role)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                return (
                    Division: assignedDivision,
                    Name: latest.Name,
                    Role: lastNonEmptyRole,
                    LastSeenAt: latest.LastSeenAt);
            })
            .ToList();
    }

    /// <summary>
    /// Рахує open-групи НВ за effective division пов'язаних повідомлень.
    /// </summary>
    private static async Task<Dictionary<string, int>> LoadUnknownGroupsCountByDivisionAsync(
        AppDbContext db,
        HashSet<Guid> messageIds,
        IReadOnlyDictionary<Guid, string> messageEffectiveDivisions,
        CancellationToken ct)
    {
        var openUnknownGroups = await db.ParticipantCandidateGroups
            .AsNoTracking()
            .Where(x => x.Status == CandidateGroupStatus.Open)
            .ToListAsync(ct);

        return openUnknownGroups
            .Where(x => x.ParticipantRefs.Any(r => messageIds.Contains(r.MessageId)))
            .Select(group => ResolveGroupDivision(group, messageEffectiveDivisions))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Завантажує підтверджених осіб і визначає effective division з пріоритетом confirmed division.
    /// </summary>
    private static async Task<List<(string Division, string Name, string? Role, DateTime LastSeenAt)>> LoadConfirmedPeopleAsync(
        AppDbContext db,
        HashSet<Guid> messageIds,
        IReadOnlyDictionary<Guid, DateTime> messageDates,
        IReadOnlyDictionary<Guid, string> messageEffectiveDivisions,
        CancellationToken ct)
    {
        var confirmedGroups = await db.ParticipantCandidateGroups
            .AsNoTracking()
            .Where(x =>
                x.Status == CandidateGroupStatus.Confirmed &&
                x.ResolvedParticipantId.HasValue)
            .ToListAsync(ct);

        confirmedGroups = [.. confirmedGroups.Where(x => x.ParticipantRefs.Any(r => messageIds.Contains(r.MessageId)))];

        if (confirmedGroups.Count == 0)
            return [];

        var resolvedIds = confirmedGroups
            .Select(x => x.ResolvedParticipantId!.Value)
            .Distinct()
            .ToList();

        var resolvedParticipants = await db.ResolvedParticipants
            .AsNoTracking()
            .Where(x => resolvedIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var people = new List<(string Division, string Name, string? Role, DateTime LastSeenAt)>();

        foreach (var group in confirmedGroups)
        {
            if (!resolvedParticipants.TryGetValue(group.ResolvedParticipantId!.Value, out var resolvedParticipant))
                continue;

            var lastSeenAt = group.ParticipantRefs
                .Where(x => messageDates.ContainsKey(x.MessageId))
                .Select(x => messageDates[x.MessageId])
                .DefaultIfEmpty()
                .Max();

            if (lastSeenAt == default)
                continue;

            var division = ResolveConfirmedDivision(resolvedParticipant.Division, group, messageEffectiveDivisions);
            if (string.IsNullOrWhiteSpace(division))
                continue;

            people.Add((
                Division: division,
                Name: resolvedParticipant.Name,
                Role: resolvedParticipant.Role ?? group.SuggestedRole,
                LastSeenAt: lastSeenAt));
        }

        return people;
    }

    private static string? ResolveConfirmedDivision(
        string? confirmedDivision,
        ParticipantCandidateGroup group,
        IReadOnlyDictionary<Guid, string> messageEffectiveDivisions)
    {
        if (IsMeaningfulDivision(confirmedDivision))
            return confirmedDivision!.Trim();

        var dominantObservedDivision = group.ParticipantRefs
            .Select(x => messageEffectiveDivisions.TryGetValue(x.MessageId, out var division) ? division : null)
            .Where(IsMeaningfulDivision)
            .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(dominantObservedDivision))
            return dominantObservedDivision;

        if (IsMeaningfulDivision(group.SuggestedDivision))
            return group.SuggestedDivision!.Trim();

        return null;
    }

    private static string? ResolveGroupDivision(
        ParticipantCandidateGroup group,
        IReadOnlyDictionary<Guid, string> messageEffectiveDivisions)
    {
        var dominantObservedDivision = group.ParticipantRefs
            .Select(x => messageEffectiveDivisions.TryGetValue(x.MessageId, out var division) ? division : null)
            .Where(IsMeaningfulDivision)
            .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(dominantObservedDivision))
            return dominantObservedDivision;

        if (IsMeaningfulDivision(group.SuggestedDivision))
            return group.SuggestedDivision!.Trim();

        return null;
    }

    private static bool IsMeaningfulDivision(string? division)
        => !string.IsNullOrWhiteSpace(division)
           && !string.Equals(division.Trim(), UnknownDivision, StringComparison.OrdinalIgnoreCase);
}
