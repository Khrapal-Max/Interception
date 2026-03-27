//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Reports;
using Interception.UI.Application.Interceptions.Models.Reports;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services.Reports;

/// <summary>
/// Будує простий зведений звіт по підрозділах.
/// </summary>
public sealed class DivisionReportService(IDbContextFactory<AppDbContext> dbFactory)
    : IDivisionReportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<DivisionReportModel> BuildAsync(
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

        var messages = await messagesQuery
            .Where(x => !string.IsNullOrWhiteSpace(x.Division))
            .ToListAsync(ct);

        var messageIds = messages
            .Select(x => x.Id)
            .ToHashSet();

        var messageDates = messages
            .ToDictionary(x => x.Id, x => x.ObservedDate);

        var messageDivisions = messages
            .Where(x => !string.IsNullOrWhiteSpace(x.Division))
            .ToDictionary(x => x.Id, x => x.Division!, EqualityComparer<Guid>.Default);

        var openUnknownGroups = await db.ParticipantCandidateGroups
            .AsNoTracking()
            .Where(x =>
                x.Status == CandidateGroupStatus.Open &&
                !string.IsNullOrWhiteSpace(x.SuggestedDivision))
            .ToListAsync(ct);

        var unknownGroupsCountByDivision = openUnknownGroups
            .Where(x => x.ParticipantRefs.Any(r => messageIds.Contains(r.MessageId)))
            .GroupBy(x => x.SuggestedDivision!)
            .ToDictionary(
                x => x.Key,
                x => x.Count(),
                StringComparer.OrdinalIgnoreCase);

        var confirmedPeople = await LoadConfirmedPeopleAsync(
            db,
            messageIds,
            messageDates,
            messageDivisions,
            ct);

        var confirmedPeopleByDivision = confirmedPeople
            .GroupBy(x => x.Division, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var groups = messages
            .GroupBy(x => x.Division!, StringComparer.OrdinalIgnoreCase)
            .Select(x => BuildGroup(x.Key, [.. x], unknownGroupsCountByDivision, confirmedPeopleByDivision))
            .OrderBy(x => x.Division)
            .ToList();

        return new DivisionReportModel(groups);
    }

    private static DivisionReportGroupModel BuildGroup(
        string division,
        List<InterceptionMessage> messages,
        Dictionary<string, int> unknownGroupsCountByDivision,
        Dictionary<string, List<(string Division, string Name, string? Role, DateTime LastSeenAt)>> confirmedPeopleByDivision)
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

        var confirmedPeople = confirmedPeopleByDivision.TryGetValue(division, out var people)
            ? people
            : [];

        var reportPeople = BuildPeople(messages, confirmedPeople);

        return new DivisionReportGroupModel(
            Division: division,
            Frequencies: frequencies,
            UnknownMentionsCount: unknownMentionsCount,
            UnknownGroupsCount: unknownGroupsCount,
            People: reportPeople);
    }

    private static List<DivisionReportPersonRowModel> BuildPeople(
        List<InterceptionMessage> messages,
        List<(string Division, string Name, string? Role, DateTime LastSeenAt)> confirmedPeople)
    {
        var people = messages
            .SelectMany(message => message.Participants
                .Where(participant =>
                    !participant.IsUnknown &&
                    !string.IsNullOrWhiteSpace(participant.Name))
                .Select(participant => (
                    Name: participant.Name!,
                    participant.Role,
                    LastSeenAt: message.ObservedDate)))
            .ToList();

        people.AddRange(confirmedPeople.Select(x => (
            x.Name,
            x.Role,
            x.LastSeenAt)));

        var rows = people
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(x => x.LastSeenAt)
                    .ToList();

                var (Name, Role, LastSeenAt) = ordered.First();

                var lastNonEmptyRole = ordered
                    .Select(x => x.Role)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                var name = Name;

                return new DivisionReportPersonRowModel(
                    PersonKey: name.Trim().ToUpperInvariant(),
                    Name: name,
                    Role: lastNonEmptyRole,
                    LastSeenAt: LastSeenAt);
            })
            .OrderBy(x => x.Name)
            .ToList();

        return rows;
    }

    /// <summary>
    /// Завантажує підтверджених осіб, які мають зв'язок з повідомленнями вибраного звіту.
    /// </summary>
    private static async Task<List<(string Division, string Name, string? Role, DateTime LastSeenAt)>> LoadConfirmedPeopleAsync(
        AppDbContext db,
        HashSet<Guid> messageIds,
        Dictionary<Guid, DateTime> messageDates,
        Dictionary<Guid, string> messageDivisions,
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
            .Where(x => x.ResolvedParticipantId.HasValue)
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
            if (!group.ResolvedParticipantId.HasValue)
                continue;

            if (!resolvedParticipants.TryGetValue(group.ResolvedParticipantId.Value, out var resolvedParticipant))
                continue;

            var lastSeenAt = group.ParticipantRefs
                .Where(x => messageDates.ContainsKey(x.MessageId))
                .Select(x => messageDates[x.MessageId])
                .DefaultIfEmpty()
                .Max();

            if (lastSeenAt == default)
                continue;

            var division = group.SuggestedDivision;

            if (string.IsNullOrWhiteSpace(division))
            {
                division = group.ParticipantRefs
                    .Select(x => messageDivisions.TryGetValue(x.MessageId, out var messageDivision) ? messageDivision : null)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            }

            if (string.IsNullOrWhiteSpace(division))
                division = resolvedParticipant.Division;

            if (string.IsNullOrWhiteSpace(division))
                continue;

            people.Add((
                Division: division,
                resolvedParticipant.Name,
                Role: resolvedParticipant.Role ?? group.SuggestedRole,
                LastSeenAt: lastSeenAt));
        }

        return people;
    }
}
