//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using ClosedXML.Excel;
using Interception.UI.Application.Interceptions.Dtos;

namespace Interception.UI.Application.Interceptions.Import;

/// <summary>
/// Парсить Excel-файл (формат зразка) у список <see cref="ImportRowDto"/>.
///
/// Очікувана структура аркуша (перший рядок — заголовки, дані з 2-го):
///   Col 1  Дата
///   Col 2  Час
///   Col 3  Частота
///   Col 4  Р/М (Division)
///   Col 5  Точка перехвату
///   Col 6  Вектор сігнала
///   Col 7  Ініціатор
///   Col 8  Роль ініціатора
///   Col 9  Підрозділ ініціатора
///   Col 10 Відповідач
///   Col 11 Роль відповідача
///   Col 12 Дія
///   Col 13 Деталі
/// </summary>
public sealed class ExcelImportParser
{
    private static readonly HashSet<string> UnknownMarkers =
        new(StringComparer.OrdinalIgnoreCase) { "НВ", "нв", "невідома", "невідомий", "unknown", "" };

    /// <summary>
    /// Парсить потік Excel-файлу.
    /// Повертає пари (рядок, помилка?) — помилка null означає успішний рядок.
    /// </summary>
    public IReadOnlyList<(ImportRowDto? Row, ImportRowError? Error)> Parse(Stream stream)
    {
        using var wb = new XLWorkbook(stream);

        // Беремо перший аркуш з даними (не аркуш "ДІЇ")
        var ws = wb.Worksheets
            .FirstOrDefault(s => !s.Name.Equals("ДІЇ", StringComparison.OrdinalIgnoreCase))
            ?? wb.Worksheets.First();

        var results = new List<(ImportRowDto?, ImportRowError?)>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        // Рядок 1 — заголовки, починаємо з 2
        for (var r = 2; r <= lastRow; r++)
        {
            var row = ws.Row(r);

            // Пропускаємо повністю порожні рядки
            if (row.IsEmpty()) continue;

            try
            {
                var dto = ParseRow(row, r);
                results.Add((dto, null));
            }
            catch (Exception ex)
            {
                results.Add((null, new ImportRowError(r, ex.Message)));
            }
        }

        return results;
    }

    private static ImportRowDto ParseRow(IXLRow row, int rowNumber)
    {
        // --- Дата ---
        var rawDate = row.Cell(1).Value;
        if (!TryParseDate(rawDate, out var date))
            throw new FormatException($"Невірний формат дати: '{rawDate}'");

        // --- Час ---
        var rawTime = row.Cell(2).Value;
        if (!TryParseTime(rawTime, out var time))
            throw new FormatException($"Невірний формат часу: '{rawTime}'");

        // --- Обов'язкова дія ---
        var actionName = NormalizeOptional(row.Cell(12).GetString());

        return new ImportRowDto
        {
            RowNumber        = rowNumber,
            Date             = date,
            Time             = time,
            Frequency        = NormalizeOptional(row.Cell(3).GetString()),
            Division         = NormalizeOptional(row.Cell(4).GetString()),
            PointSignal      = NormalizeOptional(row.Cell(5).GetString()),
            VectorSignal     = NormalizeOptional(row.Cell(6).GetString()),
            InitiatorName    = NormalizeParticipant(row.Cell(7).GetString()),
            InitiatorRole    = NormalizeOptional(row.Cell(8).GetString()),
            InitiatorDivision= NormalizeOptional(row.Cell(9).GetString()),
            ResponderName    = NormalizeParticipant(row.Cell(10).GetString()),
            ResponderRole    = NormalizeOptional(row.Cell(11).GetString()),
            ActionName       = actionName,
            Details          = NormalizeOptional(row.Cell(13).GetString()),
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool TryParseDate(XLCellValue value, out DateOnly result)
    {
        result = default;
        if (value.IsDateTime)  { result = DateOnly.FromDateTime(value.GetDateTime()); return true; }
        if (value.IsText)      { return DateOnly.TryParse(value.GetText(), out result); }
        return false;
    }

    private static bool TryParseTime(XLCellValue value, out TimeOnly result)
    {
        result = default;

        if (value.IsTimeSpan)
        {
            result = TimeOnly.FromTimeSpan(value.GetTimeSpan());
            return true;
        }

        if (value.IsDateTime)
        {
            result = TimeOnly.FromDateTime(value.GetDateTime());
            return true;
        }

        if (value.IsText)
        {
            return TimeOnly.TryParse(value.GetText(), out result);
        }

        return false;
    }

    /// <summary>
    /// null / пробіли / "НВ" / "невідома" → null (невідомий учасник).
    /// </summary>
    private static string? NormalizeParticipant(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || UnknownMarkers.Contains(trimmed))
            return null;
        return trimmed;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
