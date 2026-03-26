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

        var groups = messages
            .GroupBy(x => x.Division!, StringComparer.OrdinalIgnoreCase)
            .Select(x => BuildGroup(x.Key, [.. x], unknownGroupsCountByDivision))
            .OrderBy(x => x.Division)
            .ToList();

        return new DivisionReportModel(groups);
    }

    private static DivisionReportGroupModel BuildGroup(
        string division,
        List<InterceptionMessage> messages,
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

        var people = BuildPeople(messages);

        return new DivisionReportGroupModel(
            Division: division,
            Frequencies: frequencies,
            UnknownMentionsCount: unknownMentionsCount,
            UnknownGroupsCount: unknownGroupsCount,
            People: people);
    }

    private static List<DivisionReportPersonRowModel> BuildPeople(
        List<InterceptionMessage> messages)
    {
        var rows = messages
            .SelectMany(message => message.Participants.Select(participant => new
            {
                Message = message,
                Participant = participant
            }))
            .Where(x =>
                !x.Participant.IsUnknown &&
                !string.IsNullOrWhiteSpace(x.Participant.Name))
            .GroupBy(x => x.Participant.Name!, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(x => x.Message.ObservedDate)
                    .ToList();

                var lastSeen = ordered.First();

                var lastNonEmptyRole = ordered
                    .Select(x => x.Participant.Role)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                var name = lastSeen.Participant.Name!;

                return new DivisionReportPersonRowModel(
                    PersonKey: name.Trim().ToUpperInvariant(),
                    Name: name,
                    Role: lastNonEmptyRole,
                    LastSeenAt: lastSeen.Message.ObservedDate);
            })
            .OrderBy(x => x.Name)
            .ToList();

        return rows;
    }
}