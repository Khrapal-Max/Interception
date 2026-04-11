//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Entities;
using Interception.UI.Domain.Policies;
using Interception.UI.Domain.Records;
using Interception.UI.Extensions;

namespace Interception.UI.Domain.Services;

/// <summary>
/// Доменний сервіс групування невідомих учасників.
/// Інкапсулює scoring + specification рішення для join/create/recalculate.
/// </summary>
public static class ParticipantCandidateGroupingDomainService
{
    public static bool HasSameObservationMember(UnknownContext candidate, IEnumerable<UnknownContext> members) =>
        members.Any(x => x.MessageId == candidate.MessageId);

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

    public static (double Score, PatternMatchReasons Reasons) ComputeGroupFit(
        UnknownContext candidate,
        List<UnknownContext> members,
        PatternRecognitionOptions options)
    {
        if (members.Count == 0 || HasSameObservationMember(candidate, members))
            return (0.0, new PatternMatchReasons());

        var results = members.Select(member => ComputeScore(candidate, member, options)).ToList();
        var strongMatches = results.Where(x => x.Score >= options.MinConfidenceScore).ToList();

        if (!ParticipantCandidateGroupingPolicy.CanJoinGroup(strongMatches.Count, members.Count))
            return (0.0, new PatternMatchReasons());

        return (strongMatches.Average(x => x.Score), AggregateReasons([.. strongMatches.Select(x => x.Reasons)]));
    }

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

        return (pairResults.Average(x => x.Score), AggregateReasons([.. pairResults.Select(x => x.Reasons)]));
    }

    public static HashSet<string> ToNormalizedSet(IEnumerable<string?> values)
        => [.. values
            .Select(SemanticValueExtensions.NormalizeKeyOrNull)
            .Where(v => v is not null)
            .Select(v => v!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    public static string? GetDominantValue(IEnumerable<string?> values)
        => values
            .Select(SemanticValueExtensions.NormalizeMeaningfulOrNull)
            .Where(v => v is not null)
            .GroupBy(v => v!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Key)
            .FirstOrDefault();

    private static PatternMatchReasons AggregateReasons(IReadOnlyList<PatternMatchReasons> reasons)
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

    private static bool EqualsNormalized(string? a, string? b)
    {
        var aa = SemanticValueExtensions.NormalizeKeyOrNull(a);
        var bb = SemanticValueExtensions.NormalizeKeyOrNull(b);
        return aa is not null && bb is not null &&
               string.Equals(aa, bb, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountSharedPartners(HashSet<string> left, HashSet<string> right)
        => left.Count == 0 || right.Count == 0
            ? 0
            : left.Count(right.Contains);
}
