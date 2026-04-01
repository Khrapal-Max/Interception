//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Analytics.Abstractions;
using Interception.Application.Analytics.Builders;
using Interception.Application.Analytics.Dtos;
using Interception.Domain;
using Interception.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Interception.Application.Analytics.Services;

/// <summary>
/// Реалізація top-N підказок по конкретних відомих особах для групи НВ.
/// </summary>
public sealed class KnownParticipantSuggestionService(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<PatternRecognitionOptions> options) : IKnownParticipantSuggestionService
{
    private readonly PatternRecognitionOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<IReadOnlyList<KnownParticipantSuggestionDto>> GetKnownSuggestionsAsync(
        Guid groupId,
        int take = 3,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var group = await db.ParticipantCandidateGroups
          .AsNoTracking()
          .Include(g => g.ParticipantRefs)
          .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null)
            return [];

        return await BuildKnownSuggestionsAsync(db, group, take, ct);
    }

    /// <summary>
    /// Будує підказки по відомих особах безпосередньо з контексту групи та історії.
    /// </summary>
    internal async Task<IReadOnlyList<KnownParticipantSuggestionDto>> BuildKnownSuggestionsAsync(
        AppDbContext db,
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
                Labels = p.InterceptionMessage.Labels.Select(l => l.NameLabel).ToList(),
                KnownParticipants = p.InterceptionMessage.Participants
                    .Where(x => !x.IsUnknown && x.Name != null)
                    .Select(x => x.Name!)
                    .ToList()
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

        var blockedKnownNames = groupRows
            .SelectMany(x => x.KnownParticipants)
            .Select(Extensions.SemanticValue.NormalizeMeaningfulOrNull)
            .Where(x => x is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var knownRows = await db.InterceptionMessageParticipants
            .Where(p => !p.IsUnknown && p.Name != null)
            .Select(p => new
            {
                Name = p.Name!,
                p.Role,
                p.InterceptionMessage.Frequency,
                p.InterceptionMessage.VectorSignal,
                p.InterceptionMessage.Division,
                p.InterceptionMessage.ObservedDate,
                Labels = p.InterceptionMessage.Labels.Select(l => l.NameLabel).ToList()
            })
            .AsNoTracking()
            .ToListAsync(ct);

        if (knownRows.Count == 0)
            return [];

        return [.. knownRows
            .Where(x =>
            {
                var normalized = Extensions.SemanticValue.NormalizeMeaningfulOrNull(x.Name);
                return normalized is not null && !blockedKnownNames.Contains(normalized);
            })
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var observations = g.Select(x => new GroupObservationSnapshot(
                    x.Frequency,
                    x.VectorSignal,
                    x.Division,
                    x.Role,
                    x.ObservedDate,
                    x.Labels)).ToList();

                var candidateProfile = PatternRecognitionMath.BuildGroupProfile(observations, _options);

                var frequencyScore = PatternRecognitionMath.ComputeOverlapScore(groupProfile.Frequencies, candidateProfile.Frequencies);
                var vectorScore = PatternRecognitionMath.ComputeOverlapScore(groupProfile.VectorSignals, candidateProfile.VectorSignals);
                var divisionScore = PatternRecognitionMath.ComputeOverlapScore(groupProfile.Divisions, candidateProfile.Divisions);
                var roleScore = PatternRecognitionMath.ComputeOverlapScore(groupProfile.Roles, candidateProfile.Roles);
                var labelScore = PatternRecognitionMath.ComputeOverlapScore(groupProfile.Labels, candidateProfile.Labels);
                var pivotScore = PatternRecognitionMath.ComputeAverageNonZero(
                    PatternRecognitionMath.ComputeOverlapScore(groupProfile.FrequencyDivisionPairs, candidateProfile.FrequencyDivisionPairs),
                    PatternRecognitionMath.ComputeOverlapScore(groupProfile.VectorDivisionPairs, candidateProfile.VectorDivisionPairs),
                    PatternRecognitionMath.ComputeOverlapScore(groupProfile.RoleDivisionPairs, candidateProfile.RoleDivisionPairs),
                    PatternRecognitionMath.ComputeOverlapScore(groupProfile.LabelDivisionPairs, candidateProfile.LabelDivisionPairs));

                var timeScore = observations.Count == 0
                    ? 0.0
                    : (double)observations.Count(x => x.ObservedDate >= groupProfile.WindowStart && x.ObservedDate <= groupProfile.WindowEnd) / observations.Count;

                if (frequencyScore <= 0 && vectorScore <= 0 && divisionScore <= 0 && roleScore <= 0 && labelScore <= 0 && pivotScore <= 0)
                    return null;

                var roleWeight = Math.Min(_options.DivisionWeight + _options.SharedLabelsWeight, 0.10);
                var pivotWeight = Math.Min(_options.SharedPartnersWeight, 0.18);
                var totalWeight = _options.FrequencyWeight + _options.VectorWeight + _options.DivisionWeight
                                + roleWeight + _options.SharedLabelsWeight + _options.TimeWeight + pivotWeight;

                var rawScore =
                    (frequencyScore * _options.FrequencyWeight) +
                    (vectorScore * _options.VectorWeight) +
                    (divisionScore * _options.DivisionWeight) +
                    (roleScore * roleWeight) +
                    (labelScore * _options.SharedLabelsWeight) +
                    (timeScore * _options.TimeWeight) +
                    (pivotScore * pivotWeight);

                var score = totalWeight <= 0 ? 0.0 : Math.Min(rawScore / totalWeight, 1.0);

                return new KnownParticipantSuggestionDto
                {
                    Name = g.Key,
                    Role = candidateProfile.DominantRole,
                    Division = candidateProfile.DominantDivision,
                    MatchScore = score,
                    SeenCount = observations.Count,
                    CommonLabels = PatternRecognitionMath.GetCommonValues(groupProfile.Labels, observations.SelectMany(x => x.Labels), 5),
                    Reasons = new KnownSuggestionReasonsDto
                    {
                        SameFrequency = frequencyScore > 0,
                        SameVector = vectorScore > 0,
                        SameDivision = divisionScore > 0,
                        SameRole = roleScore > 0,
                        CloseInTime = timeScore > 0,
                        SharedLabels = labelScore > 0,
                        PivotIntersection = pivotScore > 0,
                    }
                };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.Reasons.MatchCount)
            .ThenByDescending(x => x.SeenCount)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(take)];
    }
}
