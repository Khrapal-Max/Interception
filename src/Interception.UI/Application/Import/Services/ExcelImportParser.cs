//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Interception.UI.Application.Import.Dtos;

namespace Interception.UI.Application.Import.Services;

/// <summary>
/// Парсер Excel-аркуша «Спостереження», сумісного з поточним експортом observation.
/// Старий шаблон імпорту більше не підтримується.
/// </summary>
public sealed class ExcelImportParser
{
    private static readonly Regex UnknownRegex = new(@"^НВ(\s+\d+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyList<(ImportRowDto? Row, ImportRowErrorDto? Error)> Parse(Stream stream)
    {
        using var wb = new XLWorkbook(stream);

        var ws = wb.Worksheets.FirstOrDefault(x => x.Name.Equals("Спостереження", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Аркуш 'Спостереження' не знайдено.");

        var headerMap = BuildHeaderMap(ws.Row(1));
        EnsureRequiredHeaders(headerMap);

        var results = new List<(ImportRowDto?, ImportRowErrorDto?)>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var r = 2; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            if (row.IsEmpty())
                continue;

            try
            {
                results.Add((ParseRow(row, r, headerMap), null));
            }
            catch (Exception ex)
            {
                results.Add((null, new ImportRowErrorDto(r, ex.Message)));
            }
        }

        return results;
    }

    private static ImportRowDto ParseRow(IXLRow row, int rowNumber, IReadOnlyDictionary<string, int> headerMap)
    {
        var observedAt = ParseObservedAt(row.Cell(headerMap["дата/час"]).Value);
        var participantsText = NormalizeOptional(row.Cell(headerMap["учасники"]).GetString());

        return new ImportRowDto
        {
            RowNumber = rowNumber,
            ObservedAtLocal = observedAt,
            Frequency = NormalizeOptional(GetCellString(row, headerMap, "частота")),
            Division = NormalizeOptional(GetCellString(row, headerMap, "підрозділ")),
            VectorSignal = NormalizeOptional(GetCellString(row, headerMap, "вектор")),
            PointSignal = NormalizeOptional(GetCellString(row, headerMap, "точка")),
            ActionName = NormalizeOptional(GetCellString(row, headerMap, "дія")),
            Note = NormalizeOptional(GetCellString(row, headerMap, "примітка")),
            Participants = ParseParticipants(participantsText),
            Labels = ParseLabels(NormalizeOptional(GetCellString(row, headerMap, "мітки")))
        };
    }

    private static DateTime ParseObservedAt(XLCellValue value)
    {
        if (value.IsDateTime)
            return DateTime.SpecifyKind(value.GetDateTime(), DateTimeKind.Local);

        if (value.IsText && DateTime.TryParse(value.GetText(), CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsed))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Local);

        throw new FormatException($"Невірний формат 'Дата/час': '{value}'");
    }

    private static IReadOnlyList<ImportParticipantDto> ParseParticipants(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var tokens = SplitByCommaOutsideParentheses(value);
        var result = new List<ImportParticipantDto>();
        var ordinal = 1;

        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var (name, role) = ParseParticipantToken(trimmed);
            var isUnknown = string.IsNullOrWhiteSpace(name) || UnknownRegex.IsMatch(name);

            result.Add(new ImportParticipantDto
            {
                Ordinal = ordinal++,
                Name = isUnknown ? null : name,
                Role = role,
                IsUnknown = isUnknown
            });
        }

        return result;
    }

    private static (string? Name, string? Role) ParseParticipantToken(string value)
    {
        var openIndex = value.LastIndexOf('(');
        var closeIndex = value.EndsWith(')') ? value.Length - 1 : -1;

        if (openIndex > 0 && closeIndex > openIndex)
        {
            var name = NormalizeOptional(value[..openIndex]);
            var role = NormalizeOptional(value[(openIndex + 1)..closeIndex]);
            return (name, role);
        }

        return (NormalizeOptional(value), null);
    }

    private static IReadOnlyList<string> ParseLabels(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return [.. value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static List<string> SplitByCommaOutsideParentheses(string input)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var depth = 0;

        foreach (var ch in input)
        {
            if (ch == '(')
                depth++;
            else if (ch == ')' && depth > 0)
                depth--;

            if (ch == ',' && depth == 0)
            {
                result.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(ch);
        }

        if (sb.Length > 0)
            result.Add(sb.ToString());

        return result;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastCell = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var c = 1; c <= lastCell; c++)
        {
            var key = NormalizeOptional(headerRow.Cell(c).GetString())?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(key))
                map[key] = c;
        }

        return map;
    }

    private static void EnsureRequiredHeaders(IReadOnlyDictionary<string, int> headerMap)
    {
        var required = new[] { "дата/час", "частота", "підрозділ", "вектор", "точка", "дія", "примітка", "учасники", "мітки" };
        var missing = required.Where(x => !headerMap.ContainsKey(x)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"У файлі відсутні колонки: {string.Join(", ", missing)}.");
    }

    private static string? GetCellString(IXLRow row, IReadOnlyDictionary<string, int> headerMap, string header)
        => headerMap.TryGetValue(header, out var column) ? row.Cell(column).GetString() : null;

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
