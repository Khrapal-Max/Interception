//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Components.Pages.Observations.Models;
using Interception.UI.Domain.Enums;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Interception.UI.Components.Pages.Observations.Parsing;

/// <summary>
/// Converts pasted service radio text block into observation draft fields.
/// </summary>
internal static partial class ObservationRadioTextParser
{
    private const string DefaultAction = "службовий р/о";

    public static ObservationRadioParseResult Parse(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new ObservationRadioParseResult
            {
                ErrorMessage = "Вставте текст службового р/о."
            };
        }

        var normalizedText = Normalize(rawText);
        var lines = normalizedText
            .Split('\n')
            .Select(CleanLine)
            .ToList();

        var dateIndex = FindDateLineIndex(lines);
        if (dateIndex < 0)
        {
            return new ObservationRadioParseResult
            {
                ErrorMessage = "Не знайдено рядок з датою та часом у форматі 20.03.2026, 19:19:40."
            };
        }

        var observedDateText = lines[dateIndex];
        if (!DateTime.TryParseExact(
                observedDateText,
                "dd.MM.yyyy, HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var observedDate))
        {
            return new ObservationRadioParseResult
            {
                ErrorMessage = "Не вдалося розібрати дату та час блоку."
            };
        }

        var frequencyIndex = NextNonEmptyLineIndex(lines, dateIndex + 1);
        var rmIndex = frequencyIndex >= 0 ? NextNonEmptyLineIndex(lines, frequencyIndex + 1) : -1;

        if (frequencyIndex < 0 || rmIndex < 0)
        {
            return new ObservationRadioParseResult
            {
                ErrorMessage = "Після дати очікуються рядки з частотою та р/м."
            };
        }

        var frequency = CleanValue(lines[frequencyIndex]);
        var rmLine = CleanValue(lines[rmIndex]);

        var participants = ExtractParticipants(lines, rmIndex + 1, out var bodyStartIndex);
        var body = bodyStartIndex >= 0
            ? BuildBody(lines, bodyStartIndex)
            : string.Empty;

        var (rmRaw, districtRaw) = SplitRmAndDistrict(rmLine);
        var sourcePost = ParseSourcePost(lines);

        var result = new ObservationRadioParseResult
        {
            IsSuccess = true,
            SourcePost = sourcePost,
            SuggestedActionRaw = DefaultAction,
            Seed = new ObservationCreateSeedModel
            {
                ObservedDate = observedDate,
                ActionRaw = DefaultAction,
                Layer = frequency,
                RmRaw = rmRaw,
                DistrictRaw = districtRaw,
                Note = CleanValue(body),
                SourcePost = sourcePost
            }
        };

        if (participants.Count == 0)
        {
            result.Warnings.Add("Не знайдено окремий блок учасників. Перевірте текст.");
        }
        else
        {
            result.Seed.Participants = [.. participants
                 .Select((value, index) => new ObservationParticipantSeedRow
                 {
                     LabelRaw = value,
                     Ordinal = index + 1,
                     IsUnknown = IsUnknownParticipant(value)
                 })];
        }

        result.SuggestedTags = BuildSuggestedTags(sourcePost, rmRaw, districtRaw);

        if (string.IsNullOrWhiteSpace(result.Seed.Layer))
            result.Warnings.Add("Частоту не вдалося визначити.");

        if (string.IsNullOrWhiteSpace(result.Seed.RmRaw))
            result.Warnings.Add("Р/М не вдалося визначити.");

        if (string.IsNullOrWhiteSpace(result.Seed.DistrictRaw))
            result.Warnings.Add("Район не знайдено у рядку р/м.");

        if (string.IsNullOrWhiteSpace(result.Seed.Note))
            result.Warnings.Add("Текст комунікації не знайдено. Перевірте блок.");

        return result;
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            builder.Append(ch switch
            {
                '\u00A0' => ' ',
                '\uFEFF' => ' ',
                '\u200B' => ' ',
                '\u200C' => ' ',
                '\u200D' => ' ',
                '\r' => '\n',
                _ => ch
            });
        }

        return builder.ToString();
    }

    private static string CleanLine(string value)
        => value.Replace('\t', ' ').Trim();

    private static int FindDateLineIndex(List<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (FindDateLineRegex().IsMatch(lines[i]))
                return i;
        }

        return -1;
    }

    private static int NextNonEmptyLineIndex(List<string> lines, int startIndex)
    {
        for (var i = startIndex; i < lines.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]) && !IsSeparator(lines[i]))
                return i;
        }

        return -1;
    }

    private static List<string> ExtractParticipants(List<string> lines, int startIndex, out int bodyStartIndex)
    {
        bodyStartIndex = -1;
        var participants = new List<string>();

        for (var i = startIndex; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (LooksLikeDialogue(line) || line.StartsWith("Коментар:", StringComparison.OrdinalIgnoreCase))
            {
                bodyStartIndex = i;
                break;
            }

            foreach (var rawPart in line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var participant = CleanParticipant(rawPart);
                if (string.IsNullOrWhiteSpace(participant))
                    continue;

                participants.Add(participant);
            }
        }

        return participants;
    }

    private static string BuildBody(IReadOnlyList<string> lines, int startIndex)
    {
        var bodyLines = lines
            .Skip(startIndex)
            .Where(x => !IsSeparator(x))
            .Select(CleanBodyLine)
            .Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join(Environment.NewLine, bodyLines);
    }

    private static string CleanBodyLine(string line)
        => CleanValue(line) ?? string.Empty;

    private static bool LooksLikeDialogue(string line)
    {
        return line.StartsWith('-') || line.Contains("прием", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSeparator(string line)
        => SeparatorRegex().IsMatch(line);

    private static (string? rmRaw, string? districtRaw) SplitRmAndDistrict(string? rmLine)
    {
        if (string.IsNullOrWhiteSpace(rmLine))
            return (null, null);

        var districtMatch = DistrictRegex().Match(rmLine);
        if (!districtMatch.Success)
            return (CleanValue(rmLine), null);

        var district = CleanValue(districtMatch.Value);
        var rmRaw = CleanValue(rmLine[..districtMatch.Index].Trim(' ', '-', '—', '–', ',', ';'));
        return (rmRaw, district);
    }

    private static string? ParseSourcePost(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = SourcePostRegex().Match(line);
            if (match.Success)
                return CleanValue(match.Groups["post"].Value);
        }

        return null;
    }

    private static List<ObservationRadioTagSuggestionRow> BuildSuggestedTags(string? sourcePost, string? rmRaw, string? districtRaw)
    {
        var result = new List<ObservationRadioTagSuggestionRow>();

        if (!string.IsNullOrWhiteSpace(sourcePost))
        {
            result.Add(new ObservationRadioTagSuggestionRow
            {
                Value = sourcePost.Trim(),
                Kind = TagKind.Keyword,
                Reason = "Пост джерела",
                Applied = false
            });
        }

        if (!string.IsNullOrWhiteSpace(rmRaw) && rmRaw.Contains("БПЛА", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(new ObservationRadioTagSuggestionRow
            {
                Value = "БПЛА",
                Kind = TagKind.Keyword,
                Reason = "Знайдено у р/м",
                Applied = false
            });
        }

        if (!string.IsNullOrWhiteSpace(districtRaw))
        {
            result.Add(new ObservationRadioTagSuggestionRow
            {
                Value = districtRaw.Trim(),
                Kind = TagKind.Location,
                Reason = "Знайдено у районі",
                Applied = false
            });
        }

        return [.. result
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .GroupBy(x => new { Value = x.Value.Trim().ToUpperInvariant(), x.Kind })
            .Select(x => x.First())];
    }

    private static string? CleanValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = value
            .Replace('\u00A0', ' ')
            .Replace('\uFEFF', ' ')
            .Replace('\u200B', ' ')
            .Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static bool IsUnknownParticipant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().ToUpperInvariant();

        return normalized == "НВ"
            || normalized.StartsWith("НВ ")
            || normalized.StartsWith("НВ-")
            || normalized.StartsWith("НВ_");
    }

    private static string CleanParticipant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value
            .Trim()
            .Trim('"', '\'', '«', '»')
            .TrimStart('—', '-', '*', '•', '·')
            .Trim();
    }

    [GeneratedRegex(@"^\s*[-—–_=]{3,}\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"р-н\s+[^()]+(?:\([^)]*\))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DistrictRegex();

    [GeneratedRegex("""Отримано з поста\s+[\"«“](?<post>[^\"»”]+)[\"»”]""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourcePostRegex();
    [GeneratedRegex(@"^\d{2}\.\d{2}\.\d{4},\s*\d{2}:\d{2}:\d{2}$")]
    private static partial Regex FindDateLineRegex();
}
