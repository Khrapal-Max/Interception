//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using System.Globalization;

namespace Interception.UI.Application.Observations.Import;

/// <summary>
/// Dependency-free CSV/TSV parser for registry imports.
/// Recommended: export Excel to CSV before import.
///
/// Supported header variants (UA/EN):
/// - час/time/date/datetime
/// - дія/action
/// - шар/layer, р/м/rm, точка/point, локація/location, район/district, фірма/company, примітка/note
/// - особа 1..N / person 1..N
/// - роль 1..N / role 1..N (optional)
///   Also supports adjacency: if a column immediately after "особа N" is "роль", it will be used.
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

        // column lookup helpers (robust: exact match OR contains)
        int FindCol(params string[] keys)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                var h = headers[i];
                if (string.IsNullOrWhiteSpace(h)) continue;

                foreach (var k in keys)
                {
                    if (h == k || h.Contains(k, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }
            return -1;
        }

        string? Get(IReadOnlyList<string> cells, int col)
            => col >= 0 && col < cells.Count ? (string.IsNullOrWhiteSpace(cells[col]) ? null : cells[col].Trim()) : null;

        var colTime = FindCol("час", "time", "date", "datetime");
        var colAction = FindCol("дія", "action");

        var colLayer = FindCol("шар", "layer");
        var colRm = FindCol("р/м", "rm");
        var colPoint = FindCol("точка", "point");
        var colLocation = FindCol("локація", "location");
        var colDistrict = FindCol("район", "district");
        var colCompany = FindCol("фірма", "company");
        var colNote = FindCol("примітка", "note");

        // participants: "особа 1..N" / "person 1..N"
        var personCols = headers
            .Select((h, i) => new { h, i })
            .Where(x => x.h.StartsWith("особа", StringComparison.OrdinalIgnoreCase) || x.h.StartsWith("person", StringComparison.OrdinalIgnoreCase))
            .Select(x => new { x.i, n = ExtractNumber(x.h) })
            .OrderBy(x => x.n)
            .ThenBy(x => x.i)
            .ToList();

        // roles: "роль 1..N" / "role 1..N"
        var roleColsByNumber = headers
            .Select((h, i) => new { h, i })
            .Where(x => x.h.StartsWith("роль", StringComparison.OrdinalIgnoreCase) || x.h.StartsWith("role", StringComparison.OrdinalIgnoreCase))
            .Select(x => new { x.i, n = ExtractNumber(x.h) })
            .Where(x => x.n != int.MaxValue) // numbered roles only
            .ToDictionary(x => x.n, x => x.i);

        var rows = new List<ObservationImportRow>();

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cells = SplitCsvLine(line, delimiter).ToList();
            while (cells.Count < headers.Count) cells.Add(string.Empty);

            var action = colAction >= 0 ? Get(cells, colAction) : null;
            if (string.IsNullOrWhiteSpace(action))
                continue;

            var time = colTime >= 0 ? Get(cells, colTime) : null;
            var (date, part) = ParseDateAndPart(time);

            var layer = Get(cells, colLayer);
            var rm = Get(cells, colRm);
            var point = Get(cells, colPoint);
            var location = Get(cells, colLocation);
            var district = Get(cells, colDistrict);
            var company = Get(cells, colCompany);
            var note = Get(cells, colNote);

            var participants = new List<ObservationImportParticipant>();

            foreach (var pc in personCols)
            {
                var labelCell = Get(cells, pc.i);
                if (string.IsNullOrWhiteSpace(labelCell))
                    continue;

                // 1) Try role from a matching "роль N" column
                string? role = null;

                if (pc.n != int.MaxValue && roleColsByNumber.TryGetValue(pc.n, out var roleCol))
                    role = Get(cells, roleCol);

                // 2) If not found: check adjacency (person col + 1 has header "роль"/"role")
                if (string.IsNullOrWhiteSpace(role))
                {
                    var adj = pc.i + 1;
                    if (adj < headers.Count)
                    {
                        var h = headers[adj];
                        if (h.Equals("роль", StringComparison.OrdinalIgnoreCase) ||
                            h.Equals("role", StringComparison.OrdinalIgnoreCase) ||
                            h.StartsWith("роль", StringComparison.OrdinalIgnoreCase) ||
                            h.StartsWith("role", StringComparison.OrdinalIgnoreCase))
                        {
                            role = Get(cells, adj);
                        }
                    }
                }

                // 3) If still not found: parse role from the same cell: "NAME (role)" / "NAME - role" / "NAME: role"
                var (label, parsedRole) = SplitLabelAndRole(labelCell);
                if (string.IsNullOrWhiteSpace(role))
                    role = parsedRole;

                var norm = Normalize(label);
                var isUnknown = norm is "нв" or "nv" or "unknown" or "unk" or "?";

                participants.Add(new ObservationImportParticipant(label, isUnknown, string.IsNullOrWhiteSpace(role) ? null : role!.Trim()));
            }

            rows.Add(new ObservationImportRow(
                ObservedDate: date,
                DayPart: (short)part,
                ActionRaw: action.Trim(),
                Layer: layer,
                RmRaw: rm,
                PointRaw: point,
                LocationRaw: location,
                DistrictRaw: district,
                CompanyRaw: company,
                Note: note,
                Participants: participants));
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
        var digits = new string(header.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : int.MaxValue;
    }

    private static (string label, string? role) SplitLabelAndRole(string raw)
    {
        var s = raw.Trim();

        // NAME (role)
        var open = s.LastIndexOf('(');
        var close = s.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            var label = s[..open].Trim();
            var role = s[(open + 1)..close].Trim();
            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(role))
                return (label, role);
        }

        // NAME - role
        var dash = s.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0)
        {
            var label = s[..dash].Trim();
            var role = s[(dash + 3)..].Trim();
            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(role))
                return (label, role);
        }

        // NAME: role
        var colon = s.IndexOf(':');
        if (colon > 0)
        {
            var label = s[..colon].Trim();
            var role = s[(colon + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(role))
                return (label, role);
        }

        return (s, null);
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

        if (t.EndsWith("AM", StringComparison.OrdinalIgnoreCase))
        {
            part = DayPart.FirstHalf;
            t = t[..^2].Trim();
        }
        else if (t.EndsWith("PM", StringComparison.OrdinalIgnoreCase))
        {
            part = DayPart.SecondHalf;
            t = t[..^2].Trim();
        }

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

        for (var i = 0; i < line.Length; i++)
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
