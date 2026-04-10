//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using ClosedXML.Excel;
using Interception.UI.Application.Import.Dtos;

namespace Interception.UI.Application.Import.Services;

/// <summary>
/// Парсить Excel-файл у список <see cref="ImportRowDto"/>.
/// Працює з аркушем «Спостереження», сумісним із поточним Excel-експортом.
/// </summary>
public sealed class ExcelImportParser
{
    private static readonly HashSet<string> UnknownMarkers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "НВ", "НЕВІДОМИЙ", "НЕВІДОМА", "UNKNOWN", ""
        };

    public IReadOnlyList<(ImportRowDto? Row, ImportRowErrorDto? Error)> Parse(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheets
            .FirstOrDefault(x => x.Name.Equals("Спостереження", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.FirstOrDefault(x => !x.Name.Equals("ДІЇ", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheet(1);

        var results = new List<(ImportRowDto?, ImportRowErrorDto?)>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (IsEffectivelyEmpty(row))
                continue;

            try
            {
                results.Add((ParseRow(row, rowNumber), null));
            }
            catch (Exception ex)
            {
                results.Add((null, new ImportRowErrorDto(rowNumber, ex.Message)));
            }
        }

        return results;
    }

    private static ImportRowDto ParseRow(IXLRow row, int rowNumber)
    {
        var observedAtLocal = ParseObservedAt(row.Cell(1).Value);

        return new ImportRowDto
        {
            RowNumber = rowNumber,
            ObservedAtLocal = observedAtLocal,
            Frequency = NormalizeOptional(row.Cell(2).GetString()),
            Division = NormalizeOptional(row.Cell(3).GetString()),
            VectorSignal = NormalizeOptional(row.Cell(4).GetString()),
            PointSignal = NormalizeOptional(row.Cell(5).GetString()),
            ActionName = NormalizeOptional(row.Cell(6).GetString()),
            Note = NormalizeOptional(row.Cell(7).GetString()),
            Participants = ParseParticipants(row.Cell(8).GetString()),
            Labels = ParseLabels(row.Cell(10).GetString())
        };
    }

    private static DateTime ParseObservedAt(XLCellValue value)
    {
        if (value.IsDateTime)
            return value.GetDateTime();

        if (value.IsText && DateTime.TryParse(value.GetText(), out var parsed))
            return parsed;

        throw new FormatException($"Невірний формат дати/часу: '{value}'");
    }

    private static IReadOnlyList<ImportParticipantDto> ParseParticipants(string? value)
    {
        var items = SplitTopLevel(value)
            .Select(NormalizeOptional)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var result = new List<ImportParticipantDto>();
        var ordinal = 1;

        foreach (var item in items)
        {
            var (name, role) = SplitParticipant(item!);
            var isUnknown = IsUnknownParticipant(name);

            result.Add(new ImportParticipantDto
            {
                Ordinal = ordinal++,
                Name = isUnknown ? null : name,
                Role = NormalizeOptional(role),
                IsUnknown = isUnknown
            });
        }

        return result;
    }

    private static IReadOnlyList<string> ParseLabels(string? value)
        => [.. SplitTopLevel(value)
            .Select(NormalizeOptional)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)!];

    private static (string? Name, string? Role) SplitParticipant(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            var openIndex = trimmed.LastIndexOf('(');
            if (openIndex > 0)
            {
                var name = NormalizeOptional(trimmed[..openIndex]);
                var role = NormalizeOptional(trimmed[(openIndex + 1)..^1]);
                return (name, role);
            }
        }

        return (NormalizeOptional(trimmed), null);
    }

    private static IReadOnlyList<string> SplitTopLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 0;

        foreach (var ch in value)
        {
            if (ch == '(')
            {
                depth++;
                current.Append(ch);
                continue;
            }

            if (ch == ')')
            {
                depth = Math.Max(0, depth - 1);
                current.Append(ch);
                continue;
            }

            if ((ch == ',' || ch == ';' || ch == '\n') && depth == 0)
            {
                var token = current.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(token))
                    result.Add(token);

                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        var tail = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(tail))
            result.Add(tail);

        return result;
    }

    private static bool IsUnknownParticipant(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return true;

        if (UnknownMarkers.Contains(normalized))
            return true;

        return normalized.StartsWith("НВ ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEffectivelyEmpty(IXLRow row)
    {
        for (var i = 1; i <= 10; i++)
        {
            if (!string.IsNullOrWhiteSpace(row.Cell(i).GetString()))
                return false;
        }

        return !row.Cell(1).Value.IsDateTime;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
