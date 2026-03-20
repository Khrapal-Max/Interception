using System.Globalization;
using System.Text.RegularExpressions;
using Interception.UI.Components.Pages.Observations.Models;
using Interception.UI.Domain.Enums;

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
        var lines = normalizedText.Split('\n')
            .Select(x => x.TrimEnd())
            .ToArray();

        var dateIndex = FindDateLineIndex(lines);
        if (dateIndex < 0)
        {
            return new ObservationRadioParseResult
            {
                ErrorMessage = "Не знайдено рядок з датою та часом у форматі 20.03.2026, 19:19:40."
            };
        }

        var observedDateText = lines[dateIndex].Trim();
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

        var frequency = lines[frequencyIndex].Trim();
        var rmLine = lines[rmIndex].Trim();

        var participants = ExtractParticipants(lines, rmIndex + 1, out var bodyStartIndex);
        var body = bodyStartIndex >= 0
            ? string.Join(Environment.NewLine, lines.Skip(bodyStartIndex)).Trim()
            : string.Empty;

        var (rmRaw, districtRaw) = SplitRmAndDistrict(rmLine);
        var sourcePost = ParseSourcePost(lines);

        var result = new ObservationRadioParseResult
        {
            IsSuccess = true,
            SourcePost = sourcePost,
            SuggestedActionRaw = DefaultAction,
            Draft = new ObservationEditorModel
            {
                ObservedDate = observedDate,
                ActionRaw = DefaultAction,
                Layer = Clean(frequency),
                RmRaw = Clean(rmRaw),
                DistrictRaw = Clean(districtRaw),
                Note = Clean(body)
            }
        };

        if (participants.Count == 0)
        {
            result.Warnings.Add("Не знайдено окремий блок учасників. Перевірте текст.");
        }
        else
        {
            result.Draft.Participants = participants
                .Select((value, index) => new ObservationParticipantEditorRow
                {
                    LabelRaw = value,
                    Ordinal = index + 1,
                    IsUnknown = false
                })
                .ToList();
        }

        result.SuggestedTags = BuildSuggestedTags(sourcePost, rmRaw, districtRaw);

        if (string.IsNullOrWhiteSpace(result.Draft.Layer))
            result.Warnings.Add("Частоту не вдалося визначити.");

        if (string.IsNullOrWhiteSpace(result.Draft.RmRaw))
            result.Warnings.Add("Р/М не вдалося визначити.");

        if (string.IsNullOrWhiteSpace(result.Draft.DistrictRaw))
            result.Warnings.Add("Район не знайдено у рядку р/м.");

        if (string.IsNullOrWhiteSpace(result.Draft.Note))
            result.Warnings.Add("Текст комунікації не знайдено. Перевірте формат блоку.");

        return result;
    }

    private static int FindDateLineIndex(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (DateLineRegex().IsMatch(lines[i].Trim()))
                return i;
        }

        return -1;
    }

    private static int NextNonEmptyLineIndex(IReadOnlyList<string> lines, int startIndex)
    {
        for (var i = startIndex; i < lines.Count; i++)
        {
            var value = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (IsSeparatorLine(value))
                continue;

            return i;
        }

        return -1;
    }

    private static List<string> ExtractParticipants(IReadOnlyList<string> lines, int startIndex, out int bodyStartIndex)
    {
        var result = new List<string>();
        bodyStartIndex = -1;

        for (var i = startIndex; i < lines.Count; i++)
        {
            var current = lines[i].Trim();

            if (string.IsNullOrWhiteSpace(current))
            {
                if (result.Count == 0)
                    continue;

                bodyStartIndex = NextNonEmptyLineIndex(lines, i + 1);
                return result;
            }

            if (IsSeparatorLine(current))
                continue;

            if (LooksLikeDialogueLine(current))
            {
                bodyStartIndex = i;
                return result;
            }

            foreach (var token in SplitParticipants(current))
            {
                if (!result.Any(x => string.Equals(x, token, StringComparison.OrdinalIgnoreCase)))
                    result.Add(token);
            }
        }

        return result;
    }

    private static (string? RmRaw, string? DistrictRaw) SplitRmAndDistrict(string? rmLine)
    {
        if (string.IsNullOrWhiteSpace(rmLine))
            return (null, null);

        var match = DistrictInRmRegex().Match(rmLine.Trim());
        if (!match.Success)
            return (Clean(rmLine), null);

        var districtRaw = Clean(match.Groups["district"].Value);
        var rmRaw = Clean(rmLine[..match.Index]);

        return (rmRaw, districtRaw);
    }

    private static string? ParseSourcePost(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var match = SourcePostRegex().Match(line.Trim());
            if (match.Success)
                return Clean(match.Groups["post"].Value);
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
                Reason = "Пост / джерело блоку"
            });
        }

        if (!string.IsNullOrWhiteSpace(rmRaw) &&
            rmRaw.Contains("БПЛА", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(new ObservationRadioTagSuggestionRow
            {
                Value = "БПЛА",
                Kind = TagKind.Subdivision,
                Reason = "Зустрічається у рядку р/м"
            });
        }

        if (!string.IsNullOrWhiteSpace(districtRaw))
        {
            result.Add(new ObservationRadioTagSuggestionRow
            {
                Value = districtRaw.Trim(),
                Kind = TagKind.Location,
                Reason = "Витягнуто з району"
            });
        }

        return result;
    }

    private static IEnumerable<string> SplitParticipants(string line)
    {
        return line
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanParticipant)
            .Where(x => !string.IsNullOrWhiteSpace(x))!;
    }

    private static string? CleanParticipant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = value.Trim();
        cleaned = cleaned.Trim('—', '-', '–', '—', '"', '“', '”', '«', '»');
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private static bool LooksLikeDialogueLine(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.TrimStart();
        return trimmed.StartsWith("—") || trimmed.StartsWith("-") || trimmed.StartsWith("–");
    }

    private static bool IsSeparatorLine(string value)
        => value.All(x => x is '-' or '—' or '–' or '_');

    private static string Normalize(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^\d{2}\.\d{2}\.\d{4},\s*\d{2}:\d{2}:\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex DateLineRegex();

    [GeneratedRegex(@"Отримано\s+з\s+поста\s+[""«“]?(?<post>[^""»”]+)[""»”]?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourcePostRegex();

    [GeneratedRegex(@"\((?<district>р-н.+)\)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DistrictInRmRegex();
}
