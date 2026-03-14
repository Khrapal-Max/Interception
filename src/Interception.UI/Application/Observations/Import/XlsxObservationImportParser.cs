//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Interception.UI.Application.Observations.Import;

/// <summary>
/// Minimal dependency-free XLSX parser (OpenXML).
/// Reads the first worksheet and maps headers similarly to CsvObservationImportParser.
/// 
/// IMPORTANT:
/// - IBrowserFile.OpenReadStream() may forbid synchronous reads. Therefore we copy the input stream
///   to a MemoryStream using async APIs first.
/// - Templates may contain empty/title rows before the header row. We auto-detect the header row.
/// </summary>
public sealed class XlsxObservationImportParser
{
    private const int HeaderScanRows = 30;

    public async Task<IReadOnlyList<ObservationImportRow>> ParseAsync(Stream xlsxStream, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await xlsxStream.CopyToAsync(ms, ct);
        ms.Position = 0;

        return ParseInternal(ms, ct);
    }

    private static IReadOnlyList<ObservationImportRow> ParseInternal(MemoryStream ms, CancellationToken ct)
    {
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);

        var sharedStrings = ReadSharedStrings(zip);

        var sheetPath = ResolveFirstWorksheetPath(zip)
            ?? throw new InvalidOperationException("XLSX: не знайдено аркушів (workbook.xml / rels).");

        var sheetEntry = zip.GetEntry(sheetPath)
            ?? throw new InvalidOperationException($"XLSX: не знайдено лист '{sheetPath}'.");

        using var sheetStream = sheetEntry.Open();
        var xdoc = XDocument.Load(sheetStream);

        var sheetData = xdoc.Descendants().FirstOrDefault(x => x.Name.LocalName == "sheetData")
            ?? throw new InvalidOperationException("XLSX: не знайдено sheetData.");

        var rowsXml = sheetData.Elements().Where(x => x.Name.LocalName == "row").ToList();
        if (rowsXml.Count == 0)
            return [];

        // 1) Find header row (templates may contain empty/title rows at top)
        var headerRowIndex = FindHeaderRowIndex(rowsXml, sharedStrings);
        if (headerRowIndex < 0)
            throw new InvalidOperationException("XLSX: не знайдено рядок заголовків. Очікуються колонки 'час' та 'дія' (або 'action').");

        var headerCells = ReadRowCells(rowsXml[headerRowIndex], sharedStrings);
        var headers = headerCells.Select(NormHeader).ToList();

        // Helper to find column by canonical keys with 'contains' fallback.
        int FindCol(params string[] keys)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                var h = headers[i];
                if (string.IsNullOrWhiteSpace(h)) continue;

                foreach (var k in keys)
                {
                    if (h == k || h.Contains(k))
                        return i;
                }
            }
            return -1;
        }

        string? Get(List<string> cells, int col)
        {
            if (col < 0 || col >= cells.Count) return null;
            var v = cells[col];
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        var colTime = FindCol("час", "time", "date", "datetime");
        var colAction = FindCol("дія", "action");

        if (colAction < 0)
            throw new InvalidOperationException("XLSX: не знайдено колонку 'дія' / 'action' у заголовку.");

        // participants columns: "особа 1..N" / "person 1..N"
        var participantCols = headers
            .Select((h, i) => new HeaderCol(h, i))
            .Where(x => x.Header.StartsWith("особа") || x.Header.StartsWith("person"))
            .OrderBy(x => x.Number)
            .ThenBy(x => x.Index)
            .ToList();

        // optional role columns: "роль 1..N" / "role 1..N"
        var roleCols = headers
            .Select((h, i) => new HeaderCol(h, i))
            .Where(x => x.Header.StartsWith("роль") || x.Header.StartsWith("role"))
            .OrderBy(x => x.Number)
            .ThenBy(x => x.Index)
            .ToList();

        var roleMap = BuildRoleMap(participantCols, roleCols);

        var colLayer = FindCol("шар", "layer");
        var colRm = FindCol("р/м", "rm");
        var colPoint = FindCol("точка", "point");
        var colLocation = FindCol("локація", "location");
        var colDistrict = FindCol("район", "district");
        var colCompany = FindCol("фірма", "company");
        var colNote = FindCol("примітка", "note");

        // 2) Data rows start after header row
        var rows = new List<ObservationImportRow>();
        for (var r = headerRowIndex + 1; r < rowsXml.Count; r++)
        {
            ct.ThrowIfCancellationRequested();

            var cells = ReadRowCells(rowsXml[r], sharedStrings);

            // allow shorter rows
            while (cells.Count < headers.Count) cells.Add(string.Empty);

            var action = Get(cells, colAction);
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
            foreach (var pc in participantCols)
            {
                var rawCell = pc.Index < cells.Count ? cells[pc.Index]?.Trim() : null;
                if (string.IsNullOrWhiteSpace(rawCell))
                    continue;

                // If role is not provided in a dedicated column, allow formats:
                // "КЛИМ (водій)", "КЛИМ - водій", "КЛИМ: водій"
                var (labelRaw, roleFromCell) = TrySplitLabelAndRole(rawCell);

                string? roleRaw = null;
                if (roleMap.TryGetValue(pc.Index, out var roleCol) && roleCol >= 0 && roleCol < cells.Count)
                {
                    var roleCell = cells[roleCol]?.Trim();
                    if (!string.IsNullOrWhiteSpace(roleCell))
                        roleRaw = roleCell;
                }

                roleRaw ??= roleFromCell;

                var norm = Normalize(labelRaw);

                var (LabelRaw, IsUnknown, RoleRaw) = ParseParticipant(norm, roleRaw);
                
                participants.Add(new ObservationImportParticipant(LabelRaw, IsUnknown, RoleRaw));
            }

            rows.Add(new ObservationImportRow(
                date, (short)part, action.Trim(), layer, rm, point, location, district, company, note, participants));
        }

        return rows;
    }

    private static int FindHeaderRowIndex(List<XElement> rowsXml, IReadOnlyList<string> sharedStrings)
    {
        var take = Math.Min(HeaderScanRows, rowsXml.Count);

        var bestIdx = -1;
        var bestScore = -1;

        for (int i = 0; i < take; i++)
        {
            var cells = ReadRowCells(rowsXml[i], sharedStrings);
            if (cells.Count == 0) continue;

            var norm = cells.Select(NormHeader).ToList();
            var nonEmpty = norm.Count(x => !string.IsNullOrWhiteSpace(x));

            if (nonEmpty < 2)
                continue;

            var score = 0;

            if (norm.Any(x => x == "час" || x.Contains("час") || x.Contains("time") || x.Contains("date")))
                score += 2;

            if (norm.Any(x => x == "дія" || x.Contains("дія") || x.Contains("action")))
                score += 2;

            if (norm.Any(x => x.StartsWith("особа") || x.StartsWith("person")))
                score += 1;

            // Prefer later (closer to actual data) if equal score.
            if (score > bestScore || (score == bestScore && score >= 3 && i > bestIdx))
            {
                bestScore = score;
                bestIdx = i;
            }
        }

        return bestScore >= 3 ? bestIdx : -1;
    }

    private static List<string> ReadRowCells(XElement row, IReadOnlyList<string> sharedStrings)
    {
        // Sparse mapping by column index
        var map = new Dictionary<int, string?>();
        var maxCol = -1;

        foreach (var c in row.Elements().Where(x => x.Name.LocalName == "c"))
        {
            var r = (string?)c.Attribute("r"); // e.g., "C3"
            var col = ColumnIndexFromRef(r);
            if (col < 0) continue;

            maxCol = Math.Max(maxCol, col);
            map[col] = ReadCellValue(c, sharedStrings);
        }

        if (maxCol < 0)
            return [];

        var list = new List<string>(maxCol + 1);
        for (var i = 0; i <= maxCol; i++)
            list.Add(map.TryGetValue(i, out var v) ? v ?? string.Empty : string.Empty);

        return list;
    }

    private static int ColumnIndexFromRef(string? cellRef)
    {
        if (string.IsNullOrWhiteSpace(cellRef)) return -1;

        var letters = new string([.. cellRef.TakeWhile(char.IsLetter)]);
        if (letters.Length == 0) return -1;

        var col = 0;
        foreach (var ch in letters.ToUpperInvariant())
            col = col * 26 + (ch - 'A' + 1);

        return col - 1;
    }

    private static string? ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t"); // s / inlineStr / str / etc.

        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
            return cell.Descendants().FirstOrDefault(x => x.Name.LocalName == "t")?.Value;

        var v = cell.Elements().FirstOrDefault(x => x.Name.LocalName == "v")?.Value;
        if (string.IsNullOrWhiteSpace(v))
            return null;

        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx) &&
                idx >= 0 && idx < sharedStrings.Count)
                return sharedStrings[idx];

            return null;
        }

        return v;
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];

        using var s = entry.Open();
        var xdoc = XDocument.Load(s);

        var list = new List<string>();
        foreach (var si in xdoc.Descendants().Where(x => x.Name.LocalName == "si"))
        {
            var textParts = si.Descendants().Where(x => x.Name.LocalName == "t").Select(x => x.Value);
            list.Add(string.Concat(textParts));
        }

        return list;
    }

    private static string? ResolveFirstWorksheetPath(ZipArchive zip)
    {
        // workbook.xml lists sheets with r:id; workbook.xml.rels maps it to a sheet target.
        var wbEntry = zip.GetEntry("xl/workbook.xml");
        var relEntry = zip.GetEntry("xl/_rels/workbook.xml.rels");
        if (wbEntry is null || relEntry is null) return null;

        XDocument wb, rels;
        using (var s = wbEntry.Open()) wb = XDocument.Load(s);
        using (var s = relEntry.Open()) rels = XDocument.Load(s);

        var firstSheet = wb.Descendants().FirstOrDefault(x => x.Name.LocalName == "sheet");
        if (firstSheet is null) return null;

        var relId = firstSheet.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value;
        if (string.IsNullOrWhiteSpace(relId)) return null;

        var rel = rels.Descendants().FirstOrDefault(x =>
            x.Name.LocalName == "Relationship" &&
            string.Equals((string?)x.Attribute("Id"), relId, StringComparison.OrdinalIgnoreCase));

        var target = (string?)rel?.Attribute("Target"); // "worksheets/sheet1.xml"
        if (string.IsNullOrWhiteSpace(target)) return null;

        return "xl/" + target.TrimStart('/');
    }

    private static string NormHeader(string? h) => Normalize(h) ?? string.Empty;

    private static string? Normalize(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        return v.Trim().ToLowerInvariant();
    }

    private static int ExtractNumber(string header)
    {
        var digits = new string([.. header.Where(char.IsDigit)]);
        return int.TryParse(digits, out var n) ? n : int.MaxValue;
    }

    private static (DateOnly date, DayPart part) ParseDateAndPart(string? time)
    {
        if (string.IsNullOrWhiteSpace(time))
            return (DateOnly.FromDateTime(DateTime.UtcNow), DayPart.FirstHalf);

        var t = time.Trim();

        // Numeric Excel OADate
        if (double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa))
        {
            try
            {
                var dt = DateTime.FromOADate(oa);
                var partByTime = dt.Hour >= 12 ? DayPart.SecondHalf : DayPart.FirstHalf;
                return (DateOnly.FromDateTime(dt), partByTime);
            }
            catch { /* ignore */ }
        }

        var part = DayPart.FirstHalf;

        if (t.EndsWith("AM", StringComparison.OrdinalIgnoreCase)) { part = DayPart.FirstHalf; t = t[..^2].Trim(); }
        else if (t.EndsWith("PM", StringComparison.OrdinalIgnoreCase)) { part = DayPart.SecondHalf; t = t[..^2].Trim(); }

        if (t.Contains("перша", StringComparison.OrdinalIgnoreCase)) part = DayPart.FirstHalf;
        if (t.Contains("друга", StringComparison.OrdinalIgnoreCase)) part = DayPart.SecondHalf;

        t = t.Replace("AM", "", StringComparison.OrdinalIgnoreCase)
             .Replace("PM", "", StringComparison.OrdinalIgnoreCase)
             .Trim();

        if (DateOnly.TryParse(t, new CultureInfo("uk-UA"), DateTimeStyles.None, out var dUk))
            return (dUk, part);

        if (DateOnly.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dInv))
            return (dInv, part);

        if (DateTime.TryParse(t, new CultureInfo("uk-UA"), DateTimeStyles.AssumeLocal, out var dt2))
            return (DateOnly.FromDateTime(dt2), part);

        return (DateOnly.FromDateTime(DateTime.UtcNow), part);
    }


    private sealed record HeaderCol(string Header, int Index)
    {
        public int Number { get; } = ExtractNumber(Header);
    }

    private static Dictionary<int, int> BuildRoleMap(
        IReadOnlyList<HeaderCol> participantCols,
        IReadOnlyList<HeaderCol> roleCols)
    {
        // map: participant column index -> role column index
        var map = new Dictionary<int, int>();

        if (participantCols.Count == 0 || roleCols.Count == 0)
            return map;

        // Build role lookup by extracted number
        var roleByNum = new Dictionary<int, int>();
        foreach (var rc in roleCols)
        {
            if (rc.Number != int.MaxValue && !roleByNum.ContainsKey(rc.Number))
                roleByNum[rc.Number] = rc.Index;
        }

        foreach (var pc in participantCols)
        {
            // Pair by same number if present
            if (pc.Number != int.MaxValue && roleByNum.TryGetValue(pc.Number, out var roleIndex))
            {
                map[pc.Index] = roleIndex;
                continue;
            }

            // Fallback 1: role column immediately to the right with generic header ("роль"/"role")
            var rightIndex = pc.Index + 1;
            var rightRole = roleCols.FirstOrDefault(x => x.Index == rightIndex);
            if (rightRole is not null && rightRole.Number == int.MaxValue)
            {
                map[pc.Index] = rightIndex;
            }
        }

        // Fallback 2: if counts match, pair sequentially
        if (map.Count == 0 && roleCols.Count == participantCols.Count)
        {
            for (var i = 0; i < participantCols.Count; i++)
                map[participantCols[i].Index] = roleCols[i].Index;
        }

        return map;
    }

    private static (string labelRaw, string? roleRaw) TrySplitLabelAndRole(string raw)
    {
        var s = raw.Trim();

        // Parentheses form: "КЛИМ (водій)"
        var open = s.LastIndexOf('(');
        var close = s.LastIndexOf(')');
        if (open >= 1 && close > open)
        {
            var label = s[..open].Trim();
            var role = s[(open + 1)..close].Trim();
            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(role))
                return (label, role);
        }

        // "label - role"
        var dash = s.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0)
        {
            var label = s[..dash].Trim();
            var role = s[(dash + 3)..].Trim();
            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(role))
                return (label, role);
        }

        // "label: role"
        var colon = s.IndexOf(": ", StringComparison.Ordinal);
        if (colon > 0)
        {
            var label = s[..colon].Trim();
            var role = s[(colon + 2)..].Trim();
            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(role))
                return (label, role);
        }

        return (s, null);
    }

    private static (string? LabelRaw, bool IsUnknown, string? RoleRaw) ParseParticipant(string? rawLabel, string? rawRole)
    {
        var label = string.IsNullOrWhiteSpace(rawLabel) ? null : rawLabel.Trim();
        var role = string.IsNullOrWhiteSpace(rawRole) ? null : rawRole.Trim();

        var isUnknown = UnknownIdentityText.IsUnknownLabel(label);

        return (UnknownIdentityText.NormalizeRawUnknownLabel(label), isUnknown, role);
    }

    internal enum DayPart : short
    {
        FirstHalf = 1,
        SecondHalf = 2
    }
}
