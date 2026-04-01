//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Abstractions;
using Interception.Application.Analytics.Abstractions;
using Interception.Application.Analytics.Builders;
using Interception.Application.Analytics.Dtos;
using Interception.Common.Extensions;
using Interception.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Interception.Application.Analytics.Services;

/// <summary>
/// Реалізація non-person suggestions: підрозділ / контекст / середовище групи.
/// </summary>
public sealed class ContextSuggestionService(
    IAppDbContextFactory dbFactory,
    IOptions<PatternRecognitionOptions> options) : IContextSuggestionService
{
    private readonly PatternRecognitionOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CandidateContextSuggestionDto>> GetContextSuggestionsAsync(
        Guid groupId,
        int take = 3,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var group = await db.ParticipantCandidateGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
            return [];

        return await BuildContextSuggestionsAsync(db, group, take, ct);
    }

    /// <summary>
    /// Будує контекстні suggestions для конкретної групи кандидатів.
    /// </summary>
    internal async Task<IReadOnlyList<CandidateContextSuggestionDto>> BuildContextSuggestionsAsync(
        IAppDbContext db,
        ParticipantCandidateGroup group,
        int take,
        CancellationToken ct)
    {
        var groupParticipantIds = group.ParticipantRefs
            .Select(r => (Guid)r.ParticipantId)
            .ToHashSet();

        if (groupParticipantIds.Count == 0)
            return [];

        var groupRows = await db.InterceptionMessageParticipants
            .Where(p => groupParticipantIds.Contains(p.Id))
            .Select(p => new
            {
                p.Role,
                p.InterceptionMessage.Frequency,
                p.InterceptionMessage.VectorSignal,
                p.InterceptionMessage.Division,
                p.InterceptionMessage.ObservedDate,
                Labels = p.InterceptionMessage.Labels.Select(l => l.NameLabel).ToList()
            })
            .AsNoTracking()
            .ToListAsync(ct);

        if (groupRows.Count == 0)
            return [];

        var groupProfile = PatternRecognitionMath.BuildGroupProfile(
            [.. groupRows.Select(x => new GroupObservationSnapshot(
                x.Frequency,
                x.VectorSignal,
                x.Division,
                x.Role,
                x.ObservedDate,
                x.Labels))],
            _options);

        var contextProfiles = await BuildContextProfilesAsync(db, ct);

        return [.. contextProfiles
            .Select(context =>
            {
                var frequencyScore = PatternRecognitionMath.ComputeOverlapScore(groupProfile.Frequencies, context.Pivot.Frequencies);
                var vectorScore = PatternRecognitionMath.ComputeOverlapScore(groupProfile.VectorSignals, context.Pivot.VectorSignals);
                var divisionScore = PatternRecognitionMath.ComputeOverlapScore(groupProfile.Divisions, context.Pivot.Divisions);
                var roleScore = PatternRecognitionMath.ComputeOverlapScore(groupProfile.Roles, context.Pivot.Roles);
                var labelScore = PatternRecognitionMath.ComputeOverlapScore(groupProfile.Labels, context.Pivot.Labels);
                var pivotScore = PatternRecognitionMath.ComputeAverageNonZero(
                    PatternRecognitionMath.ComputeOverlapScore(groupProfile.FrequencyDivisionPairs, context.Pivot.FrequencyDivisionPairs),
                    PatternRecognitionMath.ComputeOverlapScore(groupProfile.VectorDivisionPairs, context.Pivot.VectorDivisionPairs),
                    PatternRecognitionMath.ComputeOverlapScore(groupProfile.RoleDivisionPairs, context.Pivot.RoleDivisionPairs),
                    PatternRecognitionMath.ComputeOverlapScore(groupProfile.LabelDivisionPairs, context.Pivot.LabelDivisionPairs));

                var hasConfirmedContext = context.RelatedResolvedNames.Count > 0;
                var confirmedScore = !hasConfirmedContext
                    ? 0.0
                    : Math.Min(1.0, (double)context.ConfirmedGroupCount / Math.Max(1, context.SeenCount));

                if (frequencyScore <= 0 && vectorScore <= 0 && divisionScore <= 0 && roleScore <= 0 && labelScore <= 0 && pivotScore <= 0)
                    return null;

                var roleWeight = Math.Min(_options.DivisionWeight + _options.SharedLabelsWeight, 0.10);
                var pivotWeight = Math.Min(_options.SharedPartnersWeight, 0.18);
                var confirmedWeight = Math.Min(_options.SharedPartnersWeight / 2.0, 0.10);
                var totalWeight = _options.FrequencyWeight + _options.VectorWeight + _options.DivisionWeight
                                + roleWeight + _options.SharedLabelsWeight + pivotWeight + confirmedWeight;

                var rawScore =
                    (frequencyScore * _options.FrequencyWeight) +
                    (vectorScore * _options.VectorWeight) +
                    (divisionScore * _options.DivisionWeight) +
                    (roleScore * roleWeight) +
                    (labelScore * _options.SharedLabelsWeight) +
                    (pivotScore * pivotWeight) +
                    (confirmedScore * confirmedWeight);

                var score = totalWeight <= 0 ? 0.0 : Math.Min(rawScore / totalWeight, 1.0);

                return new CandidateContextSuggestionDto
                {
                    Division = context.Division,
                    SuggestedRole = context.Pivot.DominantRole,
                    MatchScore = score,
                    SeenCount = context.SeenCount,
                    ConfirmedGroupCount = context.ConfirmedGroupCount,
                    CommonLabels = PatternRecognitionMath.GetCommonValues(groupProfile.Labels, context.Pivot.Labels.Keys, 5),
                    RelatedKnownNames = [.. context.RelatedKnownNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(5)],
                    RelatedResolvedNames = [.. context.RelatedResolvedNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(5)],
                    Reasons = new CandidateContextReasonsDto
                    {
                        SameFrequency = frequencyScore > 0,
                        SameVector = vectorScore > 0,
                        SameDivision = divisionScore > 0,
                        SameRole = roleScore > 0,
                        SharedLabels = labelScore > 0,
                        HasConfirmedContext = hasConfirmedContext,
                        PivotIntersection = pivotScore > 0
                    }
                };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.Reasons.MatchCount)
            .ThenByDescending(x => x.ConfirmedGroupCount)
            .ThenByDescending(x => x.SeenCount)
            .ThenBy(x => x.Division, StringComparer.OrdinalIgnoreCase)
            .Take(take)];
    }

    /// <summary>
    /// Будує базові профілі контекстів із known та confirmed історії.
    /// </summary>
    private async Task<IReadOnlyList<ContextProfile>> BuildContextProfilesAsync(IAppDbContext db, CancellationToken ct)
    {
        var knownRows = await db.InterceptionMessageParticipants
            .Where(p => !p.IsUnknown && p.Name != null && p.InterceptionMessage.Division != null)
            .Select(p => new
            {
                Name = p.Name!,
                p.Role,
                Division = p.InterceptionMessage.Division!,
                p.InterceptionMessage.Frequency,
                p.InterceptionMessage.VectorSignal,
                p.InterceptionMessage.ObservedDate,
                Labels = p.InterceptionMessage.Labels.Select(l => l.NameLabel).ToList()
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var confirmedGroups = await db.ParticipantCandidateGroups
            .Where(g => g.Status == Domain.Enums.CandidateGroupStatus.Confirmed && g.SuggestedDivision != null)
            .AsNoTracking()
            .ToListAsync(ct);

        var confirmedMessageIds = confirmedGroups
            .SelectMany(g => g.ParticipantRefs.Select(r => r.MessageId))
            .ToHashSet();

        var confirmedMessages = confirmedMessageIds.Count == 0
            ? []
            : await db.InterceptionMessages
                .Where(m => confirmedMessageIds.Contains(m.Id))
                .Select(m => new
                {
                    m.Id,
                    m.Frequency,
                    m.VectorSignal,
                    m.Division,
                    m.ObservedDate,
                    Labels = m.Labels.Select(l => l.NameLabel).ToList()
                })
                .AsNoTracking()
                .ToDictionaryAsync(
                    m => m.Id,
                    m => new GroupObservationSnapshot(m.Frequency, m.VectorSignal, m.Division, null, m.ObservedDate, m.Labels),
                    ct);

        var buckets = new Dictionary<string, ContextProfileBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in knownRows)
        {
            var normalizedDivision = StringSemanticValueExtensions.NormalizeMeaningfulOrNull(row.Division);
            var key = StringSemanticValueExtensions.NormalizeKeyOrNull(row.Division);
            if (key is null || normalizedDivision is null)
                continue;

            var builder = GetOrCreateContextProfileBuilder(buckets, key, normalizedDivision);
            builder.Observations.Add(new GroupObservationSnapshot(row.Frequency, row.VectorSignal, row.Division, row.Role, row.ObservedDate, row.Labels));
            builder.RelatedKnownNames.Add(row.Name.Trim());
        }

        foreach (var group in confirmedGroups)
        {
            var normalizedDivision = StringSemanticValueExtensions.NormalizeMeaningfulOrNull(group.SuggestedDivision);
            var key = StringSemanticValueExtensions.NormalizeKeyOrNull(group.SuggestedDivision);
            if (key is null || normalizedDivision is null)
                continue;

            var builder = GetOrCreateContextProfileBuilder(buckets, key, normalizedDivision);
            builder.ConfirmedGroupCount++;

            if (!string.IsNullOrWhiteSpace(group.SuggestedName))
                builder.RelatedResolvedNames.Add(group.SuggestedName.Trim());

            foreach (var messageId in group.ParticipantRefs.Select(r => r.MessageId).Distinct())
            {
                if (!confirmedMessages.TryGetValue(messageId, out var msg))
                    continue;

                builder.Observations.Add(msg with { Role = group.SuggestedRole });
            }
        }

        return [.. buckets.Values
            .Where(x => x.Observations.Count > 0)
            .Select(x => x.Build(PatternRecognitionMath.BuildGroupProfile(x.Observations, _options)))];
    }

    private static ContextProfileBuilder GetOrCreateContextProfileBuilder(
        Dictionary<string, ContextProfileBuilder> buckets,
        string key,
        string division)
    {
        if (buckets.TryGetValue(key, out var builder))
            return builder;

        builder = new ContextProfileBuilder(division);
        buckets[key] = builder;
        return builder;
    }
}
