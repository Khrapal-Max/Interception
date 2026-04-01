//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Analytics.Abstractions;
using Interception.Application.Analytics.Builders;
using Interception.Domain.Entities;
using Interception.Domain.Enums;
using Interception.Common.Extensions;
using Interception.Domain.Records;
using Interception.Application.Abstractions;

namespace Interception.Application.Analytics.Services;

/// <summary>
/// Основний сервіс аналізу невідомих учасників і побудови / збагачення open-груп.
/// </summary>
public sealed class ParticipantCandidateAnalysisService(
    IAppDbContextFactory dbFactory,
    IKnownParticipantSuggestionService knownParticipantSuggestionService,
    IOptions<PatternRecognitionOptions> options) : IParticipantCandidateAnalysisService
{
    private readonly PatternRecognitionOptions _options = options.Value;

    /// <inheritdoc />
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
                KnownPartners = m.Participants.Where(p => !p.IsUnknown && p.Name != null).Select(p => p.Name!).ToList()
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
                    Frequency: SemanticValueExtensions.NormalizeMeaningfulOrNull(m.Frequency),
                    VectorSignal: SemanticValueExtensions.NormalizeMeaningfulOrNull(m.VectorSignal),
                    PointSignal: SemanticValueExtensions.NormalizeMeaningfulOrNull(m.PointSignal),
                    Division: SemanticValueExtensions.NormalizeMeaningfulOrNull(m.Division),
                    Role: SemanticValueExtensions.NormalizeMeaningfulOrNull(p.Role),
                    ObservedDate: m.ObservedDate,
                    KnownPartnerNames: PatternRecognitionMath.ToNormalizedSet(m.KnownPartners),
                    Labels: PatternRecognitionMath.ToNormalizedSet(m.Labels));
            })
            .ToList();

        var confirmedIds = await db.ParticipantCandidateGroups
            .Where(g => g.Status == CandidateGroupStatus.Confirmed)
            .SelectMany(g => g.ParticipantRefs.Select(r => r.ParticipantId))
            .ToHashSetAsync(ct);

        var openGroups = await db.ParticipantCandidateGroups
            .AsNoTracking()
            .Include(g => g.ParticipantRefs)
            .Where(g => g.Status == CandidateGroupStatus.Open)
            .ToListAsync(ct);

        var openIds = openGroups.SelectMany(g => g.ParticipantRefs.Select(r => r.ParticipantId)).ToHashSet();
        var freeCandidates = allCandidates.Where(p => !confirmedIds.Contains(p.ParticipantId)).ToList();
        var unassigned = freeCandidates.Where(p => !openIds.Contains(p.ParticipantId)).ToList();

        var assigned = new HashSet<Guid>();
        var changed = 0;
        var openGroupUpdates = new List<OpenGroupUpdate>();

        if (openGroups.Count > 0)
        {
            var openParticipantIds = openGroups.SelectMany(g => g.ParticipantRefs.Select(r => r.ParticipantId)).ToHashSet();
            var existingUnknownRefs = unknownRefs.Where(p => openParticipantIds.Contains(p.Id)).ToList();

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
                            Frequency: Extensions.SemanticValue.NormalizeMeaningfulOrNull(m.Frequency),
                            VectorSignal: Extensions.SemanticValue.NormalizeMeaningfulOrNull(m.VectorSignal),
                            PointSignal: Extensions.SemanticValue.NormalizeMeaningfulOrNull(m.PointSignal),
                            Division: Extensions.SemanticValue.NormalizeMeaningfulOrNull(m.Division),
                            Role: Extensions.SemanticValue.NormalizeMeaningfulOrNull(p.Role),
                            ObservedDate: m.ObservedDate,
                            KnownPartnerNames: PatternRecognitionMath.ToNormalizedSet(m.KnownPartners),
                            Labels: PatternRecognitionMath.ToNormalizedSet(m.Labels));
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
                var refsToAdd = new List<ParticipantRef>();

                foreach (var newCandidate in unassigned)
                {
                    if (assigned.Contains(newCandidate.ParticipantId))
                        continue;

                    if (PatternRecognitionMath.HasSameObservationMember(newCandidate, groupContexts))
                        continue;

                    var (Score, Reasons) = PatternRecognitionMath.ComputeGroupFit(newCandidate, groupContexts, _options);
                    if (Score < _options.MinConfidenceScore)
                        continue;

                    refsToAdd.Add(new ParticipantRef(newCandidate.MessageId, newCandidate.ParticipantId, newCandidate.Ordinal));
                    assigned.Add(newCandidate.ParticipantId);
                    groupContexts.Add(newCandidate);
                    groupChanged = true;
                }

                if (groupChanged)
                {
                    var (Score, Reasons) = PatternRecognitionMath.RecalculateGroupScore(groupContexts, _options);
                    openGroupUpdates.Add(new OpenGroupUpdate(
                        openGroup.Id,
                        refsToAdd,
                        Score,
                        Reasons,
                        PatternRecognitionMath.GetDominantValue(groupContexts.Select(x => x.Role)),
                        PatternRecognitionMath.GetDominantValue(groupContexts.Select(x => x.Division))));
                }
            }

            if (openGroupUpdates.Count > 0)
            {
                foreach (var update in openGroupUpdates)
                    await ApplyOpenGroupUpdateAsync(update, ct);

                changed += openGroupUpdates.Count;
            }
        }

        var remainingCandidates = unassigned.Where(p => !assigned.Contains(p.ParticipantId)).ToList();
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

                if (PatternRecognitionMath.HasSameObservationMember(remainingCandidates[j], group))
                    continue;

                var fit = PatternRecognitionMath.ComputeGroupFit(remainingCandidates[j], group, _options);
                if (fit.Score < _options.MinConfidenceScore)
                    continue;

                group.Add(remainingCandidates[j]);
                newAssigned.Add(remainingCandidates[j].ParticipantId);
            }

            if (group.Count < 2)
                continue;

            var (Score, Reasons) = PatternRecognitionMath.RecalculateGroupScore(group, _options);
            if (Score < _options.MinConfidenceScore)
                continue;

            newAssigned.Add(remainingCandidates[i].ParticipantId);

            var refs = group
                .Select(p => new ParticipantRef(p.MessageId, p.ParticipantId, p.Ordinal))
                .ToList();

            newGroups.Add(ParticipantCandidateGroup.Create(
                refs,
                Score,
                Reasons,
                suggestedRole: PatternRecognitionMath.GetDominantValue(group.Select(p => p.Role)),
                suggestedDivision: PatternRecognitionMath.GetDominantValue(group.Select(p => p.Division))));
        }

        if (newGroups.Count > 0)
        {
            db.ParticipantCandidateGroups.AddRange(newGroups);
            await db.SaveChangesAsync(ct);

            foreach (var groupId in newGroups.Select(g => (Guid)g.Id))
                await RefreshOpenGroupSuggestionAsync(groupId, ct);

            changed += newGroups.Count;
        }

        return changed;
    }

    /// <summary>
    /// Застосовує збагачення до open-групи в окремому DbContext, щоб уникнути конфліктів
    /// трекінгу та owned-колекції ParticipantRefs під час аналізу.
    /// </summary>
    private async Task ApplyOpenGroupUpdateAsync(OpenGroupUpdate update, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var group = await db.ParticipantCandidateGroups
            .Include(g => g.ParticipantRefs)
            .FirstOrDefaultAsync(
                g => g.Id == update.GroupId && g.Status == CandidateGroupStatus.Open,
                ct);

        if (group is null)
            return;

        foreach (var newRef in update.NewRefs)
        {
            if (group.ParticipantRefs.Any(x => x.ParticipantId == newRef.ParticipantId))
                continue;

            group.AddRef(newRef);
        }

        group.UpdateScore(update.Score, update.Reasons);

        var suggestedRole = SemanticValueExtensions.NormalizeMeaningfulOrNull(update.SuggestedRole);
        if (!string.IsNullOrWhiteSpace(suggestedRole) && group.SuggestedRole != suggestedRole)
            group.UpdateSuggestedRole(suggestedRole);

        var suggestedDivision = SemanticValueExtensions.NormalizeMeaningfulOrNull(update.SuggestedDivision);
        if (!string.IsNullOrWhiteSpace(suggestedDivision) && group.SuggestedDivision != suggestedDivision)
            group.UpdateSuggestedDivision(suggestedDivision);

        await db.SaveChangesAsync(ct);
        await RefreshOpenGroupSuggestionAsync(update.GroupId, ct);
    }

    /// <summary>
    /// Синхронізує top known suggestion назад в open-group для overlay у реєстрі.
    /// </summary>
    private async Task RefreshOpenGroupSuggestionAsync(Guid groupId, CancellationToken ct)
    {
        var best = (await knownParticipantSuggestionService.GetKnownSuggestionsAsync(groupId, 1, ct))
            .FirstOrDefault();

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var group = await db.ParticipantCandidateGroups
            .FirstOrDefaultAsync(
                g => g.Id == groupId && g.Status == CandidateGroupStatus.Open,
                ct);

        if (group is null)
            return;

        var changed = false;

        var suggestedName = SemanticValueExtensions.NormalizeMeaningfulOrNull(best?.Name);
        if (group.SuggestedName != suggestedName)
        {
            group.UpdateSuggestedName(suggestedName);
            changed = true;
        }

        var suggestedRole = SemanticValueExtensions.NormalizeMeaningfulOrNull(best?.Role);
        if (!string.IsNullOrWhiteSpace(suggestedRole) && group.SuggestedRole != suggestedRole)
        {
            group.UpdateSuggestedRole(suggestedRole);
            changed = true;
        }

        var suggestedDivision = SemanticValueExtensions.NormalizeMeaningfulOrNull(best?.Division);
        if (!string.IsNullOrWhiteSpace(suggestedDivision) && group.SuggestedDivision != suggestedDivision)
        {
            group.UpdateSuggestedDivision(suggestedDivision);
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }


    /// <summary>
    /// Описує набір змін, які треба застосувати до open-групи після аналізу.
    /// </summary>
    private sealed record OpenGroupUpdate(
        Guid GroupId,
        IReadOnlyList<ParticipantRef> NewRefs,
        double Score,
        PatternMatchReasons Reasons,
        string? SuggestedRole,
        string? SuggestedDivision);
}
