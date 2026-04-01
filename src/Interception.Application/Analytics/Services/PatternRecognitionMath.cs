//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Analytics.Builders;
using Interception.Domain.Entities;
using Interception.Common.Extensions;

namespace Interception.Application.Analytics.Services;

/// <summary>
/// Спільні helper-методи для pivot/scoring сценаріїв pattern recognition.
/// </summary>
internal static class PatternRecognitionMath
{
    /// <summary>
    /// Обчислює pairwise score між двома невідомими контекстами.
    /// </summary>
    public static (double Score, PatternMatchReasons Reasons) ComputeScore(
        UnknownContext a,
        UnknownContext b,
        PatternRecognitionOptions options)
    {
        var sameFrequency = EqualsNormalized(a.Frequency, b.Frequency);
        var sameVector = EqualsNormalized(a.VectorSignal, b.VectorSignal);
        var samePoint = EqualsNormalized(a.PointSignal, b.PointSignal);
        var sameDivision = EqualsNormalized(a.Division, b.Division);
        var sameRole = EqualsNormalized(a.Role, b.Role);

        var closeInTime = Math.Abs((a.ObservedDate - b.ObservedDate).TotalMinutes)
            <= options.TimeWindowMinutes;

        var sharedPartners = CountSharedPartners(a.KnownPartnerNames, b.KnownPartnerNames)
            >= options.MinSharedPartners;

        var sharedLabels = a.Labels.Count > 0
            && b.Labels.Count > 0
            && a.Labels.Overlaps(b.Labels);

        var roleWeight = Math.Min(options.SharedLabelsWeight + (options.TimeWeight / 2.0), 0.06);
        var totalWeight = options.FrequencyWeight + options.VectorWeight + options.SharedPartnersWeight
                        + options.PointSignalWeight + options.DivisionWeight + options.TimeWeight
                        + options.SharedLabelsWeight + roleWeight;

        var rawScore =
            (sameFrequency ? options.FrequencyWeight : 0) +
            (sameVector ? options.VectorWeight : 0) +
            (sharedPartners ? options.SharedPartnersWeight : 0) +
            (samePoint ? options.PointSignalWeight : 0) +
            (sameDivision ? options.DivisionWeight : 0) +
            (closeInTime ? options.TimeWeight : 0) +
            (sharedLabels ? options.SharedLabelsWeight : 0) +
            (sameRole ? roleWeight : 0);

        var score = totalWeight <= 0 ? 0 : rawScore / totalWeight;

        return (score, new PatternMatchReasons
        {
            SameFrequency = sameFrequency,
            SameVector = sameVector,
            SamePointSignal = samePoint,
            SameDivision = sameDivision,
            CloseInTime = closeInTime,
            SharedPartners = sharedPartners,
            SharedLabels = sharedLabels
        });
    }

    /// <summary>
    /// Перевіряє, чи кандидат не походить з того самого observation, що й уже включений член групи.
    /// </summary>
    public static bool HasSameObservationMember(UnknownContext candidate, IEnumerable<UnknownContext> members) =>
        members.Any(x => x.MessageId == candidate.MessageId);

    /// <summary>
    /// Обчислює, наскільки кандидат підходить до вже зібраної групи.
    /// </summary>
    public static (double Score, PatternMatchReasons Reasons) ComputeGroupFit(
        UnknownContext candidate,
        List<UnknownContext> members,
        PatternRecognitionOptions options)
    {
        if (members.Count == 0)
            return (0.0, new PatternMatchReasons());

        if (HasSameObservationMember(candidate, members))
            return (0.0, new PatternMatchReasons());

        var results = members
            .Select(member => ComputeScore(candidate, member, options))
            .ToList();

        var strongMatches = results
            .Where(x => x.Score >= options.MinConfidenceScore)
            .ToList();

        var requiredMatches = members.Count == 1
            ? 1
            : (int)Math.Ceiling(members.Count / 2.0);

        if (strongMatches.Count < requiredMatches)
            return (0.0, new PatternMatchReasons());

        return (
            strongMatches.Average(x => x.Score),
            AggregateReasons([.. strongMatches.Select(x => x.Reasons)]));
    }

    /// <summary>
    /// Перераховує score групи по всіх допустимих парах її учасників.
    /// </summary>
    public static (double Score, PatternMatchReasons Reasons) RecalculateGroupScore(
        List<UnknownContext> members,
        PatternRecognitionOptions options)
    {
        if (members.Count < 2)
            return (0.0, new PatternMatchReasons());

        var pairResults = new List<(double Score, PatternMatchReasons Reasons)>();

        for (var i = 0; i < members.Count; i++)
        {
            for (var j = i + 1; j < members.Count; j++)
            {
                if (members[i].MessageId == members[j].MessageId)
                    continue;

                pairResults.Add(ComputeScore(members[i], members[j], options));
            }
        }

        if (pairResults.Count == 0)
            return (0.0, new PatternMatchReasons());

        return (
            pairResults.Average(x => x.Score),
            AggregateReasons([.. pairResults.Select(x => x.Reasons)]));
    }

    /// <summary>
    /// Агрегує причини по більшості pairwise результатів.
    /// </summary>
    public static PatternMatchReasons AggregateReasons(IReadOnlyList<PatternMatchReasons> reasons)
    {
        if (reasons.Count == 0)
            return new PatternMatchReasons();

        var threshold = (int)Math.Ceiling(reasons.Count / 2.0);

        return new PatternMatchReasons
        {
            SameFrequency = reasons.Count(r => r.SameFrequency) >= threshold,
            SameVector = reasons.Count(r => r.SameVector) >= threshold,
            SamePointSignal = reasons.Count(r => r.SamePointSignal) >= threshold,
            SameDivision = reasons.Count(r => r.SameDivision) >= threshold,
            CloseInTime = reasons.Count(r => r.CloseInTime) >= threshold,
            SharedPartners = reasons.Count(r => r.SharedPartners) >= threshold,
            SharedLabels = reasons.Count(r => r.SharedLabels) >= threshold,
        };
    }

    /// <summary>
    /// Будує pivot-профіль по observation-знімках.
    /// </summary>
    public static GroupProfile BuildGroupProfile(
        List<GroupObservationSnapshot> observations,
        PatternRecognitionOptions options)
    {
        if (observations.Count == 0)
        {
            return new GroupProfile(
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                DateTime.MinValue,
                DateTime.MinValue,
                null,
                null,
                null,
                null);
        }

        var minDate = observations.Min(x => x.ObservedDate);
        var maxDate = observations.Max(x => x.ObservedDate);

        return new GroupProfile(
            Frequencies: BuildCountMap(observations.Select(x => x.Frequency)),
            VectorSignals: BuildCountMap(observations.Select(x => x.VectorSignal)),
            Divisions: BuildCountMap(observations.Select(x => x.Division)),
            Roles: BuildCountMap(observations.Select(x => x.Role)),
            Labels: BuildCountMap(observations.SelectMany(x => x.Labels)),
            FrequencyDivisionPairs: BuildPairCountMap(observations.Select(x => (x.Frequency, x.Division))),
            VectorDivisionPairs: BuildPairCountMap(observations.Select(x => (x.VectorSignal, x.Division))),
            RoleDivisionPairs: BuildPairCountMap(observations.Select(x => (x.Role, x.Division))),
            LabelDivisionPairs: BuildLabelDivisionPairCountMap(observations),
            WindowStart: minDate.AddMinutes(-options.TimeWindowMinutes),
            WindowEnd: maxDate.AddMinutes(options.TimeWindowMinutes),
            DominantFrequency: GetDominantValue(observations.Select(x => x.Frequency)),
            DominantVector: GetDominantValue(observations.Select(x => x.VectorSignal)),
            DominantDivision: GetDominantValue(observations.Select(x => x.Division)),
            DominantRole: GetDominantValue(observations.Select(x => x.Role)));
    }

    /// <summary>
    /// Рахує overlap між двома count-map словниками.
    /// </summary>
    public static double ComputeOverlapScore(IReadOnlyDictionary<string, int> left, IReadOnlyDictionary<string, int> right)
    {
        if (left.Count == 0 || right.Count == 0)
            return 0.0;

        var keys = new HashSet<string>(left.Keys, StringComparer.OrdinalIgnoreCase);
        keys.UnionWith(right.Keys);

        var numerator = 0;
        var denominator = 0;

        foreach (var key in keys)
        {
            var leftCount = left.TryGetValue(key, out var l) ? l : 0;
            var rightCount = right.TryGetValue(key, out var r) ? r : 0;
            numerator += Math.Min(leftCount, rightCount);
            denominator += Math.Max(leftCount, rightCount);
        }

        return denominator == 0 ? 0.0 : (double)numerator / denominator;
    }

    /// <summary>
    /// Обчислює середнє лише по ненульових величинах.
    /// </summary>
    public static double ComputeAverageNonZero(params double[] values)
    {
        var nonZero = values.Where(v => v > 0).ToList();
        return nonZero.Count == 0 ? 0.0 : nonZero.Average();
    }

    /// <summary>
    /// Повертає спільні значення для відображення оператору.
    /// </summary>
    public static IReadOnlyList<string> GetCommonValues(
        IReadOnlyDictionary<string, int> pivotLeft,
        IEnumerable<string?> originalValues,
        int take)
    {
        return [.. originalValues
            .Select(x => SemanticValueExtensions.NormalizeMeaningfulOrNull(x))
            .Where(x => x is not null)
            .Select(x => new { Original = x!, Key = SemanticValueExtensions.NormalizeKeyOrNull(x) })
            .Where(x => x.Key is not null && pivotLeft.ContainsKey(x.Key))
            .Select(x => x.Original)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(take)];
    }

    /// <summary>
    /// Створює map із normalized значень та їхньої кількості.
    /// </summary>
    public static Dictionary<string, int> BuildCountMap(IEnumerable<string?> values)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var key = SemanticValueExtensions.NormalizeKeyOrNull(value);
            if (key is null)
                continue;

            map[key] = map.TryGetValue(key, out var current)
                ? current + 1
                : 1;
        }

        return map;
    }

    /// <summary>
    /// Створює count-map для бінарних пар ознак.
    /// </summary>
    public static Dictionary<string, int> BuildPairCountMap(IEnumerable<(string? Left, string? Right)> pairs)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (left, right) in pairs)
        {
            var key = BuildPairKey(left, right);
            if (key is null)
                continue;

            map[key] = map.TryGetValue(key, out var current)
                ? current + 1
                : 1;
        }

        return map;
    }

    /// <summary>
    /// Створює count-map для пар мітка × підрозділ.
    /// </summary>
    public static Dictionary<string, int> BuildLabelDivisionPairCountMap(IEnumerable<GroupObservationSnapshot> observations)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var observation in observations)
        {
            var division = SemanticValueExtensions.NormalizeKeyOrNull(observation.Division);
            if (division is null)
                continue;

            foreach (var label in observation.Labels)
            {
                var labelKey = SemanticValueExtensions.NormalizeKeyOrNull(label);
                if (labelKey is null)
                    continue;

                var pairKey = $"{labelKey}|{division}";
                map[pairKey] = map.TryGetValue(pairKey, out var current)
                    ? current + 1
                    : 1;
            }
        }

        return map;
    }

    /// <summary>
    /// Повертає ключ пари у normalized вигляді.
    /// </summary>
    public static string? BuildPairKey(string? left, string? right)
    {
        var leftKey = SemanticValueExtensions.NormalizeKeyOrNull(left);
        var rightKey = SemanticValueExtensions.NormalizeKeyOrNull(right);

        return leftKey is null || rightKey is null
            ? null
            : $"{leftKey}|{rightKey}";
    }

    /// <summary>
    /// Перетворює список строк у normalized set.
    /// </summary>
    public static HashSet<string> ToNormalizedSet(IEnumerable<string?> values)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var key = SemanticValueExtensions.NormalizeKeyOrNull(value);
            if (key is not null)
                set.Add(key);
        }

        return set;
    }

    /// <summary>
    /// Порівнює два значення після normalized трансформації.
    /// </summary>
    public static bool EqualsNormalized(string? left, string? right)
    {
        var leftKey = SemanticValueExtensions.NormalizeKeyOrNull(left);
        var rightKey = SemanticValueExtensions.NormalizeKeyOrNull(right);

        return leftKey is not null &&
               rightKey is not null &&
               string.Equals(leftKey, rightKey, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Повертає домінуюче значення з набору.
    /// </summary>
    public static string? GetDominantValue(IEnumerable<string?> values)
    {
        var groups = values
            .Select(SemanticValueExtensions.NormalizeMeaningfulOrNull)
            .Where(v => v is not null)
            .Select(v => v!)
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return groups.Count == 0 ? null : groups[0].First();
    }

    private static int CountSharedPartners(HashSet<string> partnersA, HashSet<string> partnersB)
    {
        if (partnersA.Count == 0 || partnersB.Count == 0)
            return 0;

        var setA = new HashSet<string>(partnersA, StringComparer.OrdinalIgnoreCase);
        return partnersB.Count(setA.Contains);
    }
}
