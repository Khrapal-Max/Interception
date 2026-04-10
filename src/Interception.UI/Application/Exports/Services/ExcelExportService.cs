//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using ClosedXML.Excel;
using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Exports.Abstractions;
using Interception.UI.Application.Exports.Dtos;
using Interception.UI.Application.Exports.Models;
using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Reports.Abstractions;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Exports.Services;

/// <summary>
/// Центральний Excel-експорт для реєстрів, звітів і аналітики.
/// Один тип експорту = один лист, без технічних Id у користувацькому файлі.
/// </summary>
public sealed class ExcelExportService(
    IDbContextFactory<AppDbContext> dbFactory,
    IFrequencyDivisionService frequencyDivisionService,
    IPersonRegistryService personRegistryService,
    IDivisionReportService divisionReportService,
    IDayPictureService dayPictureService,
    ILinkMapService linkMapService)
    : IExcelExportService
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string UnknownDivision = "НВ підрозділ";
    private const string UnknownGroup = "Невідомо";

    /// <inheritdoc />
    public async Task<ExportFileDto> ExportAsync(ExportRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var workbook = new XLWorkbook();

        var fileName = request.Kind switch
        {
            ExportKind.Interceptions => await BuildInterceptionsWorkbookAsync(workbook, request, ct),
            ExportKind.FrequencyDivisionRegistry => await BuildFrequencyRegistryWorkbookAsync(workbook, ct),
            ExportKind.PersonsRegistry => await BuildPersonsRegistryWorkbookAsync(workbook, ct),
            ExportKind.DivisionReport => await BuildDivisionReportWorkbookAsync(workbook, request, ct),
            ExportKind.DayPicture => await BuildDayPictureWorkbookAsync(workbook, request, ct),
            ExportKind.LinkMap => await BuildLinkMapWorkbookAsync(workbook, request, ct),
            ExportKind.FrequencyWeights => await BuildFrequencyWeightsWorkbookAsync(workbook, ct),
            _ => throw new NotSupportedException($"Непідтримуваний тип експорту '{request.Kind}'.")
        };

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ExportFileDto(fileName, ExcelContentType, stream.ToArray());
    }

    private async Task<string> BuildInterceptionsWorkbookAsync(XLWorkbook workbook, ExportRequestDto request, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var (dateFrom, dateToExclusive) = NormalizeDateRange(request.DateFrom, request.DateTo);

        var query = db.InterceptionMessages
            .AsNoTracking()
            .Include(x => x.InterceptionAction)
            .Include(x => x.Participants)
            .Include(x => x.Labels)
            .AsQueryable();

        if (dateFrom.HasValue)
            query = query.Where(x => x.ObservedDate >= dateFrom.Value);

        if (dateToExclusive.HasValue)
            query = query.Where(x => x.ObservedDate < dateToExclusive.Value);

        var messages = await query
            .OrderByDescending(x => x.ObservedDate)
            .ToListAsync(ct);

        var sheet = workbook.Worksheets.Add("Спостереження");
        WriteHeader(sheet,
            "Дата/час", "Частота", "Підрозділ", "Вектор", "Точка",
            "Дія", "Примітка", "Учасники", "Кількість учасників", "Мітки", "Кількість міток",
            "Створив", "Створено", "Оновлено");

        var row = 2;
        foreach (var message in messages)
        {
            sheet.Cell(row, 1).Value = message.ObservedDate;
            sheet.Cell(row, 2).Value = message.Frequency;
            sheet.Cell(row, 3).Value = message.Division;
            sheet.Cell(row, 4).Value = message.VectorSignal;
            sheet.Cell(row, 5).Value = message.PointSignal;
            sheet.Cell(row, 6).Value = message.InterceptionAction?.Name;
            sheet.Cell(row, 7).Value = message.Note;
            sheet.Cell(row, 8).Value = BuildParticipantsText(message);
            sheet.Cell(row, 9).Value = message.Participants.Count;
            sheet.Cell(row, 10).Value = string.Join(", ", message.Labels
                .OrderBy(x => x.NameLabel, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.NameLabel));
            sheet.Cell(row, 11).Value = message.Labels.Count;
            sheet.Cell(row, 12).Value = message.CreatedBy;
            sheet.Cell(row, 13).Value = message.CreatedAt;
            sheet.Cell(row, 14).Value = message.UpdatedAt;
            row++;
        }

        ApplySheetStyle(sheet);
        return BuildFileName("sposterezhennia");
    }

    private async Task<string> BuildFrequencyRegistryWorkbookAsync(XLWorkbook workbook, CancellationToken ct)
    {
        var items = await frequencyDivisionService.GetAllAsync(ct);

        var sheet = workbook.Worksheets.Add("Частоти");
        WriteHeader(sheet, "Частота", "Підрозділ");

        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.Frequency;
            sheet.Cell(row, 2).Value = item.Division;
            row++;
        }

        ApplySheetStyle(sheet);
        return BuildFileName("registry_frequency_division");
    }

    private async Task<string> BuildPersonsRegistryWorkbookAsync(XLWorkbook workbook, CancellationToken ct)
    {
        var items = await personRegistryService.GetAllAsync(ct);

        var sheet = workbook.Worksheets.Add("Особи");
        WriteHeader(sheet,
            "Ім'я", "Роль", "Підрозділ", "Підтверджено", "Підтвердив", "Підтверджено о");

        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.Name;
            sheet.Cell(row, 2).Value = item.Role;
            sheet.Cell(row, 3).Value = item.Division;
            sheet.Cell(row, 4).Value = item.IsConfirmed ? "Так" : "Ні";
            sheet.Cell(row, 5).Value = item.ConfirmedBy;
            sheet.Cell(row, 6).Value = item.ConfirmedAt;
            row++;
        }

        ApplySheetStyle(sheet);
        return BuildFileName("registry_persons");
    }

    private async Task<string> BuildDivisionReportWorkbookAsync(XLWorkbook workbook, ExportRequestDto request, CancellationToken ct)
    {
        var (dateFrom, dateToExclusive) = NormalizeDateRange(request.DateFrom, request.DateTo);
        var report = await divisionReportService.BuildAsync(dateFrom, dateToExclusive, ct);

        var sheet = workbook.Worksheets.Add("Звіт підрозділів");
        WriteHeader(sheet,
            "Підрозділ", "Частоти", "Unknown mentions", "Unknown groups", "Кількість відомих осіб",
            "Особа", "Роль", "Остання поява");

        var row = 2;
        foreach (var group in report.Groups.OrderBy(x => x.Division, StringComparer.OrdinalIgnoreCase))
        {
            var people = group.People.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
            if (people.Count == 0)
            {
                sheet.Cell(row, 1).Value = group.Division;
                sheet.Cell(row, 2).Value = string.Join(", ", group.Frequencies);
                sheet.Cell(row, 3).Value = group.UnknownMentionsCount;
                sheet.Cell(row, 4).Value = group.UnknownGroupsCount;
                sheet.Cell(row, 5).Value = group.People.Count;
                row++;
                continue;
            }

            foreach (var person in people)
            {
                sheet.Cell(row, 1).Value = group.Division;
                sheet.Cell(row, 2).Value = string.Join(", ", group.Frequencies);
                sheet.Cell(row, 3).Value = group.UnknownMentionsCount;
                sheet.Cell(row, 4).Value = group.UnknownGroupsCount;
                sheet.Cell(row, 5).Value = group.People.Count;
                sheet.Cell(row, 6).Value = person.Name;
                sheet.Cell(row, 7).Value = person.Role;
                sheet.Cell(row, 8).Value = person.LastSeenAt;
                row++;
            }
        }

        ApplySheetStyle(sheet);
        return BuildFileName("report_divisions");
    }

    private async Task<string> BuildDayPictureWorkbookAsync(XLWorkbook workbook, ExportRequestDto request, CancellationToken ct)
    {
        var day = request.Day ?? DateTime.Today;
        var picture = await dayPictureService.BuildAsync(day, ct);

        var sheet = workbook.Worksheets.Add("Картина дня");
        WriteHeader(sheet,
            "День", "Підрозділ", "Початок бесіди", "Кінець бесіди", "Дата/час", "Частота",
            "Підрозділ повідомлення", "Вектор", "Дія", "Примітка", "Учасники", "Кількість повідомлень у лінії");

        var row = 2;
        foreach (var group in picture.Groups.OrderBy(x => x.Division, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var conversation in group.Conversations.OrderBy(x => x.StartedAtUtc))
            {
                foreach (var entry in conversation.Entries.OrderBy(x => x.ObservedDate))
                {
                    sheet.Cell(row, 1).Value = picture.Day.ToString("yyyy-MM-dd");
                    sheet.Cell(row, 2).Value = group.Division;
                    sheet.Cell(row, 3).Value = conversation.StartedAtUtc;
                    sheet.Cell(row, 4).Value = conversation.EndedAtUtc;
                    sheet.Cell(row, 5).Value = entry.ObservedDate;
                    sheet.Cell(row, 6).Value = entry.Frequency;
                    sheet.Cell(row, 7).Value = entry.Division;
                    sheet.Cell(row, 8).Value = entry.VectorSignal;
                    sheet.Cell(row, 9).Value = entry.ActionName;
                    sheet.Cell(row, 10).Value = entry.Note;
                    sheet.Cell(row, 11).Value = string.Join(", ", entry.Participants);
                    sheet.Cell(row, 12).Value = group.MessageCount;
                    row++;
                }
            }
        }

        ApplySheetStyle(sheet);
        return BuildFileName($"day_picture_{day:yyyyMMdd}");
    }

    private async Task<string> BuildLinkMapWorkbookAsync(XLWorkbook workbook, ExportRequestDto request, CancellationToken ct)
    {
        var (dateFrom, dateToExclusive) = NormalizeDateRange(request.DateFrom, request.DateTo);
        var map = await linkMapService.BuildAsync(dateFrom, dateToExclusive, ct);

        var sheet = workbook.Worksheets.Add("Карта зв'язків");
        WriteHeader(sheet,
            "Підрозділ", "Частоти", "Ключова особа", "Роль", "Members", "MentionCount",
            "InternalConnectionWeight", "BridgeWeight", "PrimaryAction", "TopActions",
            "Цільовий підрозділ", "Контактна особа", "Частота мосту", "Вага мосту", "Дія мосту");

        var row = 2;
        foreach (var group in map.Groups)
        {
            var bridges = group.Bridges.OrderByDescending(x => x.Weight).ThenBy(x => x.TargetDivision).ToList();
            if (bridges.Count == 0)
            {
                sheet.Cell(row, 1).Value = group.Division;
                sheet.Cell(row, 2).Value = string.Join(", ", group.Frequencies);
                sheet.Cell(row, 3).Value = group.KeyPersonName;
                sheet.Cell(row, 4).Value = group.KeyPersonRole;
                sheet.Cell(row, 5).Value = string.Join(", ", group.Members);
                sheet.Cell(row, 6).Value = group.MentionCount;
                sheet.Cell(row, 7).Value = group.InternalConnectionWeight;
                sheet.Cell(row, 8).Value = group.BridgeWeight;
                sheet.Cell(row, 9).Value = group.PrimaryAction;
                sheet.Cell(row, 10).Value = string.Join(", ", group.TopActions);
                row++;
                continue;
            }

            foreach (var bridge in bridges)
            {
                sheet.Cell(row, 1).Value = group.Division;
                sheet.Cell(row, 2).Value = string.Join(", ", group.Frequencies);
                sheet.Cell(row, 3).Value = group.KeyPersonName;
                sheet.Cell(row, 4).Value = group.KeyPersonRole;
                sheet.Cell(row, 5).Value = string.Join(", ", group.Members);
                sheet.Cell(row, 6).Value = group.MentionCount;
                sheet.Cell(row, 7).Value = group.InternalConnectionWeight;
                sheet.Cell(row, 8).Value = group.BridgeWeight;
                sheet.Cell(row, 9).Value = group.PrimaryAction;
                sheet.Cell(row, 10).Value = string.Join(", ", group.TopActions);
                sheet.Cell(row, 11).Value = bridge.TargetDivision;
                sheet.Cell(row, 12).Value = bridge.ContactPersonName;
                sheet.Cell(row, 13).Value = bridge.BridgeFrequency;
                sheet.Cell(row, 14).Value = bridge.Weight;
                sheet.Cell(row, 15).Value = bridge.PrimaryAction;
                row++;
            }
        }

        ApplySheetStyle(sheet);
        return BuildFileName("analytics_link_map");
    }

    private async Task<string> BuildFrequencyWeightsWorkbookAsync(XLWorkbook workbook, CancellationToken ct)
    {
        var rows = await BuildFrequencyWeightRowsAsync(ct);

        var sheet = workbook.Worksheets.Add("Вага підрозділів");
        WriteHeader(sheet,
            "Частота", "Підрозділ по частоті", "Загальна кількість осіб", "Група підрозділу", "Кількість осіб", "Вага, %");

        var row = 2;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.Frequency;
            sheet.Cell(row, 2).Value = item.FrequencyDivision;
            sheet.Cell(row, 3).Value = item.TotalPersons;
            sheet.Cell(row, 4).Value = item.GroupName;
            sheet.Cell(row, 5).Value = item.PersonsCount;
            sheet.Cell(row, 6).Value = item.WeightPercent;
            row++;
        }

        ApplySheetStyle(sheet);
        return BuildFileName("analytics_frequency_weights");
    }

    private async Task<List<FrequencyWeightExportRow>> BuildFrequencyWeightRowsAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var messages = await db.InterceptionMessages
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.Frequency))
            .Select(x => new FrequencyMessageRow(x.Id, x.Frequency!, x.Division, x.ObservedDate))
            .ToListAsync(ct);

        if (messages.Count == 0)
            return [];

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
            .Select(x => new FrequencyResolvedRow(x.Id, x.Name, x.Division))
            .ToListAsync(ct);

        var resolvedMap = resolvedParticipants.ToDictionary(x => x.Id);
        var participantToResolvedMap = BuildParticipantToResolvedMap(candidateGroups);
        var resolvedByNameMap = BuildResolvedByNameMap(resolvedParticipants);
        var participantsByMessageId = participants
            .GroupBy(x => x.MessageId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var participantsById = participants.ToDictionary(x => x.Id);
        var participantToGroupMap = BuildParticipantToGroupMap(candidateGroups);
        var groupDivisionMap = BuildGroupDivisionMap(candidateGroups, participantsById, participantToResolvedMap, resolvedMap, resolvedByNameMap);

        var rows = new List<FrequencyWeightExportRow>();

        foreach (var frequencyGroup in messages.GroupBy(x => x.Frequency, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var frequencyDivision = frequencyGroup
                .Select(x => NormalizeKnownDivision(x.Division))
                .Where(x => x is not null)
                .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Key)
                .FirstOrDefault();

            var persons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var message in frequencyGroup)
            {
                if (!participantsByMessageId.TryGetValue(message.Id, out var messageParticipants))
                    continue;

                foreach (var participant in messageParticipants)
                {
                    var personKey = BuildPersonKey(participant, participantToGroupMap, participantToResolvedMap, resolvedMap, resolvedByNameMap);
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
            var grouped = persons
                .GroupBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .Select(x => new
                {
                    GroupName = x.Key,
                    PersonsCount = x.Count(),
                    WeightPercent = totalPersons == 0 ? 0m : Math.Round(x.Count() * 100m / totalPersons, 2)
                })
                .OrderByDescending(x => x.WeightPercent)
                .ThenByDescending(x => x.PersonsCount)
                .ThenBy(x => x.GroupName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var item in grouped)
            {
                rows.Add(new FrequencyWeightExportRow(
                    frequencyGroup.Key,
                    frequencyDivision,
                    totalPersons,
                    item.GroupName,
                    item.PersonsCount,
                    item.WeightPercent));
            }
        }

        return rows;
    }

    private static Dictionary<Guid, Guid> BuildParticipantToResolvedMap(IReadOnlyList<ParticipantCandidateGroup> candidateGroups)
    {
        var map = new Dictionary<Guid, Guid>();

        foreach (var group in candidateGroups)
        {
            if (!group.ResolvedParticipantId.HasValue)
                continue;

            foreach (var participantRef in group.ParticipantRefs)
                map.TryAdd(participantRef.ParticipantId, group.ResolvedParticipantId.Value);
        }

        return map;
    }

    private static Dictionary<string, Guid> BuildResolvedByNameMap(IReadOnlyList<FrequencyResolvedRow> resolvedParticipants)
    {
        return resolvedParticipants
            .Select(x => new { x.Id, NormalizedName = StringTextNormExtensions.NormalizeOption(x.Name) })
            .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedName))
            .GroupBy(x => x.NormalizedName!, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<Guid, Guid> BuildParticipantToGroupMap(IReadOnlyList<ParticipantCandidateGroup> candidateGroups)
    {
        return candidateGroups
            .SelectMany(group => group.ParticipantRefs.Select(reference => new { reference.ParticipantId, group.Id }))
            .GroupBy(x => x.ParticipantId)
            .Where(x => x.Select(v => v.Id).Distinct().Count() == 1)
            .ToDictionary(x => x.Key, x => x.First().Id);
    }

    private static Dictionary<Guid, string?> BuildGroupDivisionMap(
        IReadOnlyList<ParticipantCandidateGroup> candidateGroups,
        IReadOnlyDictionary<Guid, FrequencyParticipantRow> participantsById,
        IReadOnlyDictionary<Guid, Guid> participantToResolvedMap,
        IReadOnlyDictionary<Guid, FrequencyResolvedRow> resolvedMap,
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

            foreach (var reference in group.ParticipantRefs)
            {
                if (!participantsById.TryGetValue(reference.ParticipantId, out var participant))
                    continue;

                var division = ResolveDivisionForGroupMember(participant, participantToResolvedMap, resolvedMap, resolvedByNameMap);
                if (!string.IsNullOrWhiteSpace(division))
                    divisions.Add(division);
            }

            map[group.Id] = divisions.Count == 1 ? divisions.First() : null;
        }

        return map;
    }

    private static string BuildPersonKey(
        FrequencyParticipantRow participant,
        IReadOnlyDictionary<Guid, Guid> participantToGroupMap,
        IReadOnlyDictionary<Guid, Guid> participantToResolvedMap,
        IReadOnlyDictionary<Guid, FrequencyResolvedRow> resolvedMap,
        IReadOnlyDictionary<string, Guid> resolvedByNameMap)
    {
        if (participantToGroupMap.TryGetValue(participant.Id, out var groupId))
            return $"group:{groupId}";

        if (participantToResolvedMap.TryGetValue(participant.Id, out var resolvedId) && resolvedMap.ContainsKey(resolvedId))
            return $"resolved:{resolvedId}";

        var normalizedName = StringTextNormExtensions.NormalizeOption(participant.Name);
        if (!string.IsNullOrWhiteSpace(normalizedName)
            && resolvedByNameMap.TryGetValue(normalizedName, out var resolvedIdByName)
            && resolvedMap.ContainsKey(resolvedIdByName))
        {
            return $"resolved:{resolvedIdByName}";
        }

        if (!string.IsNullOrWhiteSpace(normalizedName))
            return participant.IsUnknown ? $"unknown:{normalizedName}" : $"known:{normalizedName}";

        return $"unknown-participant:{participant.Id}";
    }

    private static string BuildGroupName(
        FrequencyParticipantRow participant,
        IReadOnlyDictionary<Guid, Guid> participantToGroupMap,
        IReadOnlyDictionary<Guid, string?> groupDivisionMap,
        IReadOnlyDictionary<Guid, Guid> participantToResolvedMap,
        IReadOnlyDictionary<Guid, FrequencyResolvedRow> resolvedMap,
        IReadOnlyDictionary<string, Guid> resolvedByNameMap)
    {
        if (participantToGroupMap.TryGetValue(participant.Id, out var groupId)
            && groupDivisionMap.TryGetValue(groupId, out var groupDivision)
            && !string.IsNullOrWhiteSpace(groupDivision))
        {
            return groupDivision;
        }

        var resolvedDivision = ResolveDivisionForGroupMember(participant, participantToResolvedMap, resolvedMap, resolvedByNameMap);
        if (!string.IsNullOrWhiteSpace(resolvedDivision))
            return resolvedDivision;

        var messageDivision = NormalizeKnownDivision(participant.MessageDivision);
        if (!string.IsNullOrWhiteSpace(messageDivision))
            return messageDivision;

        return UnknownGroup;
    }

    private static string? ResolveDivisionForGroupMember(
        FrequencyParticipantRow participant,
        IReadOnlyDictionary<Guid, Guid> participantToResolvedMap,
        IReadOnlyDictionary<Guid, FrequencyResolvedRow> resolvedMap,
        IReadOnlyDictionary<string, Guid> resolvedByNameMap)
    {
        if (participantToResolvedMap.TryGetValue(participant.Id, out var resolvedId)
            && resolvedMap.TryGetValue(resolvedId, out var resolvedParticipant))
        {
            return NormalizeKnownDivision(resolvedParticipant.Division);
        }

        var normalizedName = StringTextNormExtensions.NormalizeOption(participant.Name);
        if (!string.IsNullOrWhiteSpace(normalizedName)
            && resolvedByNameMap.TryGetValue(normalizedName, out var resolvedIdByName)
            && resolvedMap.TryGetValue(resolvedIdByName, out var resolvedByName))
        {
            return NormalizeKnownDivision(resolvedByName.Division);
        }

        return null;
    }

    private static async Task<Dictionary<Guid, ParticipantOverlay>> BuildParticipantOverlayMapAsync(
        AppDbContext db,
        IReadOnlyList<InterceptionMessage> messages,
        CancellationToken ct)
    {
        var unknownIds = messages
            .SelectMany(x => x.Participants.Where(p => p.IsUnknown).Select(p => p.Id))
            .ToHashSet();

        var result = new Dictionary<Guid, ParticipantOverlay>();
        if (unknownIds.Count == 0)
            return result;

        var confirmedGroups = await db.ParticipantCandidateGroups
            .Include(x => x.ParticipantRefs)
            .Where(x => x.Status == CandidateGroupStatus.Confirmed && x.ResolvedParticipantId != null)
            .AsNoTracking()
            .ToListAsync(ct);

        if (confirmedGroups.Count > 0)
        {
            var resolvedIds = confirmedGroups
                .Select(x => x.ResolvedParticipantId!.Value)
                .Distinct()
                .ToList();

            var resolvedNames = await db.ResolvedParticipants
                .Where(x => resolvedIds.Contains(x.Id))
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

            foreach (var group in confirmedGroups)
            {
                if (!group.ResolvedParticipantId.HasValue || !resolvedNames.TryGetValue(group.ResolvedParticipantId.Value, out var resolvedName))
                    continue;

                foreach (var reference in group.ParticipantRefs.Where(x => unknownIds.Contains(x.ParticipantId)))
                    result.TryAdd(reference.ParticipantId, new ParticipantOverlay(resolvedName, "confirmed"));
            }
        }

        var openGroups = await db.ParticipantCandidateGroups
            .Include(x => x.ParticipantRefs)
            .Where(x => x.Status == CandidateGroupStatus.Open && x.SuggestedName != null)
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var group in openGroups)
        {
            foreach (var reference in group.ParticipantRefs.Where(x => unknownIds.Contains(x.ParticipantId)))
                result.TryAdd(reference.ParticipantId, new ParticipantOverlay(group.SuggestedName, "open"));
        }

        return result;
    }

    private static string BuildParticipantsText(InterceptionMessage message)
    {
        return string.Join(", ", message.Participants
            .OrderBy(x => x.Ordinal)
            .Select(x =>
            {
                var name = x.Name;

                if (string.IsNullOrWhiteSpace(name))
                    name = x.IsUnknown ? $"НВ {x.Ordinal}" : "—";

                if (!string.IsNullOrWhiteSpace(x.Role))
                    return $"{name} ({x.Role})";

                return name;
            }));
    }

    private static (DateTime? DateFrom, DateTime? DateToExclusive) NormalizeDateRange(DateTime? dateFrom, DateTime? dateTo)
    {
        var normalizedFrom = dateFrom;
        DateTime? normalizedToExclusive = null;

        if (dateTo.HasValue)
        {
            normalizedToExclusive = dateTo.Value.TimeOfDay == TimeSpan.Zero
                ? dateTo.Value.Date.AddDays(1)
                : dateTo.Value;
        }

        return (normalizedFrom, normalizedToExclusive);
    }

    private static string BuildFileName(string prefix)
        => $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

    private static void WriteHeader(IXLWorksheet sheet, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
    }

    private static void ApplySheetStyle(IXLWorksheet sheet)
    {
        var usedRange = sheet.RangeUsed();
        if (usedRange is null)
            return;

        var header = sheet.Row(1);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E2F3");
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        usedRange.Style.Alignment.WrapText = false;
        usedRange.SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        foreach (var column in sheet.ColumnsUsed())
        {
            if (column.Width > 80)
                column.Width = 80;
        }
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

    private sealed record ParticipantOverlay(string? Name, string State);
    private sealed record FrequencyMessageRow(Guid Id, string Frequency, string? Division, DateTime ObservedDate);
    private sealed record FrequencyParticipantRow(Guid Id, Guid MessageId, string? Name, bool IsUnknown, string? MessageDivision);
    private sealed record FrequencyResolvedRow(Guid Id, string Name, string? Division);
    private sealed record FrequencyWeightExportRow(string Frequency, string? FrequencyDivision, int TotalPersons, string GroupName, int PersonsCount, decimal WeightPercent);
}
