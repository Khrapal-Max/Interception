//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Reports.Abstractions;
using Interception.UI.Application.Reports.Dtos;
using Interception.UI.Domain.Analytics;
using Interception.UI.Domain.Analytics.Enums;
using Interception.UI.Domain.Interceptions;
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

        var unknownGroupsCountByDivision = await LoadUnknownGroupsCountByDivisionAsync(
            db,
            messageIds,
            messageEffectiveDivisions,
            ct);

        var canonicalByResolvedMap = await LoadCanonicalByResolvedMapAsync(db, ct);
        var canonicalByNameMap = await LoadCanonicalByNameMapAsync(db, ct);
        var canonicalDisplayMap = await LoadCanonicalDisplayMapAsync(db, ct);

        var confirmedPeople = await LoadConfirmedPeopleAsync(
            db,
            messageIds,
            messageDates,
            messageEffectiveDivisions,
            canonicalByResolvedMap,
            canonicalDisplayMap,
            ct);

        var observedPeople = BuildObservedPeople(messageRows, canonicalByNameMap, canonicalDisplayMap);

        var divisions = messageRows
            .Select(x => x.EffectiveDivision!)
            .Concat(confirmedPeople.Select(x => x.Division))
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
        List<PersonFact> observedPeople,
        List<PersonFact> confirmedPeople,
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
        List<PersonFact> observedPeople,
        List<PersonFact> confirmedPeople)
    {
        var people = new List<PersonFact>();
        people.AddRange(observedPeople);
        people.AddRange(confirmedPeople);

        return [.. people
            .GroupBy(x => x.IdentityKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(x => x.LastSeenAt)
                    .ToList();

                var current = ordered.First();

                var lastNonEmptyRole = ordered
                    .Select(x => x.Role)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                return new DivisionReportPersonRowDto(
                    PersonKey: current.IdentityKey,
                    Name: current.Name,
                    Role: lastNonEmptyRole,
                    LastSeenAt: current.LastSeenAt);
            })
            .OrderBy(x => x.Name)];
    }

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

    private static List<PersonFact> BuildObservedPeople(
        List<(InterceptionMessage Message, string? EffectiveDivision)> messageRows,
        IReadOnlyDictionary<string, Guid> canonicalByNameMap,
        IReadOnlyDictionary<Guid, string> canonicalDisplayMap)
    {
        return [.. messageRows
            .SelectMany(messageRow => messageRow.Message.Participants
                .Where(participant =>
                    !participant.IsUnknown &&
                    !string.IsNullOrWhiteSpace(participant.Name) &&
                    !string.IsNullOrWhiteSpace(messageRow.EffectiveDivision))
                .Select(participant =>
                {
                    var normalizedName = StringTextNormExtensions.NormalizeOption(participant.Name);
                    var identityKey = BuildObservedIdentityKey(
                        normalizedName,
                        canonicalByNameMap);
                    var canonicalDisplayName = TryResolveCanonicalDisplayName(identityKey, canonicalDisplayMap);
                    var displayName = canonicalDisplayName ?? participant.Name!;

                    return new PersonFact(
                        messageRow.EffectiveDivision!,
                        identityKey,
                        displayName,
                        participant.Role,
                        messageRow.Message.ObservedDate);
                }))];
    }

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

    private static async Task<List<PersonFact>> LoadConfirmedPeopleAsync(
        AppDbContext db,
        HashSet<Guid> messageIds,
        IReadOnlyDictionary<Guid, DateTime> messageDates,
        IReadOnlyDictionary<Guid, string> messageEffectiveDivisions,
        IReadOnlyDictionary<Guid, Guid> canonicalByResolvedMap,
        IReadOnlyDictionary<Guid, string> canonicalDisplayMap,
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

        var people = new List<PersonFact>();

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

            var identityKey = canonicalByResolvedMap.TryGetValue(resolvedParticipant.Id, out var canonicalId)
                ? $"cp:{canonicalId}"
                : $"resolved:{resolvedParticipant.Id}";
            var displayName = canonicalByResolvedMap.TryGetValue(resolvedParticipant.Id, out var canonicalDisplayId)
                && canonicalDisplayMap.TryGetValue(canonicalDisplayId, out var canonicalDisplayName)
                && !string.IsNullOrWhiteSpace(canonicalDisplayName)
                ? canonicalDisplayName
                : resolvedParticipant.Name;

            people.Add(new PersonFact(
                division,
                identityKey,
                displayName,
                resolvedParticipant.Role ?? group.SuggestedRole,
                lastSeenAt));
        }

        return people;
    }

    private static async Task<Dictionary<Guid, Guid>> LoadCanonicalByResolvedMapAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var rows = await db.CanonicalPersonMembers
            .AsNoTracking()
            .Select(x => new
            {
                x.ResolvedParticipantId,
                x.CanonicalPersonId
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.ResolvedParticipantId)
            .Where(x => x.Select(v => v.CanonicalPersonId).Distinct().Count() == 1)
            .ToDictionary(x => x.Key, x => x.First().CanonicalPersonId);
    }

    private static async Task<Dictionary<string, Guid>> LoadCanonicalByNameMapAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var rows = await db.CanonicalPersonMembers
            .AsNoTracking()
            .Join(
                db.ResolvedParticipants.AsNoTracking(),
                member => member.ResolvedParticipantId,
                resolved => resolved.Id,
                (member, resolved) => new
                {
                    member.CanonicalPersonId,
                    resolved.Name
                })
            .ToListAsync(ct);

        return rows
            .Select(x => new
            {
                x.CanonicalPersonId,
                NormalizedName = StringTextNormExtensions.NormalizeOption(x.Name)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedName))
            .GroupBy(x => x.NormalizedName!, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Select(v => v.CanonicalPersonId).Distinct().Count() == 1)
            .ToDictionary(x => x.Key, x => x.First().CanonicalPersonId, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<Guid, string>> LoadCanonicalDisplayMapAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        return await db.CanonicalPersons
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.DisplayName))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName.Trim(), ct);
    }

    private static string BuildObservedIdentityKey(
        string? normalizedName,
        IReadOnlyDictionary<string, Guid> canonicalByNameMap)
    {
        if (!string.IsNullOrWhiteSpace(normalizedName)
            && canonicalByNameMap.TryGetValue(normalizedName, out var canonicalId))
        {
            return $"cp:{canonicalId}";
        }

        if (!string.IsNullOrWhiteSpace(normalizedName))
            return $"observed:{normalizedName}";

        return "observed:unknown";
    }

    private static string? TryResolveCanonicalDisplayName(
        string identityKey,
        IReadOnlyDictionary<Guid, string> canonicalDisplayMap)
    {
        if (!identityKey.StartsWith("cp:", StringComparison.OrdinalIgnoreCase))
            return null;

        var canonicalIdValue = identityKey[3..];
        if (!Guid.TryParse(canonicalIdValue, out var canonicalId))
            return null;

        return canonicalDisplayMap.TryGetValue(canonicalId, out var displayName)
            ? displayName
            : null;
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

    private sealed record PersonFact(
        string Division,
        string IdentityKey,
        string Name,
        string? Role,
        DateTime LastSeenAt);
}
