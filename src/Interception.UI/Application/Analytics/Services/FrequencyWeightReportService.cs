//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Builders.Frequency;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Простий ваговий звіт по частотах.
/// Для кожної частоти рахує частку груп підрозділів серед унікальних осіб на цій частоті.
/// </summary>
public sealed partial class FrequencyWeightReportService(IDbContextFactory<AppDbContext> dbFactory)
    : IFrequencyWeightReportService
{
    private const string UnknownGroup = "Невідомо";
    private const string UnknownDivision = "НВ підрозділ";

    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<FrequencyWeightReportDto> BuildAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var messages = await db.InterceptionMessages
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.Frequency))
            .Select(x => new FrequencyMessageRow(
                x.Id,
                x.Frequency!,
                x.Division,
                x.ObservedDate))
            .ToListAsync(ct);

        if (messages.Count == 0)
            return new FrequencyWeightReportDto([]);

        var participants = await db.InterceptionMessageParticipants
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.InterceptionMessage.Frequency))
            .Select(x => new FrequencyParticipantRow(
                x.Id,
                x.InterceptionMessageId,
                x.Name,
                x.IsUnknown,
                x.InterceptionMessage.Division))
            .ToListAsync(ct);

        var candidateGroups = await db.ParticipantCandidateGroups
            .Include(x => x.ParticipantRefs)
            .AsNoTracking()
            .ToListAsync(ct);

        var resolvedParticipantIds = candidateGroups
            .Where(x => x.ResolvedParticipantId.HasValue)
            .Select(x => x.ResolvedParticipantId!.Value)
            .Distinct()
            .ToList();

        var resolvedParticipants = await db.ResolvedParticipants
            .AsNoTracking()
            .Where(x => resolvedParticipantIds.Contains(x.Id))
            .Select(x => new ResolvedRow(x.Id, x.Name, x.Division))
            .ToListAsync(ct);

        var resolvedMap = resolvedParticipants.ToDictionary(x => x.Id);
        var participantToResolvedMap = BuildParticipantToResolvedMap(candidateGroups);
        var resolvedByNameMap = BuildResolvedByNameMap(resolvedParticipants);
        var participantsByMessageId = participants
            .GroupBy(x => x.MessageId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var participantsById = participants.ToDictionary(x => x.Id);
        var participantToGroupMap = BuildParticipantToGroupMap(candidateGroups);
        var groupDivisionMap = BuildGroupDivisionMap(
            candidateGroups,
            participantsById,
            participantToResolvedMap,
            resolvedMap,
            resolvedByNameMap);

        var frequencies = messages
            .GroupBy(x => x.Frequency, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildFrequency(
                group.Key,
                [.. group],
                participantsByMessageId,
                participantToGroupMap,
                groupDivisionMap,
                participantToResolvedMap,
                resolvedMap,
                resolvedByNameMap))
            .OrderBy(x => x.Frequency, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FrequencyWeightReportDto(frequencies);
    }

    private static FrequencyWeightDto BuildFrequency(
        string frequency,
        IReadOnlyList<FrequencyMessageRow> messages,
        IReadOnlyDictionary<Guid, List<FrequencyParticipantRow>> participantsByMessageId,
        IReadOnlyDictionary<Guid, Guid> participantToGroupMap,
        IReadOnlyDictionary<Guid, string?> groupDivisionMap,
        IReadOnlyDictionary<Guid, Guid> participantToResolvedMap,
        IReadOnlyDictionary<Guid, ResolvedRow> resolvedMap,
        IReadOnlyDictionary<string, Guid> resolvedByNameMap)
    {
        var frequencyDivision = messages
            .Select(x => NormalizeKnownDivision(x.Division))
            .Where(x => x is not null)
            .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Key)
            .FirstOrDefault();

        var persons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var message in messages)
        {
            if (!participantsByMessageId.TryGetValue(message.Id, out var messageParticipants))
                continue;

            foreach (var participant in messageParticipants)
            {
                var personKey = BuildPersonKey(
                    participant,
                    participantToGroupMap,
                    participantToResolvedMap,
                    resolvedMap,
                    resolvedByNameMap);

                if (persons.ContainsKey(personKey))
                    continue;

                persons[personKey] = BuildGroupName(
                    participant,
                    participantToGroupMap,
                    groupDivisionMap,
                    participantToResolvedMap,
                    resolvedMap,
                    resolvedByNameMap);
            }
        }

        var totalPersons = persons.Count;

        var groups = persons
            .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FrequencyWeightGroupDto(
                group.Key,
                group.Count(),
                totalPersons == 0 ? 0m : Math.Round(group.Count() * 100m / totalPersons, 2)))
            .OrderByDescending(x => x.WeightPercent)
            .ThenByDescending(x => x.PersonsCount)
            .ThenBy(x => x.GroupName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FrequencyWeightDto(
            frequency,
            frequencyDivision,
            totalPersons,
            groups);
    }

    private static Dictionary<Guid, Guid> BuildParticipantToResolvedMap(IReadOnlyList<ParticipantCandidateGroup> candidateGroups)
    {
        var map = new Dictionary<Guid, Guid>();

        foreach (var group in candidateGroups)
        {
            if (!group.ResolvedParticipantId.HasValue)
                continue;

            foreach (var participantRef in group.ParticipantRefs)
            {
                map.TryAdd(participantRef.ParticipantId, group.ResolvedParticipantId.Value);
            }
        }

        return map;
    }

    private static Dictionary<string, Guid> BuildResolvedByNameMap(IReadOnlyList<ResolvedRow> resolvedParticipants)
    {
        return resolvedParticipants
            .Select(x => new
            {
                x.Id,
                NormalizedName = StringTextNormExtensions.NormalizeOption(x.Name)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedName))
            .GroupBy(x => x.NormalizedName!, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<Guid, Guid> BuildParticipantToGroupMap(IReadOnlyList<ParticipantCandidateGroup> candidateGroups)
    {
        return candidateGroups
            .SelectMany(group => group.ParticipantRefs.Select(participantRef => new
            {
                participantRef.ParticipantId,
                GroupId = group.Id
            }))
            .GroupBy(x => x.ParticipantId)
            .Where(x => x.Select(v => v.GroupId).Distinct().Count() == 1)
            .ToDictionary(x => x.Key, x => x.First().GroupId);
    }

    private static Dictionary<Guid, string?> BuildGroupDivisionMap(
        IReadOnlyList<ParticipantCandidateGroup> candidateGroups,
        IReadOnlyDictionary<Guid, FrequencyParticipantRow> participantsById,
        IReadOnlyDictionary<Guid, Guid> participantToResolvedMap,
        IReadOnlyDictionary<Guid, ResolvedRow> resolvedMap,
        IReadOnlyDictionary<string, Guid> resolvedByNameMap)
    {
        var map = new Dictionary<Guid, string?>();

        foreach (var group in candidateGroups)
        {
            var divisions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (group.ResolvedParticipantId.HasValue && resolvedMap.TryGetValue(group.ResolvedParticipantId.Value, out var resolvedGroupParticipant))
            {
                var resolvedDivision = NormalizeKnownDivision(resolvedGroupParticipant.Division);
                if (!string.IsNullOrWhiteSpace(resolvedDivision))
                    divisions.Add(resolvedDivision);
            }

            foreach (var participantRef in group.ParticipantRefs)
            {
                if (!participantsById.TryGetValue(participantRef.ParticipantId, out var participant))
                    continue;

                var division = ResolveDivisionForGroupMember(
                    participant,
                    participantToResolvedMap,
                    resolvedMap,
                    resolvedByNameMap);

                if (!string.IsNullOrWhiteSpace(division))
                    divisions.Add(division);
            }

            map[group.Id] = divisions.Count == 1
                ? divisions.First()
                : null;
        }

        return map;
    }

    private static string? ResolveDivisionForGroupMember(
        FrequencyParticipantRow participant,
        IReadOnlyDictionary<Guid, Guid> participantToResolvedMap,
        IReadOnlyDictionary<Guid, ResolvedRow> resolvedMap,
        IReadOnlyDictionary<string, Guid> resolvedByNameMap)
    {
        if (participantToResolvedMap.TryGetValue(participant.Id, out var directResolvedId)
            && resolvedMap.TryGetValue(directResolvedId, out var directResolvedParticipant))
        {
            var confirmedDivision = NormalizeKnownDivision(directResolvedParticipant.Division);
            if (!string.IsNullOrWhiteSpace(confirmedDivision))
                return confirmedDivision;
        }

        var normalizedName = StringTextNormExtensions.NormalizeOption(participant.Name);
        if (!string.IsNullOrWhiteSpace(normalizedName)
            && resolvedByNameMap.TryGetValue(normalizedName, out var resolvedIdByName)
            && resolvedMap.TryGetValue(resolvedIdByName, out var resolvedByNameParticipant))
        {
            var confirmedDivision = NormalizeKnownDivision(resolvedByNameParticipant.Division);
            if (!string.IsNullOrWhiteSpace(confirmedDivision))
                return confirmedDivision;
        }

        return NormalizeKnownDivision(participant.MessageDivision);
    }

    private static string BuildPersonKey(
        FrequencyParticipantRow participant,
        IReadOnlyDictionary<Guid, Guid> participantToGroupMap,
        IReadOnlyDictionary<Guid, Guid> participantToResolvedMap,
        IReadOnlyDictionary<Guid, ResolvedRow> resolvedMap,
        IReadOnlyDictionary<string, Guid> resolvedByNameMap)
    {
        if (participantToResolvedMap.TryGetValue(participant.Id, out var directResolvedId)
            && resolvedMap.ContainsKey(directResolvedId))
        {
            return $"resolved:{directResolvedId}";
        }

        if (participantToGroupMap.TryGetValue(participant.Id, out var groupId))
        {
            return $"group:{groupId}";
        }

        var normalizedName = StringTextNormExtensions.NormalizeOption(participant.Name);
        if (!string.IsNullOrWhiteSpace(normalizedName)
            && resolvedByNameMap.TryGetValue(normalizedName, out var resolvedIdByName)
            && resolvedMap.ContainsKey(resolvedIdByName))
        {
            return $"resolved:{resolvedIdByName}";
        }

        if (!string.IsNullOrWhiteSpace(normalizedName))
        {
            return participant.IsUnknown
                ? $"unknown:{normalizedName}"
                : $"known:{normalizedName}";
        }

        return $"unknown-participant:{participant.Id}";
    }

    private static string BuildGroupName(
        FrequencyParticipantRow participant,
        IReadOnlyDictionary<Guid, Guid> participantToGroupMap,
        IReadOnlyDictionary<Guid, string?> groupDivisionMap,
        IReadOnlyDictionary<Guid, Guid> participantToResolvedMap,
        IReadOnlyDictionary<Guid, ResolvedRow> resolvedMap,
        IReadOnlyDictionary<string, Guid> resolvedByNameMap)
    {
        if (participantToResolvedMap.TryGetValue(participant.Id, out var directResolvedId)
            && resolvedMap.TryGetValue(directResolvedId, out var directResolvedParticipant))
        {
            var confirmedDivision = NormalizeKnownDivision(directResolvedParticipant.Division);
            if (!string.IsNullOrWhiteSpace(confirmedDivision))
                return confirmedDivision;
        }

        if (participantToGroupMap.TryGetValue(participant.Id, out var groupId))
        {
            if (groupDivisionMap.TryGetValue(groupId, out var groupDivision)
                && !string.IsNullOrWhiteSpace(groupDivision))
            {
                return groupDivision;
            }

            return UnknownGroup;
        }

        var normalizedName = StringTextNormExtensions.NormalizeOption(participant.Name);
        if (!string.IsNullOrWhiteSpace(normalizedName)
            && resolvedByNameMap.TryGetValue(normalizedName, out var resolvedIdByName)
            && resolvedMap.TryGetValue(resolvedIdByName, out var resolvedByNameParticipant))
        {
            var confirmedDivision = NormalizeKnownDivision(resolvedByNameParticipant.Division);
            if (!string.IsNullOrWhiteSpace(confirmedDivision))
                return confirmedDivision;
        }

        var observationDivision = NormalizeKnownDivision(participant.MessageDivision);
        if (!string.IsNullOrWhiteSpace(observationDivision))
            return observationDivision;

        return UnknownGroup;
    }

    private static string? NormalizeKnownDivision(string? division)
    {
        var normalized = SemanticValueExtensions.NormalizeMeaningfulOrNull(division);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return string.Equals(normalized, UnknownDivision, StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }
}
