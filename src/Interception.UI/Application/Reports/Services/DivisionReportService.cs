//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Reports.Abstractions;
using Interception.UI.Application.Reports.Dtos;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Reports.Services;

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

        var canonicalByName = await LoadCanonicalByNameMapAsync(
            db,
            messageRows.SelectMany(x => x.Message.Participants).Select(x => x.Name),
            ct);

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

        var observedPeople = BuildObservedPeople(messageRows, canonicalByName);

        var divisions = messageRows
            .Select(x => x.EffectiveDivision!)
            .Concat(confirmedPeople.Select(x => x.Division))
            .Concat(observedPeople.Select(x => x.Division))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var groups = divisions
            .Select(division => BuildGroup(
                division,
                [.. messageRows.Where(x => string.Equals(x.EffectiveDivision, division, StringComparison.OrdinalIgnoreCase)).Select(x => x.Message)],
                [.. observedPeople.Where(x => string.Equals(x.Division, division, StringComparison.OrdinalIgnoreCase))],
                [.. confirmedPeople.Where(x => string.Equals(x.Division, division, StringComparison.OrdinalIgnoreCase))],
                unknownGroupsCountByDivision))
            .OrderBy(x => x.Division)
            .ToList();

        return new DivisionReportDto(groups);
    }

    private static DivisionReportGroupDto BuildGroup(
        string division,
        List<InterceptionMessage> messages,
        List<ObservedPersonRow> observedPeople,
        List<ConfirmedPersonRow> confirmedPeople,
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
        List<ObservedPersonRow> observedPeople,
        List<ConfirmedPersonRow> confirmedPeople)
    {
        var people = new List<(string PersonKey, string Name, string? Role, DateTime LastSeenAt)>();
        people.AddRange(observedPeople.Select(x => (x.PersonKey, x.Name, x.Role, x.LastSeenAt)));
        people.AddRange(confirmedPeople.Select(x => (x.PersonKey, x.Name, x.Role, x.LastSeenAt)));

        return [.. people
            .GroupBy(x => x.PersonKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(x => x.LastSeenAt)
                    .ToList();

                var (PersonKey, Name, Role, LastSeenAt) = ordered.First();

                var lastNonEmptyRole = ordered
                    .Select(x => x.Role)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                return new DivisionReportPersonRowDto(
                    PersonKey: PersonKey,
                    Name: Name,
                    Role: lastNonEmptyRole,
                    LastSeenAt: LastSeenAt);
            })
            .OrderBy(x => x.Name)];
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
    /// Якщо для імені є канонічна особа — склеює записи по ній.
    /// </summary>
    private static List<ObservedPersonRow> BuildObservedPeople(
        List<(InterceptionMessage Message, string? EffectiveDivision)> messageRows,
        IReadOnlyDictionary<string, Guid> canonicalByName)
    {
        var observations = messageRows
            .SelectMany(messageRow => messageRow.Message.Participants
                .Where(participant =>
                    !participant.IsUnknown &&
                    !string.IsNullOrWhiteSpace(participant.Name) &&
                    !string.IsNullOrWhiteSpace(messageRow.EffectiveDivision))
                .Select(participant => new
                {
                    Division = messageRow.EffectiveDivision!,
                    Name = participant.Name!,
                    participant.Role,
                    LastSeenAt = messageRow.Message.ObservedDate,
                    PersonKey = BuildObservedPersonKey(participant.Name, canonicalByName)
                }))
            .ToList();

        return [.. observations
            .GroupBy(x => x.PersonKey, StringComparer.OrdinalIgnoreCase)
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

                var first = ordered.First();
                var lastNonEmptyRole = ordered
                    .Select(x => x.Role)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                return new ObservedPersonRow(
                    Division: assignedDivision,
                    PersonKey: first.PersonKey,
                    Name: first.Name,
                    Role: lastNonEmptyRole,
                    LastSeenAt: first.LastSeenAt);
            })];
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
    private static async Task<List<ConfirmedPersonRow>> LoadConfirmedPeopleAsync(
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

        var canonicalByResolved = await LoadCanonicalByResolvedMapAsync(db, resolvedIds, ct);

        var people = new List<ConfirmedPersonRow>();

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

            people.Add(new ConfirmedPersonRow(
                Division: division,
                PersonKey: BuildConfirmedPersonKey(resolvedParticipant.Id, canonicalByResolved),
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


    private static async Task<Dictionary<Guid, Guid>> LoadCanonicalByResolvedMapAsync(
        AppDbContext db,
        IEnumerable<Guid> resolvedParticipantIds,
        CancellationToken ct)
    {
        var ids = resolvedParticipantIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await db.CanonicalPersonMembers
            .AsNoTracking()
            .Where(x => ids.Contains(x.ResolvedParticipantId))
            .ToDictionaryAsync(x => x.ResolvedParticipantId, x => x.CanonicalPersonId, ct);
    }

    private static async Task<Dictionary<string, Guid>> LoadCanonicalByNameMapAsync(
        AppDbContext db,
        IEnumerable<string?> names,
        CancellationToken ct)
    {
        var normalizedNames = names
            .Select(NormalizePersonName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalizedNames.Count == 0)
            return [];

        var rows = await (
                from member in db.CanonicalPersonMembers.AsNoTracking()
                join resolved in db.ResolvedParticipants.AsNoTracking() on member.ResolvedParticipantId equals resolved.Id
                where !string.IsNullOrWhiteSpace(resolved.Name)
                select new { member.CanonicalPersonId, resolved.Name })
            .ToListAsync(ct);

        return rows
            .Select(x => new { x.CanonicalPersonId, Name = NormalizePersonName(x.Name) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && normalizedNames.Contains(x.Name))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Select(v => v.CanonicalPersonId).Distinct().Count() == 1)
            .ToDictionary(x => x.Key, x => x.First().CanonicalPersonId, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildConfirmedPersonKey(Guid resolvedParticipantId, IReadOnlyDictionary<Guid, Guid> canonicalByResolved)
        => canonicalByResolved.TryGetValue(resolvedParticipantId, out var canonicalId)
            ? $"canonical:{canonicalId}"
            : $"resolved:{resolvedParticipantId}";

    private static string BuildObservedPersonKey(string? name, IReadOnlyDictionary<string, Guid> canonicalByName)
    {
        var normalizedName = NormalizePersonName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
            return $"name:{Guid.NewGuid()}";

        return canonicalByName.TryGetValue(normalizedName, out var canonicalId)
            ? $"canonical:{canonicalId}"
            : $"name:{normalizedName}";
    }

    private static string NormalizePersonName(string? name)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(name)?.Trim().ToUpperInvariant() ?? string.Empty;

    private sealed record ObservedPersonRow(
        string Division,
        string PersonKey,
        string Name,
        string? Role,
        DateTime LastSeenAt);

    private sealed record ConfirmedPersonRow(
        string Division,
        string PersonKey,
        string Name,
        string? Role,
        DateTime LastSeenAt);

    private static bool IsMeaningfulDivision(string? division)
        => !string.IsNullOrWhiteSpace(division)
           && !string.Equals(division.Trim(), UnknownDivision, StringComparison.OrdinalIgnoreCase);
}
