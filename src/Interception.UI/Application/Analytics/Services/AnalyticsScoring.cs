//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Enums;
using Interception.UI.Extensions;

namespace Interception.UI.Application.Analytics.Services;

internal static class AnalyticsScoring
{
    public static int CalculatePriorityScore(
        int observationsCount,
        int distinctActionsCount,
        bool stableRm,
        bool stableLayer,
        bool hasNoteOverlap,
        int relatedNodesCount,
        bool hasOpenHypothesis,
        bool recentActivity)
    {
        var score = 0;

        score += observationsCount * 5;

        if (observationsCount > 3) score += 10;
        if (observationsCount > 7) score += 20;

        if (distinctActionsCount > 0 && distinctActionsCount <= 3) score += 15;
        if (stableRm) score += 10;
        if (stableLayer) score += 10;
        if (hasNoteOverlap) score += 10;
        if (relatedNodesCount >= 3) score += 15;
        if (!hasOpenHypothesis) score += 20;
        if (recentActivity) score += 10;

        return score;
    }

    public static string ToPriorityBand(int score)
        => score switch
        {
            >= 80 => "high",
            >= 45 => "medium",
            _ => "low"
        };

    public static int CalculateContextScore(
        bool sameAction,
        bool sameRm,
        bool sameLayer,
        bool sameDistrict,
        bool noteOverlap,
        bool sameRelatedNode,
        bool closeInTime)
    {
        var score = 0;

        if (sameAction) score += 60;
        if (sameRm) score += 40;
        if (sameLayer) score += 35;
        if (sameDistrict) score += 25;
        if (noteOverlap) score += 20;
        if (sameRelatedNode) score += 20;
        if (closeInTime) score += 10;

        return score;
    }

    public static string ToRelationKind(int score, bool direct)
    {
        if (direct) return "direct";
        if (score >= 60) return "strong-indirect";
        if (score >= 30) return "medium-indirect";
        return "weak";
    }

    public static string BuildUnknownDisplay(
        string? labelRaw,
        int ordinal,
        DateOnly? observedDate = null,
        string? rmRaw = null)
    {
        if (!string.IsNullOrWhiteSpace(labelRaw))
            return labelRaw.Trim();

        if (!string.IsNullOrWhiteSpace(rmRaw) && observedDate is not null)
            return $"НВ {ordinal} · {observedDate:dd.MM} · {rmRaw}";

        if (observedDate is not null)
            return $"НВ {ordinal} · {observedDate:dd.MM}";

        return $"НВ {ordinal}";
    }

    public static HashSet<string> ExtractNoteTokens(IEnumerable<string?> notes)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var note in notes)
        {
            var normalized = TextNorm.Normalize(note);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length >= 4)
                    result.Add(token);
            }
        }

        return result;
    }

    public static bool HasNoteOverlap(string? note, HashSet<string> tokens)
    {
        if (tokens.Count == 0)
            return false;

        var normalized = TextNorm.Normalize(note);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length >= 4 && tokens.Contains(token))
                return true;
        }

        return false;
    }

    public static AnalyticsCandidateReadiness CalculateReadiness(
        int observationsCount,
        int distinctActionsCount,
        bool stableRm,
        bool stableLayer,
        bool hasNoteOverlap,
        bool hasOpenHypothesis,
        bool hasResolvedActor)
    {
        if (hasResolvedActor)
            return AnalyticsCandidateReadiness.LikelyFactPattern;

        if (hasOpenHypothesis)
            return AnalyticsCandidateReadiness.LikelyExistingHypothesis;

        var strongContext = stableRm || stableLayer || hasNoteOverlap;
        var stablePattern = distinctActionsCount > 0 && distinctActionsCount <= 3;

        if (observationsCount >= 3 && (strongContext || stablePattern))
            return AnalyticsCandidateReadiness.EnoughForHypothesis;

        return AnalyticsCandidateReadiness.Insufficient;
    }

    public static string? Normalize(string? value) => TextNorm.Normalize(value);
}