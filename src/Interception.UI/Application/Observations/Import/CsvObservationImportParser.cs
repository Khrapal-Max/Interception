//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using System.Globalization;

namespace Interception.UI.Application.Observations.Import;

/// <summary>
/// Dependency-free CSV/TSV parser for registry imports.
/// Recommended: export Excel to CSV before import.
/// </summary>
public sealed class CsvObservationImportParser
{
    public async Task<IReadOnlyList<ObservationImportRow>> ParseAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);

        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null)
            return [];

        var delimiter = DetectDelimiter(headerLine);

        var headers = SplitCsvLine(headerLine, delimiter)
            .Select(NormHeader)
            .ToList();

        var rows = new List<ObservationImportRow>();
        string? line;
        var lineNo = 1;

        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            lineNo++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cells = SplitCsvLine(line, delimiter).ToList();
            while (cells.Count < headers.Count) cells.Add(string.Empty);

            string? Get(string key)
            {
                var idx = headers.IndexOf(key);
                if (idx < 0 || idx >= cells.Count) return null;
                var v = cells[idx];
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }

            // columns (Ukrainian + English synonyms)
            var time = Get("час") ?? Get("date") ?? Get("datetime");
            var action = Get("дія") ?? Get("action");
            if (string.IsNullOrWhiteSpace(action))
                continue; // let service decide if invalid; parser keeps minimal

            var (date, part) = ParseDateAndPart(time);

            var layer = ParseDecimal(Get("шар") ?? Get("layer"));
            var rm = Get("р/м") ?? Get("rm");
            var point = Get("точка") ?? Get("point");
            var location = Get("локація") ?? Get("location");
            var district = Get("район") ?? Get("district");
            var company = Get("фірма") ?? Get("company");
            var note = Get("примітка") ?? Get("note");

            // participants: any header that starts with "особа" or "person"
            var participantHeaders = headers
                .Select((h, i) => new { h, i })
                .Where(x => x.h.StartsWith("особа") || x.h.StartsWith("person"))
                .OrderBy(x => ExtractNumber(x.h))
                .ThenBy(x => x.i)
                .ToList();

            var participants = new List<ObservationImportParticipant>();
            foreach (var ph in participantHeaders)
            {
                var raw = ph.i < cells.Count ? cells[ph.i]?.Trim() : null;
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var norm = Normalize(raw);
                var isUnknown = norm is "нв" or "nv" or "unknown" or "unk" or "?";

                participants.Add(new ObservationImportParticipant(raw, isUnknown, null));
            }

            rows.Add(new ObservationImportRow(date, (short)part, action.Trim(), layer, rm, point, location, district, company, note, participants));
        }

        return rows;
    }

    private static char DetectDelimiter(string headerLine)
    {
        // Heuristic: prefer ';' for many locales, then '\t', then ','
        var sem = headerLine.Count(c => c == ';');
        var tab = headerLine.Count(c => c == '\t');
        var com = headerLine.Count(c => c == ',');
        if (sem >= tab && sem >= com) return ';';
        if (tab >= com) return '\t';
        return ',';
    }

    private static string NormHeader(string? h) => Normalize(h) ?? string.Empty;

    private static string? Normalize(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        return v.Trim().ToLowerInvariant();
    }

    private static int ExtractNumber(string header)
    {
        // "особа 12" -> 12
        var digits = new string([.. header.Where(char.IsDigit)]);
        return int.TryParse(digits, out var n) ? n : int.MaxValue;
    }

    private static decimal? ParseDecimal(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        v = v.Trim();

        // Try invariant first, then local comma decimal
        if (decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;

        var v2 = v.Replace(',', '.');
        if (decimal.TryParse(v2, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
            return d;

        return null;
    }

    private static (DateOnly date, DayPart part) ParseDateAndPart(string? time)
    {
        // Supports:
        //  - "04.03.2026 AM"
        //  - "2026-03-04 PM"
        //  - "04.03.2026" (defaults to FirstHalf)
        if (string.IsNullOrWhiteSpace(time))
            return (DateOnly.FromDateTime(DateTime.UtcNow), DayPart.FirstHalf);

        var t = time.Trim();
        var part = DayPart.FirstHalf;

        if (t.EndsWith("AM", StringComparison.OrdinalIgnoreCase)) { part = DayPart.FirstHalf; t = t[..^2].Trim(); }
        else if (t.EndsWith("PM", StringComparison.OrdinalIgnoreCase)) { part = DayPart.SecondHalf; t = t[..^2].Trim(); }

        // Ukrainian hints
        if (t.Contains("перша", StringComparison.OrdinalIgnoreCase)) part = DayPart.FirstHalf;
        if (t.Contains("друга", StringComparison.OrdinalIgnoreCase)) part = DayPart.SecondHalf;

        // If time had a separate token like "AM/PM" in the middle, strip it
        t = t.Replace("AM", "", StringComparison.OrdinalIgnoreCase)
             .Replace("PM", "", StringComparison.OrdinalIgnoreCase)
             .Trim();

        if (DateOnly.TryParse(t, new CultureInfo("uk-UA"), DateTimeStyles.None, out var dUk))
            return (dUk, part);

        if (DateOnly.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dInv))
            return (dInv, part);

        // try DateTime
        if (DateTime.TryParse(t, new CultureInfo("uk-UA"), DateTimeStyles.AssumeLocal, out var dt))
            return (DateOnly.FromDateTime(dt), part);

        return (DateOnly.FromDateTime(DateTime.UtcNow), part);
    }

    // Minimal CSV splitter with quotes.
    private static IEnumerable<string> SplitCsvLine(string line, char delimiter)
    {
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // escaped quote
                    sb.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && c == delimiter)
            {
                yield return sb.ToString();
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        yield return sb.ToString();
    }
}

/// <summary>
/// Kept local to this file to avoid taking a dependency on domain namespace in the parser.
/// </summary>
internal enum DayPart : short
{
    FirstHalf = 1,
    SecondHalf = 2
}
