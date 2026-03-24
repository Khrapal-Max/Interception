//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Domain.Records;
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

internal sealed record GroupMessageSnapshot(
    string? Frequency,
    string? VectorSignal,
    string? Division,
    DateTime ObservedDate,
    List<string> Labels);

internal sealed record GroupProfile(
    HashSet<string> Frequencies,
    HashSet<string> VectorSignals,
    HashSet<string> Divisions,
    HashSet<string> Labels,
    DateTime WindowStart,
    DateTime WindowEnd,
    string? DominantFrequency,
    string? DominantVector,
    string? DominantDivision);

internal sealed record ContextProfile(
    string Division,
    HashSet<string> Frequencies,
    HashSet<string> VectorSignals,
    HashSet<string> Labels,
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
            .Select(p => new { p.Id, p.InterceptionMessageId, p.Ordinal })
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
                    Frequency: m.Frequency,
                    VectorSignal: m.VectorSignal,
                    PointSignal: m.PointSignal,
                    Division: m.Division,
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
        if (openGroups.Count > 0 && unassigned.Count > 0)
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
                            Frequency: m.Frequency,
                            VectorSignal: m.VectorSignal,
                            PointSignal: m.PointSignal,
                            Division: m.Division,
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

                var enriched = false;

                foreach (var newCandidate in unassigned)
                {
                    if (assigned.Contains(newCandidate.ParticipantId))
                        continue;

                    var fit = ComputeGroupFit(newCandidate, groupContexts);
                    if (fit.Score < _opts.MinConfidenceScore)
                        continue;

                    openGroup.AddRef(new ParticipantRef(
                        newCandidate.MessageId,
                        newCandidate.ParticipantId,
                        newCandidate.Ordinal));

                    assigned.Add(newCandidate.ParticipantId);
                    groupContexts.Add(newCandidate);
                    enriched = true;
                }

                if (enriched)
                {
                    var (newScore, newReasons) = RecalculateGroupScore(groupContexts);
                    openGroup.UpdateScore(newScore, newReasons);
                    openGroup.UpdateSuggestedDivision(GetDominantValue(groupContexts.Select(x => x.Division)));
                    changed++;
                }
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

                var fit = ComputeGroupFit(remainingCandidates[j], group);
                if (fit.Score < _opts.MinConfidenceScore)
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
                suggestedDivision: GetDominantValue(group.Select(p => p.Division))));
        }

        if (newGroups.Count > 0)
        {
            db.ParticipantCandidateGroups.AddRange(newGroups);
            await db.SaveChangesAsync(ct);
            changed += newGroups.Count;
        }

        return changed;
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

        return await BuildKnownSuggestionsAsync(db, group.ParticipantRefs.Select(r => r.MessageId).ToList(), take, ct);
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

        return await BuildContextSuggestionsAsync(
            db,
            group.ParticipantRefs.Select(r => r.MessageId).ToList(),
            take,
            ct);
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

        var closeInTime = Math.Abs((a.ObservedDate - b.ObservedDate).TotalMinutes)
            <= _opts.TimeWindowMinutes;

        var sharedPartners = CountSharedPartners(a.KnownPartnerNames, b.KnownPartnerNames)
            >= _opts.MinSharedPartners;

        var sharedLabels = a.Labels.Count > 0
            && b.Labels.Count > 0
            && a.Labels.Overlaps(b.Labels);

        var score =
            (sameFrequency ? _opts.FrequencyWeight : 0) +
            (sameVector ? _opts.VectorWeight : 0) +
            (sharedPartners ? _opts.SharedPartnersWeight : 0) +
            (samePoint ? _opts.PointSignalWeight : 0) +
            (sameDivision ? _opts.DivisionWeight : 0) +
            (closeInTime ? _opts.TimeWeight : 0) +
            (sharedLabels ? _opts.SharedLabelsWeight : 0);

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
    /// Перевіряє, наскільки кандидат підходить до вже зібраної групи.
    /// Кандидат має співпадати хоча б з половиною членів групи
    /// або з єдиним членом, якщо група поки складається з однієї особи.
    /// </summary>
    private (double Score, PatternMatchReasons Reasons) ComputeGroupFit(
        UnknownContext candidate,
        IReadOnlyList<UnknownContext> members)
    {
        if (members.Count == 0)
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
            AggregateReasons(strongMatches.Select(x => x.Reasons).ToList()));
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
                pairResults.Add(ComputeScore(members[i], members[j]));
        }

        return (
            pairResults.Average(x => x.Score),
            AggregateReasons(pairResults.Select(x => x.Reasons).ToList()));
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
        IReadOnlyCollection<string> partnersA,
        IReadOnlyCollection<string> partnersB)
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
        IReadOnlyCollection<Guid> groupMessageIds,
        int take,
        CancellationToken ct)
    {
        if (groupMessageIds.Count == 0)
            return [];

        var groupMessageRows = await db.InterceptionMessages
            .Where(m => groupMessageIds.Contains(m.Id))
            .Select(m => new
            {
                m.Frequency,
                m.VectorSignal,
                m.Division,
                m.ObservedDate,
                Labels = m.Labels.Select(l => l.NameLabel).ToList()
            })
            .ToListAsync(ct);

        if (groupMessageRows.Count == 0)
            return [];

        var groupMessages = groupMessageRows
            .Select(m => new GroupMessageSnapshot(
                m.Frequency,
                m.VectorSignal,
                m.Division,
                m.ObservedDate,
                m.Labels))
            .ToList();

        var profile = BuildGroupProfile(groupMessages);

        var knownRows = await db.InterceptionMessageParticipants
            .Where(p => !p.IsUnknown && p.Name != null)
            .Select(p => new
            {
                Name = p.Name!,
                p.Role,
                Frequency = p.InterceptionMessage.Frequency,
                VectorSignal = p.InterceptionMessage.VectorSignal,
                Division = p.InterceptionMessage.Division,
                ObservedDate = p.InterceptionMessage.ObservedDate,
                Labels = p.InterceptionMessage.Labels
                    .Select(l => l.NameLabel)
                    .ToList()
            })
            .ToListAsync(ct);

        if (knownRows.Count == 0)
            return [];

        var suggestions = knownRows
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var appearances = g.ToList();

                var freqMatch = HasOverlap(appearances.Select(x => x.Frequency), profile.Frequencies);
                var vecMatch = HasOverlap(appearances.Select(x => x.VectorSignal), profile.VectorSignals);
                var divMatch = HasOverlap(appearances.Select(x => x.Division), profile.Divisions);
                var timeMatch = appearances.Any(x =>
                    x.ObservedDate >= profile.WindowStart &&
                    x.ObservedDate <= profile.WindowEnd);

                var commonLabels = appearances
                    .SelectMany(x => x.Labels)
                    .Select(label => new
                    {
                        Original = label.Trim(),
                        Key = NormalizeKey(label)
                    })
                    .Where(x => x.Key is not null && profile.Labels.Contains(x.Key))
                    .Select(x => x.Original)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToList();

                var labelMatch = commonLabels.Count > 0;

                // Не показуємо кандидата лише тому що він був близько в часі.
                if (!freqMatch && !vecMatch && !divMatch && !labelMatch)
                    return null;

                var score =
                    (freqMatch ? _opts.FrequencyWeight : 0) +
                    (vecMatch ? _opts.VectorWeight : 0) +
                    (divMatch ? _opts.DivisionWeight : 0) +
                    (timeMatch ? _opts.TimeWeight : 0) +
                    (labelMatch ? _opts.SharedLabelsWeight : 0);

                var lastAppearance = appearances
                    .OrderByDescending(x => x.ObservedDate)
                    .First();

                return new KnownParticipantSuggestionDto
                {
                    Name = g.Key,
                    Role = lastAppearance.Role,
                    Division = GetDominantValue(appearances.Select(x => x.Division)),
                    MatchScore = Math.Min(score, 1.0),
                    SeenCount = appearances.Count,
                    CommonLabels = commonLabels,
                    Reasons = new KnownSuggestionReasonsDto
                    {
                        SameFrequency = freqMatch,
                        SameVector = vecMatch,
                        SameDivision = divMatch,
                        CloseInTime = timeMatch,
                        SharedLabels = labelMatch,
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

    private GroupProfile BuildGroupProfile(IReadOnlyList<GroupMessageSnapshot> groupMessages)
    {
        var frequencies = ToNormalizedSet(groupMessages.Select(x => x.Frequency));
        var vectors = ToNormalizedSet(groupMessages.Select(x => x.VectorSignal));
        var divisions = ToNormalizedSet(groupMessages.Select(x => x.Division));
        var labels = ToNormalizedSet(groupMessages.SelectMany(x => x.Labels));

        var minDate = groupMessages.Min(x => x.ObservedDate);
        var maxDate = groupMessages.Max(x => x.ObservedDate);

        return new GroupProfile(
            Frequencies: frequencies,
            VectorSignals: vectors,
            Divisions: divisions,
            Labels: labels,
            WindowStart: minDate.AddMinutes(-_opts.TimeWindowMinutes),
            WindowEnd: maxDate.AddMinutes(_opts.TimeWindowMinutes),
            DominantFrequency: GetDominantValue(groupMessages.Select(x => x.Frequency)),
            DominantVector: GetDominantValue(groupMessages.Select(x => x.VectorSignal)),
            DominantDivision: GetDominantValue(groupMessages.Select(x => x.Division)));
    }

    private async Task<IReadOnlyList<CandidateContextSuggestionDto>> BuildContextSuggestionsAsync(
        AppDbContext db,
        IReadOnlyCollection<Guid> groupMessageIds,
        int take,
        CancellationToken ct)
    {
        if (groupMessageIds.Count == 0)
            return [];

        var groupMessageRows = await db.InterceptionMessages
            .Where(m => groupMessageIds.Contains(m.Id))
            .Select(m => new
            {
                m.Frequency,
                m.VectorSignal,
                m.Division,
                m.ObservedDate,
                Labels = m.Labels.Select(l => l.NameLabel).ToList()
            })
            .ToListAsync(ct);

        if (groupMessageRows.Count == 0)
            return [];

        var groupMessages = groupMessageRows
            .Select(m => new GroupMessageSnapshot(
                m.Frequency,
                m.VectorSignal,
                m.Division,
                m.ObservedDate,
                m.Labels))
            .ToList();

        var profile = BuildGroupProfile(groupMessages);
        var contextProfiles = await BuildContextProfilesAsync(db, ct);

        var suggestions = contextProfiles
            .Select(context =>
            {
                var frequencyMatch = Overlaps(profile.Frequencies, context.Frequencies);
                var vectorMatch = Overlaps(profile.VectorSignals, context.VectorSignals);
                var divisionMatch = profile.Divisions.Contains(NormalizeKey(context.Division) ?? string.Empty);

                var commonLabels = context.Labels
                    .Where(profile.Labels.Contains)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Take(5)
                    .ToList();

                var hasConfirmedContext = context.RelatedResolvedNames.Count > 0;
                var labelMatch = commonLabels.Count > 0;

                if (!frequencyMatch && !vectorMatch && !divisionMatch && !labelMatch && !hasConfirmedContext)
                    return null;

                // Контекст не повинен будуватись лише на тому, що раніше тут щось підтвердили.
                // Потрібна хоча б одна фактична ознака поточної групи.
                if (!frequencyMatch && !vectorMatch && !divisionMatch && !labelMatch)
                    return null;

                var score =
                    (frequencyMatch ? _opts.FrequencyWeight : 0) +
                    (vectorMatch ? _opts.VectorWeight : 0) +
                    (divisionMatch ? _opts.DivisionWeight : 0) +
                    (labelMatch ? _opts.SharedLabelsWeight : 0) +
                    (hasConfirmedContext ? Math.Min(_opts.SharedPartnersWeight, 0.15) : 0);

                return new CandidateContextSuggestionDto
                {
                    Division = context.Division,
                    MatchScore = Math.Min(score, 1.0),
                    SeenCount = context.SeenCount,
                    ConfirmedGroupCount = context.ConfirmedGroupCount,
                    CommonLabels = commonLabels,
                    RelatedKnownNames = context.RelatedKnownNames
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .Take(5)
                        .ToList(),
                    RelatedResolvedNames = context.RelatedResolvedNames
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .Take(5)
                        .ToList(),
                    Reasons = new CandidateContextReasonsDto
                    {
                        SameFrequency = frequencyMatch,
                        SameVector = vectorMatch,
                        SameDivision = divisionMatch,
                        SharedLabels = labelMatch,
                        HasConfirmedContext = hasConfirmedContext
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
                Division = p.InterceptionMessage.Division!,
                Frequency = p.InterceptionMessage.Frequency,
                VectorSignal = p.InterceptionMessage.VectorSignal,
                Labels = p.InterceptionMessage.Labels
                    .Select(l => l.NameLabel)
                    .ToList()
            })
            .ToListAsync(ct);

        var confirmedGroups = await db.ParticipantCandidateGroups
            .Where(g => g.Status == CandidateGroupStatus.Confirmed && g.SuggestedDivision != null)
            .AsNoTracking()
            .ToListAsync(ct);

        var confirmedMessageIds = confirmedGroups
            .SelectMany(g => g.ParticipantRefs.Select(r => r.MessageId))
            .ToHashSet();

        var confirmedMessages = confirmedMessageIds.Count == 0
            ? new Dictionary<Guid, GroupMessageSnapshot>()
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
                    m => new GroupMessageSnapshot(
                        m.Frequency,
                        m.VectorSignal,
                        m.Division,
                        m.ObservedDate,
                        m.Labels),
                    ct);

        var buckets = new Dictionary<string, ContextProfileBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in knownRows)
        {
            var key = NormalizeKey(row.Division);
            if (key is null)
                continue;

            var builder = GetOrCreateContextProfileBuilder(buckets, key, row.Division.Trim());
            builder.SeenCount++;
            builder.RelatedKnownNames.Add(row.Name.Trim());
            AddOptional(builder.Frequencies, row.Frequency);
            AddOptional(builder.VectorSignals, row.VectorSignal);
            AddOptionalRange(builder.Labels, row.Labels);
        }

        foreach (var group in confirmedGroups)
        {
            var key = NormalizeKey(group.SuggestedDivision);
            if (key is null)
                continue;

            var builder = GetOrCreateContextProfileBuilder(buckets, key, group.SuggestedDivision!.Trim());
            builder.ConfirmedGroupCount++;

            if (!string.IsNullOrWhiteSpace(group.SuggestedName))
                builder.RelatedResolvedNames.Add(group.SuggestedName!.Trim());

            foreach (var messageId in group.ParticipantRefs.Select(r => r.MessageId).Distinct())
            {
                if (!confirmedMessages.TryGetValue(messageId, out var msg))
                    continue;

                builder.SeenCount++;
                AddOptional(builder.Frequencies, msg.Frequency);
                AddOptional(builder.VectorSignals, msg.VectorSignal);
                AddOptionalRange(builder.Labels, msg.Labels);
            }
        }

        return buckets.Values
            .Where(x => x.SeenCount > 0)
            .Select(x => x.Build())
            .ToList();
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

    private static bool Overlaps(HashSet<string> left, HashSet<string> right)
        => left.Count > 0 && right.Count > 0 && left.Overlaps(right);

    private static void AddOptional(HashSet<string> bucket, string? value)
    {
        var key = NormalizeKey(value);
        if (key is not null)
            bucket.Add(key);
    }

    private static void AddOptionalRange(HashSet<string> bucket, IEnumerable<string?> values)
    {
        foreach (var value in values)
            AddOptional(bucket, value);
    }

    private sealed class ContextProfileBuilder
    {
        public ContextProfileBuilder(string division)
        {
            Division = division;
        }

        public string Division { get; }
        public HashSet<string> Frequencies { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> VectorSignals { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Labels { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> RelatedKnownNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> RelatedResolvedNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int SeenCount { get; set; }
        public int ConfirmedGroupCount { get; set; }

        public ContextProfile Build() => new(
            Division,
            Frequencies,
            VectorSignals,
            Labels,
            RelatedKnownNames,
            RelatedResolvedNames,
            SeenCount,
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
                Frequency = msg?.Frequency,
                VectorSignal = msg?.VectorSignal,
                Division = msg?.Division,
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
    // Common helpers
    // =========================================================================

    private static bool HasOverlap(IEnumerable<string?> values, HashSet<string> normalizedSet)
    {
        if (normalizedSet.Count == 0)
            return false;

        foreach (var value in values)
        {
            var key = NormalizeKey(value);
            if (key is not null && normalizedSet.Contains(key))
                return true;
        }

        return false;
    }

    private static HashSet<string> ToNormalizedSet(IEnumerable<string?> values)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var key = NormalizeKey(value);
            if (key is not null)
                set.Add(key);
        }

        return set;
    }

    private static bool EqualsNormalized(string? left, string? right)
    {
        var leftKey = NormalizeKey(left);
        var rightKey = NormalizeKey(right);

        return leftKey is not null &&
               rightKey is not null &&
               string.Equals(leftKey, rightKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeKey(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();

    private static string? GetDominantValue(IEnumerable<string?> values)
    {
        var groups = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return groups.Count == 0 ? null : groups[0].First();
    }
}
