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
    IReadOnlyList<string> KnownPartnerNames,
    IReadOnlyList<string> Labels);

/// <summary>Знімок повідомлення для EnrichGroupsAsync — без навігацій.</summary>
internal sealed record MessageSnapshot(
    Guid Id,
    DateTime ObservedDate,
    string? Frequency,
    string? VectorSignal,
    string? Division);

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

        // ------------------------------------------------------------------
        // 1. Завантажуємо всіх НВ учасників — без Include (уникаємо циклу)
        // ------------------------------------------------------------------
        var unknownRefs = await db.InterceptionMessageParticipants
            .Where(p => p.IsUnknown)
            .Select(p => new { p.Id, p.InterceptionMessageId, p.Ordinal })
            .ToListAsync(ct);

        if (unknownRefs.Count < 2) return 0;

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
                    KnownPartnerNames: m.KnownPartners,
                    Labels: m.Labels);
            })
            .ToList();

        // ------------------------------------------------------------------
        // 2. Визначаємо які НВ вже охоплені групами
        //    Confirmed → виключаємо назавжди
        //    Open      → не створюємо нову групу, але можна збагатити
        //    Dismissed → розглядаємо знову ("замало інфи")
        // ------------------------------------------------------------------
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

        // НВ доступні для нової групи або збагачення (не Confirmed)
        var freeCandidates = allCandidates
            .Where(p => !confirmedIds.Contains(p.ParticipantId))
            .ToList();

        // НВ що ще не в жодній Open групі — кандидати на збагачення або нову групу
        var unassigned = freeCandidates
            .Where(p => !openIds.Contains(p.ParticipantId))
            .ToList();

        var assigned = new HashSet<Guid>();
        var changed = 0;

        // ------------------------------------------------------------------
        // Крок A — ЗБАГАЧЕННЯ існуючих Open груп
        // Для кожної Open групи шукаємо нових НВ що підходять
        // ------------------------------------------------------------------
        if (openGroups.Count > 0 && unassigned.Count > 0)
        {
            // Завантажуємо контекст вже існуючих НВ у Open групах
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
                            KnownPartnerNames: m.KnownPartners,
                            Labels: m.Labels);
                    });

            foreach (var openGroup in openGroups)
            {
                var groupContexts = openGroup.ParticipantRefs
                    .Where(r => existingContextMap.ContainsKey(r.ParticipantId))
                    .Select(r => existingContextMap[r.ParticipantId])
                    .ToList();

                if (groupContexts.Count == 0) continue;

                var enriched = false;

                foreach (var newCandidate in unassigned)
                {
                    if (assigned.Contains(newCandidate.ParticipantId)) continue;

                    // Порівнюємо новий НВ з кожним існуючим в групі
                    var bestScore = groupContexts
                        .Select(existing => ComputeScore(newCandidate, existing).Score)
                        .Max();

                    if (bestScore >= _opts.MinConfidenceScore)
                    {
                        openGroup.AddRef(
                            new ParticipantRef(
                                newCandidate.MessageId,
                                newCandidate.ParticipantId,
                                newCandidate.Ordinal));

                        assigned.Add(newCandidate.ParticipantId);
                        groupContexts.Add(newCandidate);
                        enriched = true;
                    }
                }

                if (enriched)
                {
                    // Перераховуємо score по всіх учасниках групи
                    var (newScore, newReasons) = RecalculateGroupScore(groupContexts);
                    openGroup.UpdateScore(newScore, newReasons);
                    changed++;
                }
            }

            await db.SaveChangesAsync(ct);
        }

        // ------------------------------------------------------------------
        // Крок B — СТВОРЕННЯ нових груп
        // З НВ що не потрапили до жодної Open групи
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
            if (newAssigned.Contains(remainingCandidates[i].ParticipantId)) continue;

            var group = new List<UnknownContext> { remainingCandidates[i] };
            var groupScore = 0.0;
            PatternMatchReasons? bestReasons = null;

            for (var j = i + 1; j < remainingCandidates.Count; j++)
            {
                if (newAssigned.Contains(remainingCandidates[j].ParticipantId)) continue;

                var (score, reasons) = ComputeScore(
                    remainingCandidates[i], remainingCandidates[j]);

                if (score >= _opts.MinConfidenceScore)
                {
                    group.Add(remainingCandidates[j]);
                    newAssigned.Add(remainingCandidates[j].ParticipantId);

                    if (score > groupScore)
                    {
                        groupScore = score;
                        bestReasons = reasons;
                    }
                }
            }

            if (group.Count < 2) continue;

            newAssigned.Add(remainingCandidates[i].ParticipantId);

            var refs = group
                .Select(p => new ParticipantRef(p.MessageId, p.ParticipantId, p.Ordinal))
                .ToList();

            var suggestedDivision = group
                .Select(p => p.Division)
                .Where(d => d != null)
                .GroupBy(d => d!)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            newGroups.Add(ParticipantCandidateGroup.Create(
                refs,
                groupScore,
                bestReasons!));
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

        // 1. Завантажуємо групу і її спостереження
        var group = await db.ParticipantCandidateGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);

        if (group is null) return [];

        var groupMsgIds = group.ParticipantRefs
            .Select(r => r.MessageId)
            .ToHashSet();

        // 2. Ознаки групи — агрегуємо по більшості
        var groupMessages = await db.InterceptionMessages
            .Where(m => groupMsgIds.Contains(m.Id))
            .Select(m => new
            {
                m.Frequency,
                m.VectorSignal,
                m.Division,
                m.ObservedDate,
                Labels = m.Labels.Select(l => l.NameLabel).ToList()
            })
            .ToListAsync(ct);

        // Домінуючі ознаки групи (найчастіші)
        var dominantFrequency = groupMessages
            .GroupBy(m => m.Frequency)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        var dominantVector = groupMessages
            .GroupBy(m => m.VectorSignal)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        var dominantDivision = groupMessages
            .GroupBy(m => m.Division)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        var groupLabels = groupMessages
            .SelectMany(m => m.Labels)
            .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var groupDateMin = groupMessages.Min(m => m.ObservedDate);
        var groupDateMax = groupMessages.Max(m => m.ObservedDate);

        // 3. Розширюємо часове вікно для пошуку відомих
        var windowStart = groupDateMin.AddMinutes(-_opts.TimeWindowMinutes);
        var windowEnd = groupDateMax.AddMinutes(_opts.TimeWindowMinutes);

        // 4. Знаходимо відомих учасників в релевантних повідомленнях
        var knownParticipants = await db.InterceptionMessageParticipants
            .Where(p => !p.IsUnknown && p.Name != null)
            .Where(p => p.InterceptionMessage.ObservedDate >= windowStart
                     && p.InterceptionMessage.ObservedDate <= windowEnd)
            .Select(p => new
            {
                p.Name,
                p.Role,
                p.InterceptionMessage.Frequency,
                p.InterceptionMessage.VectorSignal,
                p.InterceptionMessage.Division,
                p.InterceptionMessage.ObservedDate,
                Labels = p.InterceptionMessage.Labels
                    .Select(l => l.NameLabel)
                    .ToList()
            })
            .ToListAsync(ct);

        if (knownParticipants.Count == 0) return [];

        // 5. Рахуємо score для кожного відомого учасника
        var scored = knownParticipants
            .GroupBy(p => p.Name!, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var appearances = g.ToList();

                var freqMatch = !string.IsNullOrWhiteSpace(dominantFrequency)
                    && appearances.Any(p => p.Frequency == dominantFrequency);

                var vecMatch = !string.IsNullOrWhiteSpace(dominantVector)
                    && appearances.Any(p => p.VectorSignal == dominantVector);

                var divMatch = !string.IsNullOrWhiteSpace(dominantDivision)
                    && appearances.Any(p => p.Division == dominantDivision);

                var timeMatch = appearances.Any(p =>
                    p.ObservedDate >= windowStart && p.ObservedDate <= windowEnd);

                var commonLabels = appearances
                    .SelectMany(p => p.Labels)
                    .Where(l => groupLabels.Contains(l))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var labelMatch = commonLabels.Count > 0;

                var score =
                    (freqMatch ? _opts.FrequencyWeight : 0) +
                    (vecMatch ? _opts.VectorWeight : 0) +
                    (divMatch ? _opts.DivisionWeight : 0) +
                    (timeMatch ? _opts.TimeWeight : 0) +
                    (labelMatch ? _opts.SharedLabelsWeight : 0);

                var lastRole = appearances
                    .OrderByDescending(p => p.ObservedDate)
                    .Select(p => p.Role)
                    .FirstOrDefault(r => r != null);

                return new KnownParticipantSuggestionDto
                {
                    Name = g.Key,
                    Role = lastRole,
                    MatchScore = score,
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
            .Where(s => s.MatchScore > 0)
            .OrderByDescending(s => s.MatchScore)
            .Take(take)
            .ToList();

        return scored;
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

        if (group is null) return null;

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
        var sameFrequency = !string.IsNullOrWhiteSpace(a.Frequency)
                         && a.Frequency == b.Frequency;

        var sameVector = !string.IsNullOrWhiteSpace(a.VectorSignal)
                      && a.VectorSignal == b.VectorSignal;

        var samePoint = !string.IsNullOrWhiteSpace(a.PointSignal)
                     && a.PointSignal == b.PointSignal;

        var sameDivision = !string.IsNullOrWhiteSpace(a.Division)
                        && a.Division == b.Division;

        var closeInTime = Math.Abs((a.ObservedDate - b.ObservedDate).TotalMinutes)
                       <= _opts.TimeWindowMinutes;

        var sharedPartners = CountSharedPartners(a.KnownPartnerNames, b.KnownPartnerNames)
                          >= _opts.MinSharedPartners;

        var sharedLabels = a.Labels.Count > 0
                        && b.Labels.Count > 0
                        && a.Labels.Any(la => b.Labels.Any(lb =>
                               lb.Equals(la, StringComparison.OrdinalIgnoreCase)));

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
    /// Перераховує score групи по всіх парах учасників.
    /// Береться максимальний score серед усіх пар.
    /// </summary>
    private (double Score, PatternMatchReasons Reasons) RecalculateGroupScore(
        List<UnknownContext> members)
    {
        var bestScore = 0.0;
        PatternMatchReasons? bestReasons = null;

        for (var i = 0; i < members.Count; i++)
            for (var j = i + 1; j < members.Count; j++)
            {
                var (score, reasons) = ComputeScore(members[i], members[j]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestReasons = reasons;
                }
            }

        return (bestScore, bestReasons ?? new PatternMatchReasons());
    }

    private static int CountSharedPartners(
        IReadOnlyList<string> partnersA,
        IReadOnlyList<string> partnersB)
    {
        var setA = new HashSet<string>(partnersA, StringComparer.OrdinalIgnoreCase);
        return partnersB.Count(p => setA.Contains(p));
    }

    // =========================================================================
    // Enrich
    // =========================================================================

    private static async Task<IReadOnlyList<CandidateGroupDto>> EnrichGroupsAsync(
        AppDbContext db,
        List<ParticipantCandidateGroup> groups,
        CancellationToken ct)
    {
        if (groups.Count == 0) return [];

        var allMessageIds = groups
            .SelectMany(g => g.ParticipantRefs.Select(r => r.MessageId))
            .ToHashSet();

        var messages = await db.InterceptionMessages
            .Where(m => allMessageIds.Contains(m.Id))
            .Select(m => new MessageSnapshot(
                m.Id, m.ObservedDate, m.Frequency, m.VectorSignal, m.Division))
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
}
