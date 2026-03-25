//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Domain.Records;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Interception.UI.Application.Interceptions.Services;

/// <summary>Плоский контекст НВ — без навігаційних властивостей EF.</summary>
internal sealed record UnknownContext(
    Guid ParticipantId,
    int Ordinal,
    Guid MessageId,
    string? Frequency,
    string? VectorSignal,
    string? PointSignal,
    string? Division,
    string? Role,
    DateTime ObservedDate,
    HashSet<string> KnownPartnerNames,
    HashSet<string> Labels);

/// <summary>Знімок повідомлення для EnrichGroupsAsync — без навігацій.</summary>
internal sealed record MessageSnapshot(
    Guid Id,
    DateTime ObservedDate,
    string? Frequency,
    string? VectorSignal,
    string? Division);

internal sealed record GroupObservationSnapshot(
    string? Frequency,
    string? VectorSignal,
    string? Division,
    string? Role,
    DateTime ObservedDate,
    List<string> Labels);

internal sealed record GroupProfile(
    IReadOnlyDictionary<string, int> Frequencies,
    IReadOnlyDictionary<string, int> VectorSignals,
    IReadOnlyDictionary<string, int> Divisions,
    IReadOnlyDictionary<string, int> Roles,
    IReadOnlyDictionary<string, int> Labels,
    IReadOnlyDictionary<string, int> FrequencyDivisionPairs,
    IReadOnlyDictionary<string, int> VectorDivisionPairs,
    IReadOnlyDictionary<string, int> RoleDivisionPairs,
    IReadOnlyDictionary<string, int> LabelDivisionPairs,
    DateTime WindowStart,
    DateTime WindowEnd,
    string? DominantFrequency,
    string? DominantVector,
    string? DominantDivision,
    string? DominantRole);

internal sealed record ContextProfile(
    string Division,
    GroupProfile Pivot,
    HashSet<string> RelatedKnownNames,
    HashSet<string> RelatedResolvedNames,
    int SeenCount,
    int ConfirmedGroupCount);

public sealed class PatternRecognitionService(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<PatternRecognitionOptions> options) : IPatternRecognitionService
{
    private readonly PatternRecognitionOptions _opts = options.Value;

    // =========================================================================
    // RunAsync
    // =========================================================================

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var unknownRefs = await db.InterceptionMessageParticipants
            .Where(p => p.IsUnknown)
            .Select(p => new { p.Id, p.InterceptionMessageId, p.Ordinal, p.Role })
            .ToListAsync(ct);

        if (unknownRefs.Count < 2)
            return 0;

        var msgIds = unknownRefs.Select(p => p.InterceptionMessageId).ToHashSet();

        var messages = await db.InterceptionMessages
            .Where(m => msgIds.Contains(m.Id))
            .Select(m => new
            {
                m.Id,
                m.Frequency,
                m.VectorSignal,
                m.PointSignal,
                m.Division,
                m.ObservedDate,
                Labels = m.Labels.Select(l => l.NameLabel).ToList(),
                KnownPartners = m.Participants
                    .Where(p => !p.IsUnknown && p.Name != null)
                    .Select(p => p.Name!)
                    .ToList()
            })
            .ToListAsync(ct);

        var messageMap = messages.ToDictionary(m => m.Id);

        var allCandidates = unknownRefs
            .Where(p => messageMap.ContainsKey(p.InterceptionMessageId))
            .Select(p =>
            {
                var m = messageMap[p.InterceptionMessageId];
                return new UnknownContext(
                    ParticipantId: p.Id,
                    Ordinal: p.Ordinal,
                    MessageId: p.InterceptionMessageId,
                    Frequency: SemanticValue.NormalizeMeaningfulOrNull(m.Frequency),
                    VectorSignal: SemanticValue.NormalizeMeaningfulOrNull(m.VectorSignal),
                    PointSignal: SemanticValue.NormalizeMeaningfulOrNull(m.PointSignal),
                    Division: SemanticValue.NormalizeMeaningfulOrNull(m.Division),
                    Role: SemanticValue.NormalizeMeaningfulOrNull(p.Role),
                    ObservedDate: m.ObservedDate,
                    KnownPartnerNames: ToNormalizedSet(m.KnownPartners),
                    Labels: ToNormalizedSet(m.Labels));
            })
            .ToList();

        var confirmedIds = await db.ParticipantCandidateGroups
            .Where(g => g.Status == CandidateGroupStatus.Confirmed)
            .SelectMany(g => g.ParticipantRefs.Select(r => r.ParticipantId))
            .ToHashSetAsync(ct);

        var openGroups = await db.ParticipantCandidateGroups
            .Where(g => g.Status == CandidateGroupStatus.Open)
            .ToListAsync(ct);

        var openIds = openGroups
            .SelectMany(g => g.ParticipantRefs.Select(r => r.ParticipantId))
            .ToHashSet();

        var freeCandidates = allCandidates
            .Where(p => !confirmedIds.Contains(p.ParticipantId))
            .ToList();

        var unassigned = freeCandidates
            .Where(p => !openIds.Contains(p.ParticipantId))
            .ToList();

        var assigned = new HashSet<Guid>();
        var changed = 0;

        // ------------------------------------------------------------------
        // Крок A — ЗБАГАЧЕННЯ існуючих Open груп
        // ------------------------------------------------------------------
        if (openGroups.Count > 0)
        {
            var openParticipantIds = openGroups
                .SelectMany(g => g.ParticipantRefs.Select(r => r.ParticipantId))
                .ToHashSet();

            var existingUnknownRefs = unknownRefs
                .Where(p => openParticipantIds.Contains(p.Id))
                .ToList();

            var existingContextMap = existingUnknownRefs
                .Where(p => messageMap.ContainsKey(p.InterceptionMessageId))
                .ToDictionary(
                    p => p.Id,
                    p =>
                    {
                        var m = messageMap[p.InterceptionMessageId];
                        return new UnknownContext(
                            ParticipantId: p.Id,
                            Ordinal: p.Ordinal,
                            MessageId: p.InterceptionMessageId,
                            Frequency: SemanticValue.NormalizeMeaningfulOrNull(m.Frequency),
                            VectorSignal: SemanticValue.NormalizeMeaningfulOrNull(m.VectorSignal),
                            PointSignal: SemanticValue.NormalizeMeaningfulOrNull(m.PointSignal),
                            Division: SemanticValue.NormalizeMeaningfulOrNull(m.Division),
                            Role: SemanticValue.NormalizeMeaningfulOrNull(p.Role),
                            ObservedDate: m.ObservedDate,
                            KnownPartnerNames: ToNormalizedSet(m.KnownPartners),
                            Labels: ToNormalizedSet(m.Labels));
                    });

            foreach (var openGroup in openGroups)
            {
                var groupContexts = openGroup.ParticipantRefs
                    .Where(r => existingContextMap.ContainsKey(r.ParticipantId))
                    .Select(r => existingContextMap[r.ParticipantId])
                    .ToList();

                if (groupContexts.Count == 0)
                    continue;

                var groupChanged = false;

                foreach (var newCandidate in unassigned)
                {
                    if (assigned.Contains(newCandidate.ParticipantId))
                        continue;

                    if (HasSameObservationMember(newCandidate, groupContexts))
                        continue;

                    var (Score, Reasons) = ComputeGroupFit(newCandidate, groupContexts);
                    if (Score < _opts.MinConfidenceScore)
                        continue;

                    openGroup.AddRef(new ParticipantRef(
                        newCandidate.MessageId,
                        newCandidate.ParticipantId,
                        newCandidate.Ordinal));

                    assigned.Add(newCandidate.ParticipantId);
                    groupContexts.Add(newCandidate);
                    groupChanged = true;
                }

                if (groupChanged)
                {
                    var (newScore, newReasons) = RecalculateGroupScore(groupContexts);
                    openGroup.UpdateScore(newScore, newReasons);
                    openGroup.UpdateSuggestedDivision(GetDominantValue(groupContexts.Select(x => x.Division)));
                    openGroup.UpdateSuggestedRole(GetDominantValue(groupContexts.Select(x => x.Role)));
                }

                var suggestionChanged = await RefreshOpenGroupSuggestionAsync(db, openGroup, ct);
                if (groupChanged || suggestionChanged)
                    changed++;
            }

            await db.SaveChangesAsync(ct);
        }

        // ------------------------------------------------------------------
        // Крок B — СТВОРЕННЯ нових груп
        // ------------------------------------------------------------------
        var remainingCandidates = unassigned
            .Where(p => !assigned.Contains(p.ParticipantId))
            .ToList();

        if (remainingCandidates.Count < 2)
            return changed;

        var newGroups = new List<ParticipantCandidateGroup>();
        var newAssigned = new HashSet<Guid>();

        for (var i = 0; i < remainingCandidates.Count; i++)
        {
            if (newAssigned.Contains(remainingCandidates[i].ParticipantId))
                continue;

            var group = new List<UnknownContext> { remainingCandidates[i] };

            for (var j = i + 1; j < remainingCandidates.Count; j++)
            {
                if (newAssigned.Contains(remainingCandidates[j].ParticipantId))
                    continue;

                if (HasSameObservationMember(remainingCandidates[j], group))
                    continue;

                var (Score, Reasons) = ComputeGroupFit(remainingCandidates[j], group);
                if (Score < _opts.MinConfidenceScore)
                    continue;

                group.Add(remainingCandidates[j]);
                newAssigned.Add(remainingCandidates[j].ParticipantId);
            }

            if (group.Count < 2)
                continue;

            var (groupScore, groupReasons) = RecalculateGroupScore(group);
            if (groupScore < _opts.MinConfidenceScore)
                continue;

            newAssigned.Add(remainingCandidates[i].ParticipantId);

            var refs = group
                .Select(p => new ParticipantRef(p.MessageId, p.ParticipantId, p.Ordinal))
                .ToList();

            newGroups.Add(ParticipantCandidateGroup.Create(
                refs,
                groupScore,
                groupReasons,
                suggestedRole: GetDominantValue(group.Select(p => p.Role)),
                suggestedDivision: GetDominantValue(group.Select(p => p.Division))));
        }

        if (newGroups.Count > 0)
        {
            foreach (var newGroup in newGroups)
                await RefreshOpenGroupSuggestionAsync(db, newGroup, ct);

            db.ParticipantCandidateGroups.AddRange(newGroups);
            await db.SaveChangesAsync(ct);
            changed += newGroups.Count;
        }

        return changed;
    }

    private async Task<bool> RefreshOpenGroupSuggestionAsync(
        AppDbContext db,
        ParticipantCandidateGroup group,
        CancellationToken ct)
    {
        var beforeName = group.SuggestedName;
        var beforeRole = group.SuggestedRole;
        var beforeDivision = group.SuggestedDivision;

        var best = (await BuildKnownSuggestionsAsync(db, group, 1, ct)).FirstOrDefault();

        group.UpdateSuggestedName(best?.Name);

        if (!string.IsNullOrWhiteSpace(best?.Role))
            group.UpdateSuggestedRole(best.Role);

        if (!string.IsNullOrWhiteSpace(best?.Division))
            group.UpdateSuggestedDivision(best.Division);

        return !string.Equals(beforeName, group.SuggestedName, StringComparison.Ordinal)
            || !string.Equals(beforeRole, group.SuggestedRole, StringComparison.Ordinal)
            || !string.Equals(beforeDivision, group.SuggestedDivision, StringComparison.Ordinal);
    }

    // =========================================================================
    // GetKnownSuggestionsAsync
    // =========================================================================

    public async Task<IReadOnlyList<KnownParticipantSuggestionDto>> GetKnownSuggestionsAsync(
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

        return await BuildKnownSuggestionsAsync(db, group, take, ct);
    }

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

    // =========================================================================
    // Queries
    // =========================================================================

    public async Task<PagedResult<CandidateGroupDto>> GetGroupsByStatusAsync(
        CandidateGroupStatus status,
        int page = 1, int pageSize = 50,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var total = await db.ParticipantCandidateGroups
            .CountAsync(g => g.Status == status, ct);

        var groups = await db.ParticipantCandidateGroups
            .Where(g => g.Status == status)
            .OrderByDescending(g => g.ConfidenceScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        var dtos = await EnrichGroupsAsync(db, groups, ct);
        return new PagedResult<CandidateGroupDto>([.. dtos], total, page, pageSize);
    }

    public async Task<CandidateGroupDto?> GetGroupByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var group = await db.ParticipantCandidateGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (group is null)
            return null;

        var list = await EnrichGroupsAsync(db, [group], ct);
        return list.Count > 0 ? list[0] : null;
    }

    // =========================================================================
    // Scoring helpers
    // =========================================================================

    private (double Score, PatternMatchReasons Reasons) ComputeScore(
        UnknownContext a,
        UnknownContext b)
    {
        var sameFrequency = EqualsNormalized(a.Frequency, b.Frequency);
        var sameVector = EqualsNormalized(a.VectorSignal, b.VectorSignal);
        var samePoint = EqualsNormalized(a.PointSignal, b.PointSignal);
        var sameDivision = EqualsNormalized(a.Division, b.Division);
        var sameRole = EqualsNormalized(a.Role, b.Role);

        var closeInTime = Math.Abs((a.ObservedDate - b.ObservedDate).TotalMinutes)
            <= _opts.TimeWindowMinutes;

        var sharedPartners = CountSharedPartners(a.KnownPartnerNames, b.KnownPartnerNames)
            >= _opts.MinSharedPartners;

        var sharedLabels = a.Labels.Count > 0
            && b.Labels.Count > 0
            && a.Labels.Overlaps(b.Labels);

        var roleWeight = Math.Min(_opts.SharedLabelsWeight + (_opts.TimeWeight / 2.0), 0.06);
        var totalWeight = _opts.FrequencyWeight + _opts.VectorWeight + _opts.SharedPartnersWeight
                        + _opts.PointSignalWeight + _opts.DivisionWeight + _opts.TimeWeight
                        + _opts.SharedLabelsWeight + roleWeight;

        var rawScore =
            (sameFrequency ? _opts.FrequencyWeight : 0) +
            (sameVector ? _opts.VectorWeight : 0) +
            (sharedPartners ? _opts.SharedPartnersWeight : 0) +
            (samePoint ? _opts.PointSignalWeight : 0) +
            (sameDivision ? _opts.DivisionWeight : 0) +
            (closeInTime ? _opts.TimeWeight : 0) +
            (sharedLabels ? _opts.SharedLabelsWeight : 0) +
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

    private static bool HasSameObservationMember(
        UnknownContext candidate,
        IEnumerable<UnknownContext> members) =>
        members.Any(x => x.MessageId == candidate.MessageId);

    /// <summary>
    /// Перевіряє, наскільки кандидат підходить до вже зібраної групи.
    /// Кандидат має співпадати хоча б з половиною членів групи
    /// або з єдиним членом, якщо група поки складається з однієї особи.
    /// </summary>
    private (double Score, PatternMatchReasons Reasons) ComputeGroupFit(
        UnknownContext candidate,
        List<UnknownContext> members)
    {
        if (members.Count == 0)
            return (0.0, new PatternMatchReasons());

        if (HasSameObservationMember(candidate, members))
            return (0.0, new PatternMatchReasons());

        var results = members
            .Select(member => ComputeScore(candidate, member))
            .ToList();

        var strongMatches = results
            .Where(x => x.Score >= _opts.MinConfidenceScore)
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
    /// Перераховує score групи по всіх парах учасників.
    /// Береться середній score, а причини агрегуються більшістю по парах.
    /// </summary>
    private (double Score, PatternMatchReasons Reasons) RecalculateGroupScore(
        List<UnknownContext> members)
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

                pairResults.Add(ComputeScore(members[i], members[j]));
            }
        }

        if (pairResults.Count == 0)
            return (0.0, new PatternMatchReasons());

        return (
            pairResults.Average(x => x.Score),
            AggregateReasons([.. pairResults.Select(x => x.Reasons)]));
    }

    private static PatternMatchReasons AggregateReasons(
        IReadOnlyList<PatternMatchReasons> reasons)
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

    private static int CountSharedPartners(
        HashSet<string> partnersA,
        HashSet<string> partnersB)
    {
        if (partnersA.Count == 0 || partnersB.Count == 0)
            return 0;

        var setA = new HashSet<string>(partnersA, StringComparer.OrdinalIgnoreCase);
        return partnersB.Count(setA.Contains);
    }

    // =========================================================================
    // Known suggestions helpers
    // =========================================================================

    private async Task<IReadOnlyList<KnownParticipantSuggestionDto>> BuildKnownSuggestionsAsync(
        AppDbContext db,
        ParticipantCandidateGroup group,
        int take,
        CancellationToken ct)
    {
        var groupParticipantIds = group.ParticipantRefs
            .Select(r => r.ParticipantId)
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
                Labels = p.InterceptionMessage.Labels
                    .Select(l => l.NameLabel)
                    .ToList(),
                KnownParticipants = p.InterceptionMessage.Participants
                    .Where(x => !x.IsUnknown && x.Name != null)
                    .Select(x => x.Name!)
                    .ToList()
            })
            .AsNoTracking()
            .ToListAsync(ct);

        if (groupRows.Count == 0)
            return [];

        var groupObservations = groupRows
            .Select(x => new GroupObservationSnapshot(
                x.Frequency,
                x.VectorSignal,
                x.Division,
                x.Role,
                x.ObservedDate,
                x.Labels))
            .ToList();

        var groupProfile = BuildGroupProfile(groupObservations);

        var blockedKnownNames = groupRows
            .SelectMany(x => x.KnownParticipants)
            .Select(SemanticValue.NormalizeMeaningfulOrNull)
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
                Labels = p.InterceptionMessage.Labels
                    .Select(l => l.NameLabel)
                    .ToList()
            })
            .AsNoTracking()
            .ToListAsync(ct);

        if (knownRows.Count == 0)
            return [];

        var suggestions = knownRows
            .Where(x =>
            {
                var normalized = SemanticValue.NormalizeMeaningfulOrNull(x.Name);
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

                var candidateProfile = BuildGroupProfile(observations);

                var frequencyScore = ComputeOverlapScore(groupProfile.Frequencies, candidateProfile.Frequencies);
                var vectorScore = ComputeOverlapScore(groupProfile.VectorSignals, candidateProfile.VectorSignals);
                var divisionScore = ComputeOverlapScore(groupProfile.Divisions, candidateProfile.Divisions);
                var roleScore = ComputeOverlapScore(groupProfile.Roles, candidateProfile.Roles);
                var labelScore = ComputeOverlapScore(groupProfile.Labels, candidateProfile.Labels);
                var pivotScore = ComputeAverageNonZero(
                    ComputeOverlapScore(groupProfile.FrequencyDivisionPairs, candidateProfile.FrequencyDivisionPairs),
                    ComputeOverlapScore(groupProfile.VectorDivisionPairs, candidateProfile.VectorDivisionPairs),
                    ComputeOverlapScore(groupProfile.RoleDivisionPairs, candidateProfile.RoleDivisionPairs),
                    ComputeOverlapScore(groupProfile.LabelDivisionPairs, candidateProfile.LabelDivisionPairs));

                var timeScore = observations.Count == 0
                    ? 0.0
                    : (double)observations.Count(x =>
                        x.ObservedDate >= groupProfile.WindowStart &&
                        x.ObservedDate <= groupProfile.WindowEnd) / observations.Count;

                if (frequencyScore <= 0 && vectorScore <= 0 && divisionScore <= 0 && roleScore <= 0 && labelScore <= 0 && pivotScore <= 0)
                    return null;

                var roleWeight = Math.Min(_opts.DivisionWeight + _opts.SharedLabelsWeight, 0.10);
                var pivotWeight = Math.Min(_opts.SharedPartnersWeight, 0.18);
                var totalWeight = _opts.FrequencyWeight + _opts.VectorWeight + _opts.DivisionWeight
                                + roleWeight + _opts.SharedLabelsWeight + _opts.TimeWeight + pivotWeight;

                var rawScore =
                    (frequencyScore * _opts.FrequencyWeight) +
                    (vectorScore * _opts.VectorWeight) +
                    (divisionScore * _opts.DivisionWeight) +
                    (roleScore * roleWeight) +
                    (labelScore * _opts.SharedLabelsWeight) +
                    (timeScore * _opts.TimeWeight) +
                    (pivotScore * pivotWeight);

                var score = totalWeight <= 0 ? 0.0 : Math.Min(rawScore / totalWeight, 1.0);

                return new KnownParticipantSuggestionDto
                {
                    Name = g.Key,
                    Role = candidateProfile.DominantRole,
                    Division = candidateProfile.DominantDivision,
                    MatchScore = score,
                    SeenCount = observations.Count,
                    CommonLabels = GetCommonValues(groupProfile.Labels, observations.SelectMany(x => x.Labels), 5),
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
            .Take(take)
            .ToList();

        return suggestions;
    }

    private GroupProfile BuildGroupProfile(List<GroupObservationSnapshot> observations)
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
            WindowStart: minDate.AddMinutes(-_opts.TimeWindowMinutes),
            WindowEnd: maxDate.AddMinutes(_opts.TimeWindowMinutes),
            DominantFrequency: GetDominantValue(observations.Select(x => x.Frequency)),
            DominantVector: GetDominantValue(observations.Select(x => x.VectorSignal)),
            DominantDivision: GetDominantValue(observations.Select(x => x.Division)),
            DominantRole: GetDominantValue(observations.Select(x => x.Role)));
    }

    private async Task<IReadOnlyList<CandidateContextSuggestionDto>> BuildContextSuggestionsAsync(
        AppDbContext db,
        ParticipantCandidateGroup group,
        int take,
        CancellationToken ct)
    {
        var groupParticipantIds = group.ParticipantRefs
            .Select(r => r.ParticipantId)
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

        var groupProfile = BuildGroupProfile([.. groupRows
            .Select(x => new GroupObservationSnapshot(
                x.Frequency,
                x.VectorSignal,
                x.Division,
                x.Role,
                x.ObservedDate,
                x.Labels))]);

        var contextProfiles = await BuildContextProfilesAsync(db, ct);

        var suggestions = contextProfiles
            .Select(context =>
            {
                var frequencyScore = ComputeOverlapScore(groupProfile.Frequencies, context.Pivot.Frequencies);
                var vectorScore = ComputeOverlapScore(groupProfile.VectorSignals, context.Pivot.VectorSignals);
                var divisionScore = ComputeOverlapScore(groupProfile.Divisions, context.Pivot.Divisions);
                var roleScore = ComputeOverlapScore(groupProfile.Roles, context.Pivot.Roles);
                var labelScore = ComputeOverlapScore(groupProfile.Labels, context.Pivot.Labels);
                var pivotScore = ComputeAverageNonZero(
                    ComputeOverlapScore(groupProfile.FrequencyDivisionPairs, context.Pivot.FrequencyDivisionPairs),
                    ComputeOverlapScore(groupProfile.VectorDivisionPairs, context.Pivot.VectorDivisionPairs),
                    ComputeOverlapScore(groupProfile.RoleDivisionPairs, context.Pivot.RoleDivisionPairs),
                    ComputeOverlapScore(groupProfile.LabelDivisionPairs, context.Pivot.LabelDivisionPairs));

                var hasConfirmedContext = context.RelatedResolvedNames.Count > 0;
                var confirmedScore = !hasConfirmedContext
                    ? 0.0
                    : Math.Min(1.0, (double)context.ConfirmedGroupCount / Math.Max(1, context.SeenCount));

                if (frequencyScore <= 0 && vectorScore <= 0 && divisionScore <= 0 && roleScore <= 0 && labelScore <= 0 && pivotScore <= 0)
                    return null;

                var roleWeight = Math.Min(_opts.DivisionWeight + _opts.SharedLabelsWeight, 0.10);
                var pivotWeight = Math.Min(_opts.SharedPartnersWeight, 0.18);
                var confirmedWeight = Math.Min(_opts.SharedPartnersWeight / 2.0, 0.10);
                var totalWeight = _opts.FrequencyWeight + _opts.VectorWeight + _opts.DivisionWeight
                                + roleWeight + _opts.SharedLabelsWeight + pivotWeight + confirmedWeight;

                var rawScore =
                    (frequencyScore * _opts.FrequencyWeight) +
                    (vectorScore * _opts.VectorWeight) +
                    (divisionScore * _opts.DivisionWeight) +
                    (roleScore * roleWeight) +
                    (labelScore * _opts.SharedLabelsWeight) +
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
                    CommonLabels = GetCommonValues(groupProfile.Labels, context.Pivot.Labels.Keys, 5),
                    RelatedKnownNames = [.. context.RelatedKnownNames
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .Take(5)],
                    RelatedResolvedNames = [.. context.RelatedResolvedNames
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .Take(5)],
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
            .Take(take)
            .ToList();

        return suggestions;
    }

    private async Task<IReadOnlyList<ContextProfile>> BuildContextProfilesAsync(
        AppDbContext db,
        CancellationToken ct)
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
                Labels = p.InterceptionMessage.Labels
                    .Select(l => l.NameLabel)
                    .ToList()
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var confirmedGroups = await db.ParticipantCandidateGroups
            .Where(g => g.Status == CandidateGroupStatus.Confirmed && g.SuggestedDivision != null)
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
                    m => new GroupObservationSnapshot(
                        m.Frequency,
                        m.VectorSignal,
                        m.Division,
                        null,
                        m.ObservedDate,
                        m.Labels),
                    ct);

        var buckets = new Dictionary<string, ContextProfileBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in knownRows)
        {
            var normalizedDivision = SemanticValue.NormalizeMeaningfulOrNull(row.Division);
            var key = SemanticValue.NormalizeKeyOrNull(row.Division);
            if (key is null || normalizedDivision is null)
                continue;

            var builder = GetOrCreateContextProfileBuilder(buckets, key, normalizedDivision);
            builder.Observations.Add(new GroupObservationSnapshot(
                row.Frequency,
                row.VectorSignal,
                row.Division,
                row.Role,
                row.ObservedDate,
                row.Labels));
            builder.RelatedKnownNames.Add(row.Name.Trim());
        }

        foreach (var group in confirmedGroups)
        {
            var normalizedDivision = SemanticValue.NormalizeMeaningfulOrNull(group.SuggestedDivision);
            var key = SemanticValue.NormalizeKeyOrNull(group.SuggestedDivision);
            if (key is null || normalizedDivision is null)
                continue;

            var builder = GetOrCreateContextProfileBuilder(buckets, key, normalizedDivision);
            builder.ConfirmedGroupCount++;

            if (!string.IsNullOrWhiteSpace(group.SuggestedName))
                builder.RelatedResolvedNames.Add(group.SuggestedName!.Trim());

            foreach (var messageId in group.ParticipantRefs.Select(r => r.MessageId).Distinct())
            {
                if (!confirmedMessages.TryGetValue(messageId, out var msg))
                    continue;

                builder.Observations.Add(msg with { Role = group.SuggestedRole });
            }
        }

        return [.. buckets.Values
            .Where(x => x.Observations.Count > 0)
            .Select(x => x.Build(BuildGroupProfile(x.Observations)))];
    }

    private static ContextProfileBuilder GetOrCreateContextProfileBuilder(
        IDictionary<string, ContextProfileBuilder> buckets,
        string key,
        string division)
    {
        if (buckets.TryGetValue(key, out var builder))
            return builder;

        builder = new ContextProfileBuilder(division);
        buckets[key] = builder;
        return builder;
    }

    private sealed class ContextProfileBuilder(string division)
    {
        public string Division { get; } = division;
        public List<GroupObservationSnapshot> Observations { get; } = [];
        public HashSet<string> RelatedKnownNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> RelatedResolvedNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int ConfirmedGroupCount { get; set; }

        public ContextProfile Build(GroupProfile pivot) => new(
            Division,
            pivot,
            RelatedKnownNames,
            RelatedResolvedNames,
            Observations.Count,
            ConfirmedGroupCount);
    }

    // =========================================================================
    // Enrich
    // =========================================================================

    private static async Task<IReadOnlyList<CandidateGroupDto>> EnrichGroupsAsync(
        AppDbContext db,
        List<ParticipantCandidateGroup> groups,
        CancellationToken ct)
    {
        if (groups.Count == 0)
            return [];

        var allMessageIds = groups
            .SelectMany(g => g.ParticipantRefs.Select(r => r.MessageId))
            .ToHashSet();

        var messages = await db.InterceptionMessages
            .Where(m => allMessageIds.Contains(m.Id))
            .Select(m => new MessageSnapshot(
                m.Id,
                m.ObservedDate,
                m.Frequency,
                m.VectorSignal,
                m.Division))
            .AsNoTracking()
            .ToDictionaryAsync(m => m.Id, ct);

        return [.. groups.Select(g => MapToDto(g, messages))];
    }

    private static CandidateGroupDto MapToDto(
        ParticipantCandidateGroup g,
        Dictionary<Guid, MessageSnapshot> messages)
    {
        var refs = g.ParticipantRefs.Select(r =>
        {
            messages.TryGetValue(r.MessageId, out var msg);
            return new CandidateGroupRefDto
            {
                MessageId = r.MessageId,
                ParticipantId = r.ParticipantId,
                Ordinal = r.Ordinal,
                ObservedDate = msg?.ObservedDate ?? default,
                Frequency = SemanticValue.NormalizeMeaningfulOrNull(msg?.Frequency),
                VectorSignal = SemanticValue.NormalizeMeaningfulOrNull(msg?.VectorSignal),
                Division = SemanticValue.NormalizeMeaningfulOrNull(msg?.Division),
            };
        }).ToList();

        return new CandidateGroupDto
        {
            Id = g.Id,
            Status = g.Status,
            ConfidenceScore = g.ConfidenceScore,
            SuggestedName = g.SuggestedName,
            SuggestedRole = g.SuggestedRole,
            SuggestedDivision = g.SuggestedDivision,
            ResolvedParticipantId = g.ResolvedParticipantId,
            ResolvedBy = g.ResolvedBy,
            ResolvedAt = g.ResolvedAt,
            CreatedAt = g.CreatedAt,
            Reasons = new CandidateGroupReasonsDto
            {
                SameFrequency = g.Reasons.SameFrequency,
                SameVector = g.Reasons.SameVector,
                SamePointSignal = g.Reasons.SamePointSignal,
                SameDivision = g.Reasons.SameDivision,
                CloseInTime = g.Reasons.CloseInTime,
                SharedPartners = g.Reasons.SharedPartners,
                SharedLabels = g.Reasons.SharedLabels,
            },
            Refs = refs,
        };
    }

    // =========================================================================
    // Pivot helpers
    // =========================================================================

    private static Dictionary<string, int> BuildCountMap(IEnumerable<string?> values)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var key = SemanticValue.NormalizeKeyOrNull(value);
            if (key is null)
                continue;

            map[key] = map.TryGetValue(key, out var current)
                ? current + 1
                : 1;
        }

        return map;
    }

    private static Dictionary<string, int> BuildPairCountMap(IEnumerable<(string? Left, string? Right)> pairs)
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

    private static Dictionary<string, int> BuildLabelDivisionPairCountMap(IEnumerable<GroupObservationSnapshot> observations)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var observation in observations)
        {
            var division = SemanticValue.NormalizeKeyOrNull(observation.Division);
            if (division is null)
                continue;

            foreach (var label in observation.Labels)
            {
                var labelKey = SemanticValue.NormalizeKeyOrNull(label);
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

    private static string? BuildPairKey(string? left, string? right)
    {
        var leftKey = SemanticValue.NormalizeKeyOrNull(left);
        var rightKey = SemanticValue.NormalizeKeyOrNull(right);

        return leftKey is null || rightKey is null
            ? null
            : $"{leftKey}|{rightKey}";
    }

    private static double ComputeOverlapScore(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right)
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

    private static double ComputeAverageNonZero(params double[] values)
    {
        var nonZero = values.Where(v => v > 0).ToList();
        return nonZero.Count == 0 ? 0.0 : nonZero.Average();
    }

    private static IReadOnlyList<string> GetCommonValues(
        IReadOnlyDictionary<string, int> pivotLeft,
        IEnumerable<string?> originalValues,
        int take)
    {
        return [.. originalValues
            .Select(x => SemanticValue.NormalizeMeaningfulOrNull(x))
            .Where(x => x is not null)
            .Select(x => new { Original = x!, Key = SemanticValue.NormalizeKeyOrNull(x) })
            .Where(x => x.Key is not null && pivotLeft.ContainsKey(x.Key))
            .Select(x => x.Original)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(take)];
    }

    // =========================================================================
    // Common helpers
    // =========================================================================

    private static HashSet<string> ToNormalizedSet(IEnumerable<string?> values)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var key = SemanticValue.NormalizeKeyOrNull(value);
            if (key is not null)
                set.Add(key);
        }

        return set;
    }

    private static bool EqualsNormalized(string? left, string? right)
    {
        var leftKey = SemanticValue.NormalizeKeyOrNull(left);
        var rightKey = SemanticValue.NormalizeKeyOrNull(right);

        return leftKey is not null &&
               rightKey is not null &&
               string.Equals(leftKey, rightKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetDominantValue(IEnumerable<string?> values)
    {
        var groups = values
            .Select(SemanticValue.NormalizeMeaningfulOrNull)
            .Where(v => v is not null)
            .Select(v => v!)
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return groups.Count == 0 ? null : groups[0].First();
    }
}
