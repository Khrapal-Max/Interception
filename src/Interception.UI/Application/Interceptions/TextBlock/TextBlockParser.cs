//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Interception.UI.Application.Interceptions.TextBlock;

/// <summary>
/// Парсить текстовий блок перехоплення у структурований результат.
///
/// Очікуваний формат:
///   Рядок 1:  21.03.2026, 09:15:50
///   Рядок 2:  410.1370
///   Рядок 3:  УКХ р/м ім. 3 кулб 69 обрп (р-н Маліївка - Січневе)
///   Рядок 4:  ЗВЕЗДА
///   Рядок 5+: ЦЫГАН, ПАНДА   ← або кожен на окремому рядку
///   Далі:     — діалог...
///             Коментар: ...  ← ігнорується
/// </summary>
public static partial class TextBlockParser
{
    // Частота: число.число, напр. 410.1370 або 150.725
    private static readonly Regex FrequencyRegex =
        GetFrequencyRegex();

    // Дата+час: 21.03.2026, 09:15  або  21.03.2026 09:15:50
    private static readonly Regex DateTimeRegex =
        GetDateTimeRegex();

    // Вектор в дужках: (р-н Маліївка - Січневе)
    private static readonly Regex VectorRegex =
        GetVectorRegex();

    // Рядок діалогу: починається з тире (— або -)
    private static readonly Regex DialogLineRegex =
        GetDialogLineRegex();

    // Позивний: рядок з лише великих літер/цифр/пробілів (кирилиця + латиниця)
    private static readonly Regex CallsignLineRegex =
        GetCallsignLineRegex();

    public static TextBlockParseResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Fail("Порожній текст.");

        var lines = text
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count < 2)
            return Fail("Замало рядків для парсингу.");

        var idx = 0;

        // -------------------------------------------------------------------------
        // Рядок 1 — Дата та час
        // -------------------------------------------------------------------------
        DateTime? observedDate = null;
        var dateMatch = DateTimeRegex.Match(lines[idx]);
        if (dateMatch.Success)
        {
            var datePart = dateMatch.Groups[1].Value.Replace("/", ".").Replace(",", ".");
            var timePart = dateMatch.Groups[2].Value;
            if (DateTime.TryParse($"{datePart} {timePart}", out var dt))
                observedDate = dt;
            idx++;
        }

        // -------------------------------------------------------------------------
        // Рядок 2 — Частота
        // -------------------------------------------------------------------------
        string? frequency = null;
        if (idx < lines.Count && FrequencyRegex.IsMatch(lines[idx].Replace(",", ".")))
        {
            frequency = lines[idx].Replace(",", ".");
            idx++;
        }

        // -------------------------------------------------------------------------
        // Рядок 3 — Division + VectorSignal (в дужках)
        // -------------------------------------------------------------------------
        string? division = null;
        string? vectorSignal = null;
        if (idx < lines.Count && !IsCallsignLine(lines[idx]) && !IsDialogLine(lines[idx]))
        {
            var line = lines[idx];
            var vecMatch = VectorRegex.Match(line);

            if (vecMatch.Success)
            {
                vectorSignal = vecMatch.Groups[1].Value.Trim();
                division = line[..vecMatch.Index].Trim().TrimEnd();
            }
            else
            {
                // Немає дужок — весь рядок це Division
                division = line;
            }

            idx++;
        }

        // -------------------------------------------------------------------------
        // Рядки 4+ — Ініціатор і Відповідачі
        // Логіка:
        //   - Перший рядок позивних = ініціатор (якщо один)
        //     або ініціатор + відповідачі через кому
        //   - Наступні рядки ВЕЛИКИМИ ЛІТЕРАМИ = ще відповідачі
        //   - Зупиняємось при рядку діалогу (— ...) або "Коментар:"
        // -------------------------------------------------------------------------
        string? initiator = null;
        var responders = new List<string?>();
        var noteLines = new List<string>();
        var inDialog = false;

        while (idx < lines.Count)
        {
            var line = lines[idx];

            // Коментар — ігноруємо разом з рештою блоку коментаря
            if (line.StartsWith("Коментар:", StringComparison.OrdinalIgnoreCase))
            {
                idx++;
                continue;
            }

            // Початок діалогу
            if (IsDialogLine(line))
            {
                inDialog = true;
            }

            if (inDialog)
            {
                noteLines.Add(line);
                idx++;
                continue;
            }

            // Рядок позивних
            if (IsCallsignLine(line))
            {
                // Розбиваємо по комі — перший елемент ініціатор якщо ще не встановлений
                var parts = line
                    .Split(',')
                    .Select(p => NormalizeCallsign(p.Trim()))
                    .Where(p => p is not null)
                    .ToList();

                if (initiator is null && parts.Count > 0)
                {
                    initiator = parts[0];
                    responders.AddRange(parts.Skip(1));
                }
                else
                {
                    responders.AddRange(parts);
                }

                idx++;
                continue;
            }

            // Якщо рядок не схожий ні на що — кладемо в нотатку
            noteLines.Add(line);
            idx++;
        }

        var note = noteLines.Count > 0
            ? string.Join("\n", noteLines).Trim()
            : null;

        if (observedDate is null && frequency is null && division is null)
            return Fail("Не вдалось розпізнати жодне поле. Перевірте формат блоку.");

        return new TextBlockParseResult
        {
            IsSuccess = true,
            ObservedDate = observedDate,
            Frequency = frequency,
            Division = division,
            VectorSignal = vectorSignal,
            Initiator = initiator,
            Responders = responders,
            Note = note
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool IsDialogLine(string line)
        => DialogLineRegex.IsMatch(line);

    private static bool IsCallsignLine(string line)
        => CallsignLineRegex.IsMatch(line) && !IsDialogLine(line);

    /// <summary>
    /// НВ / нв / невідома → null (невідомий учасник).
    /// </summary>
    private static string? NormalizeCallsign(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var t = value.Trim();
        if (t.Equals("НВ", StringComparison.OrdinalIgnoreCase)
            || t.Equals("нв", StringComparison.OrdinalIgnoreCase)
            || t.Equals("невідома", StringComparison.OrdinalIgnoreCase)
            || t.Equals("невідомий", StringComparison.OrdinalIgnoreCase))
            return null;
        return t;
    }

    private static TextBlockParseResult Fail(string error)
        => new() { IsSuccess = false, Error = error };

    [GeneratedRegex(@"^\d{2,4}[.,]\d{1,6}$", RegexOptions.Compiled)]
    private static partial Regex GetFrequencyRegex();

    [GeneratedRegex(@"(\d{1,2}[./]\d{1,2}[./]\d{2,4})[,\s]+(\d{1,2}:\d{2}(?::\d{2})?)", RegexOptions.Compiled)]
    private static partial Regex GetDateTimeRegex();

    [GeneratedRegex(@"\(([^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex GetVectorRegex();

    [GeneratedRegex(@"^[—\-–]", RegexOptions.Compiled)]
    private static partial Regex GetDialogLineRegex();

    [GeneratedRegex(@"^[А-ЯЁЇІЄA-Z0-9][А-ЯЁЇІЄA-Z0-9\s,\.]+$", RegexOptions.Compiled)]
    private static partial Regex GetCallsignLineRegex();
}
