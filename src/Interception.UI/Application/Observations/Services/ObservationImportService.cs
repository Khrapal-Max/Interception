//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos.Import;
using Interception.UI.Application.Observations.Import;
using Interception.UI.Domain.Enums;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Interception.UI.Application.Observations.Services;

/// <summary>
/// Batch import of observations from fixed Excel template or already parsed rows.
/// </summary>
public sealed partial class ObservationImportService(IDbContextFactory<AppDbContext> dbFactory) : IObservationImportService
{
    private const int ObservedDateColumn = 0;
    private const int ActionColumn = 1;
    private const int LayerColumn = 2;
    private const int RmColumn = 3;
    private const int PointColumn = 4;
    private const int LocationColumn = 5;
    private const int DistrictColumn = 6;
    private const int SubdivisionColumn = 7;
    private const int StrengthColumn = 8;
    private const int NoteColumn = 9;
    private const int ParticipantsColumn = 10;
    private const int ExpectedColumnCount = 11;

    private static readonly string[] ExpectedColumns =
    [
        "Час",
        "Дія",
        "Шар",
        "Р/М",
        "Точка",
        "Локація",
        "Район",
        "Підрозділ",
        "Сила",
        "Примітка",
        "Учасники"
    ];

    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<ObservationImportResultDto> ImportAsync(
        IReadOnlyCollection<ObservationImportRowDto> rows,
        string source,
        Guid? sourceFileId,
        string? createdBy,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rows);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var importedCount = 0;
        var duplicateCount = 0;
        var errors = new List<ObservationImportErrorDto>();
        var batchHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows.OrderBy(x => x.RowNumber))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var observation = Domain.Observation.Create(
                    row.ObservedDate,
                    Require(row.ActionRaw, row.RowNumber, "дію"),
                    Clean(row.Layer),
                    Clean(row.RmRaw),
                    Clean(row.PointRaw),
                    Clean(row.LocationRaw),
                    Clean(row.DistrictRaw),
                    Clean(row.SubdivisionRaw),
                    row.SubdivisionStrength,
                    row.SubdivisionSource ?? ObservationSubdivisionSource.Import,
                    Clean(row.Note),
                    source,
                    sourceFileId,
                    row.RowNumber,
                    createdBy);

                foreach (var participant in NormalizeParticipants(row.Participants))
                    observation.AddParticipant(participant.LabelRaw, participant.IsUnknown, participant.RoleRaw);

                if (!batchHashes.Add(observation.ContentHash))
                {
                    duplicateCount++;
                    continue;
                }

                var existsInDb = await db.Observations
                    .AsNoTracking()
                    .AnyAsync(x => x.ContentHash == observation.ContentHash, ct);

                if (existsInDb)
                {
                    duplicateCount++;
                    continue;
                }

                db.Observations.Add(observation);
                importedCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new ObservationImportErrorDto(row.RowNumber, ex.Message));
            }
        }

        if (importedCount > 0)
            await db.SaveChangesAsync(ct);

        return new ObservationImportResultDto(importedCount, duplicateCount, errors.Count, errors);
    }

    public async Task<ObservationImportResultDto> ImportExcelAsync(
        Stream excelStream,
        string source,
        Guid? sourceFileId,
        string? createdBy,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(excelStream);

        await using var buffer = new MemoryStream();
        await excelStream.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var rows = ReadExcelRows(buffer, ct);
        return await ImportAsync(rows, source, sourceFileId, createdBy, ct);
    }

    private static List<ObservationImportRowDto> ReadExcelRows(Stream excelStream, CancellationToken ct)
    {
        using var document = SpreadsheetDocument.Open(excelStream, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("Excel workbook is invalid.");

        var sheet = workbookPart.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault()
                    ?? throw new InvalidOperationException("У файлі Excel не знайдено жодного листа.");

        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
        var sheetData = worksheetPart.Worksheet?.GetFirstChild<SheetData>()
                        ?? throw new InvalidOperationException("У першому листі немає даних.");

        var rows = sheetData.Elements<Row>().ToList();
        if (rows.Count == 0)
            return [];

        var headerValues = ReadRowCells(rows[0], workbookPart);
        ValidateHeader(headerValues);

        var result = new List<ObservationImportRowDto>();

        for (var i = 1; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var row = rows[i];
            var cells = ReadRowCells(row, workbookPart);
            if (IsEmptyDataRow(cells))
                continue;

            var rowNumber = (int)(row.RowIndex?.Value ?? (uint)(i + 1));
            EnsureDataWidth(cells, rowNumber);

            result.Add(new ObservationImportRowDto
            {
                RowNumber = rowNumber,
                ObservedDate = ParseObservedDate(GetCell(cells, ObservedDateColumn), rowNumber),
                ActionRaw = Require(GetCell(cells, ActionColumn), rowNumber, "дію"),
                Layer = Clean(GetCell(cells, LayerColumn)),
                RmRaw = Clean(GetCell(cells, RmColumn)),
                PointRaw = Clean(GetCell(cells, PointColumn)),
                LocationRaw = Clean(GetCell(cells, LocationColumn)),
                DistrictRaw = Clean(GetCell(cells, DistrictColumn)),
                SubdivisionRaw = Clean(GetCell(cells, SubdivisionColumn)),
                SubdivisionStrength = ParseStrength(GetCell(cells, StrengthColumn), rowNumber),
                SubdivisionSource = ObservationSubdivisionSource.Import,
                Note = Clean(GetCell(cells, NoteColumn)),
                Participants = ParseParticipants(GetCell(cells, ParticipantsColumn))
            });
        }

        return result;
    }

    private static List<string> ReadRowCells(Row row, WorkbookPart workbookPart)
    {
        var values = new string[ExpectedColumnCount];

        foreach (var cell in row.Elements<Cell>())
        {
            var index = GetColumnIndex(cell.CellReference?.Value);
            if (index < 0 || index >= ExpectedColumnCount)
                continue;

            values[index] = ReadCellText(workbookPart, cell);
        }

        return [.. values];
    }

    private static void ValidateHeader(IReadOnlyList<string> headerValues)
    {
        for (var i = 0; i < ExpectedColumns.Length; i++)
        {
            var actual = NormalizeCell(GetCell(headerValues, i));
            var expected = NormalizeCell(ExpectedColumns[i]);

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Шапка Excel не відповідає шаблону. " +
                    $"Колонка {i + 1}: очікується '{ExpectedColumns[i]}', отримано '{GetCell(headerValues, i) ?? "порожньо"}'.");
            }
        }
    }

    private static bool IsEmptyDataRow(List<string> cells)
        => cells.All(string.IsNullOrWhiteSpace);

    private static void EnsureDataWidth(List<string> cells, int rowNumber)
    {
        if (cells.Count < ExpectedColumnCount)
        {
            throw new InvalidOperationException(
                $"Рядок {rowNumber}: замало колонок. Використай Excel-шаблон з {ExpectedColumnCount} колонками.");
        }
    }

    private static string ReadCellText(WorkbookPart workbookPart, Cell cell)
    {
        var raw = cell.CellValue?.InnerText ?? cell.InnerText ?? string.Empty;
        var dataType = cell.DataType?.Value;

        if (dataType == CellValues.SharedString)
            return GetSharedString(workbookPart, raw);

        if (dataType == CellValues.InlineString)
            return cell.InlineString?.InnerText ?? cell.InnerText ?? raw;

        return raw;
    }

    private static string GetSharedString(WorkbookPart workbookPart, string raw)
    {
        if (!int.TryParse(raw, out var index))
            return raw;

        var item = workbookPart.SharedStringTablePart?.SharedStringTable?
            .Elements<SharedStringItem>()
            .ElementAtOrDefault(index);

        return item?.InnerText ?? raw;
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
            return -1;

        var letters = new string([.. cellReference.TakeWhile(char.IsLetter)]);

        if (letters.Length == 0)
            return -1;

        var index = 0;
        foreach (var c in letters.ToUpperInvariant())
            index = (index * 26) + (c - 'A' + 1);

        return index - 1;
    }

    private static List<ObservationImportParticipantDto> ParseParticipants(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var parts = MyRegex().Split(raw)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x));

        var result = new List<ObservationImportParticipantDto>();

        foreach (var part in parts)
        {
            var pair = part.Split(':', 2, StringSplitOptions.TrimEntries);
            var label = Clean(pair[0]);
            var role = pair.Length > 1 ? Clean(pair[1]) : null;

            if (label is null && role is null)
                continue;

            result.Add(new ObservationImportParticipantDto
            {
                LabelRaw = label,
                RoleRaw = role,
                IsUnknown = IsUnknownLabel(label)
            });
        }

        return result;
    }

    private static bool IsUnknownLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var v = value.Trim().ToLowerInvariant();
        return v == "нв" || v.StartsWith("нв ") || v == "unk" || v == "unknown";
    }

    private static DateTime ParseObservedDate(string? raw, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException($"Рядок {rowNumber}: не вказано час.");

        var value = raw.Trim();

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate) ||
            double.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("uk-UA"), out oaDate))
        {
            return DateTime.FromOADate(oaDate);
        }

        var formats = new[]
        {
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd H:mm",
            "yyyy-MM-ddTHH:mm",
            "dd.MM.yyyy HH:mm",
            "dd.MM.yyyy H:mm",
            "dd.MM.yyyy HH:mm:ss",
            "dd.MM.yyyy"
        };

        if (DateTime.TryParseExact(
                value,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            return parsed;
        }

        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("uk-UA"), DateTimeStyles.AllowWhiteSpaces, out parsed))
            return parsed;

        throw new InvalidOperationException($"Рядок {rowNumber}: не вдалося розібрати час '{raw}'.");
    }

    private static SubdivisionLinkStrength? ParseStrength(string? raw, int rowNumber)
    {
        var value = Clean(raw);
        if (value is null)
            return null;

        return value.ToLowerInvariant() switch
        {
            "1" or "слабкий" or "weak" => SubdivisionLinkStrength.Weak,
            "2" or "середній" or "medium" => SubdivisionLinkStrength.Medium,
            "3" or "сильний" or "strong" => SubdivisionLinkStrength.Strong,
            _ => throw new InvalidOperationException($"Рядок {rowNumber}: сила підрозділу має бути 1, 2 або 3.")
        };
    }

    private static string? GetCell(IReadOnlyList<string> cells, int index)
        => index < cells.Count ? cells[index] : null;

    private static string Require(string? value, int rowNumber, string label)
    {
        var cleaned = Clean(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            throw new InvalidOperationException($"Рядок {rowNumber}: не вказано {label}.");

        return cleaned;
    }

    private static string NormalizeCell(string? value)
        => (value ?? string.Empty).Trim().Replace(" ", string.Empty);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<NormalizedParticipant> NormalizeParticipants(IReadOnlyList<ObservationImportParticipantDto> participants)
    {
        var result = new List<NormalizedParticipant>();

        foreach (var participant in participants)
        {
            var labelRaw = string.IsNullOrWhiteSpace(participant.LabelRaw) ? null : participant.LabelRaw.Trim();
            var roleRaw = string.IsNullOrWhiteSpace(participant.RoleRaw) ? null : participant.RoleRaw.Trim();
            var isUnknown = participant.IsUnknown || UnknownIdentityText.IsUnknownLabel(labelRaw);

            if (labelRaw is null && roleRaw is null && !isUnknown)
                continue;

            result.Add(new NormalizedParticipant(
                UnknownIdentityText.NormalizeRawUnknownLabel(labelRaw),
                isUnknown,
                roleRaw));
        }

        return result;
    }

    private sealed record NormalizedParticipant(
        string? LabelRaw,
        bool IsUnknown,
        string? RoleRaw);

    [GeneratedRegex(@"[\r\n|;]+")]
    private static partial Regex MyRegex();
}
