//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Registry.Builders;
using Interception.UI.Domain.Enums;
using Interception.UI.Domain.ValueObjects;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services;

/// <summary>
/// Реалізація read-side реєстру перехоплень і деталізації одного повідомлення.
/// </summary>
public sealed class InterceptionQueryService(IDbContextFactory<AppDbContext> dbFactory) : IInterceptionQueryService
{
    /// <inheritdoc />
    public async Task<PagedResultDto<InterceptionListItemDto>> GetPagedAsync(
        InterceptionFilterDto filter,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var q = db.InterceptionMessages
            .Include(m => m.InterceptionAction)
            .Include(m => m.Participants)
            .Include(m => m.Labels)
            .AsNoTracking()
            .AsQueryable();
        var hasImpossibleParticipantFilter = false;

        if (filter.DateFrom.HasValue)
            q = q.Where(m => m.ObservedDate >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            q = q.Where(m => m.ObservedDate <= filter.DateTo.Value);
        var frequencyFilter = FrequencyCode.Create(filter.Frequency)?.Value;
        if (!string.IsNullOrWhiteSpace(frequencyFilter))
            q = q.Where(m => m.Frequency == frequencyFilter);
        if (!string.IsNullOrWhiteSpace(filter.VectorSignal))
            q = q.Where(m => m.VectorSignal != null && m.VectorSignal.Contains(filter.VectorSignal));
        if (!string.IsNullOrWhiteSpace(filter.ParticipantName))
        {
            // Шукаємо по відомих учасниках (Name Contains)
            // АБО по НВ що підтверджені як ця особа через ResolvedParticipant.
            // Без цього фільтр ігнорує записи де НВ [= ШАПКА ✓].
            var participantNameFilter = PersonName.Create(filter.ParticipantName)?.Value;
            if (string.IsNullOrWhiteSpace(participantNameFilter))
            {
                hasImpossibleParticipantFilter = true;
            }
            else
            {
                var resolvedParticipantIds = await db.ResolvedParticipants
                    .Where(r => r.Name.Contains(participantNameFilter!))
                    .Select(r => r.Id)
                    .ToListAsync(ct);

                // ParticipantId НВ що належать підтвердженим особам
                HashSet<Guid> resolvedUnknownParticipantIds = [];

                if (resolvedParticipantIds.Count > 0)
                {
                    resolvedUnknownParticipantIds = await db.ParticipantCandidateGroups
                        .Where(g => g.Status == CandidateGroupStatus.Confirmed
                                 && g.ResolvedParticipantId != null
                                 && resolvedParticipantIds.Contains(g.ResolvedParticipantId!.Value))
                        .SelectMany(g => g.ParticipantRefs.Select(r => r.ParticipantId))
                        .ToHashSetAsync(ct);
                }

                q = resolvedUnknownParticipantIds.Count > 0
                    ? q.Where(m =>
                        m.Participants.Any(p => p.Name != null && p.Name.Contains(participantNameFilter!)) ||
                        m.Participants.Any(p => p.IsUnknown && resolvedUnknownParticipantIds.Contains(p.Id)))
                    : q.Where(m =>
                        m.Participants.Any(p => p.Name != null && p.Name.Contains(participantNameFilter!)));
            }
        }

        if (hasImpossibleParticipantFilter)
            q = q.Where(_ => false);

        if (!string.IsNullOrWhiteSpace(filter.LabelName))
            q = q.Where(m => m.Labels.Any(l => l.NameLabel == filter.LabelName));

        var total = await q.CountAsync(ct);

        var rawItems = await q
            .OrderByDescending(m => m.ObservedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new InterceptionRegistryItemSnapshot(
                m.Id,
                m.ObservedDate,
                m.Frequency,
                m.VectorSignal,
                m.Division,
                m.InterceptionAction != null ? m.InterceptionAction.Name : null,
                m.Participants
                    .OrderBy(p => p.Ordinal)
                    .Select(p => new InterceptionRegistryParticipantSnapshot(p.Id, p.Name, p.Role, p.IsUnknown, p.Ordinal))
                    .ToList(),
                m.Labels.Select(l => l.NameLabel).ToList()))
            .ToListAsync(ct);

        var overlayMap = await BuildUnknownOverlayMapAsync(db, rawItems, ct);

        var items = rawItems.Select(m => new InterceptionListItemDto
        {
            Id = m.Id,
            ObservedDate = m.ObservedDate,
            Frequency = FrequencyCode.Create(m.Frequency)?.Value,
            VectorSignal = m.VectorSignal,
            Division = DivisionName.Create(m.Division)?.Value,
            ActionName = m.ActionName,
            Participants = [.. m.Participants.Select(p =>
            {
                overlayMap.TryGetValue(p.Id, out var overlay);
                return new ParticipantBriefDto
                {
                    Name = p.Name,
                    Role = p.Role,
                    IsUnknown = p.IsUnknown,
                    Ordinal = p.Ordinal,
                    ResolvedName = overlay.Name,
                    IsResolved = overlay.IsResolved,
                };
            })],
            Labels = [.. m.Labels],
        }).ToList();

        return new PagedResultDto<InterceptionListItemDto>(items, total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<InterceptionDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var message = await db.InterceptionMessages
            .AsNoTracking()
            .Where(m => m.Id == id)
            .FirstOrDefaultAsync(ct);

        if (message is null)
            return null;

        return new InterceptionDetailsDto
        {
            Id = message.Id,
            ObservedDate = message.ObservedDate,
            Frequency = FrequencyCode.Create(message.Frequency)?.Value,
            Division = DivisionName.Create(message.Division)?.Value,
            PointSignal = message.PointSignal,
            VectorSignal = message.VectorSignal,
            InterceptionActionId = message.InterceptionActionId,
            Note = message.Note,
            Participants = [.. message.Participants
                .OrderBy(p => p.Ordinal)
                .Select(p => new InterceptionDetailsParticipantDto
                {
                    Ordinal = p.Ordinal,
                    Name = p.Name,
                    Role = p.Role,
                    IsUnknown = p.IsUnknown
                })],
            Labels = [.. message.Labels
                .OrderBy(l => l.NameLabel)
                .Select(l => l.NameLabel)]
        };
    }

    /// <summary>
    /// Будує overlay-map для unknown participants у реєстрі.
    /// </summary>
    private static async Task<Dictionary<Guid, (string? Name, bool IsResolved)>> BuildUnknownOverlayMapAsync(
        AppDbContext db,
        IReadOnlyList<InterceptionRegistryItemSnapshot> rawItems,
        CancellationToken ct)
    {
        var unknownParticipantIds = rawItems
            .SelectMany(m => m.Participants.Where(p => p.IsUnknown).Select(p => p.Id))
            .ToHashSet();

        var overlayMap = new Dictionary<Guid, (string? Name, bool IsResolved)>();

        if (unknownParticipantIds.Count == 0)
            return overlayMap;

        var confirmedGroups = await db.ParticipantCandidateGroups
            .Where(g => g.Status == CandidateGroupStatus.Confirmed && g.ResolvedParticipantId != null)
            .OrderByDescending(g => g.ConfidenceScore)
            .AsNoTracking()
            .ToListAsync(ct);

        if (confirmedGroups.Count > 0)
        {
            var resolvedIds = confirmedGroups
                .Select(g => g.ResolvedParticipantId!.Value)
                .ToHashSet();

            var resolvedNames = await db.ResolvedParticipants
                .Where(r => resolvedIds.Contains(r.Id))
                .AsNoTracking()
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

            foreach (var group in confirmedGroups)
            {
                if (!resolvedNames.TryGetValue(group.ResolvedParticipantId!.Value, out var name))
                    continue;

                foreach (var reference in group.ParticipantRefs.Where(r => unknownParticipantIds.Contains(r.ParticipantId)))
                    overlayMap.TryAdd(reference.ParticipantId, (name, true));
            }
        }

        var openGroups = await db.ParticipantCandidateGroups
            .Where(g => g.Status == CandidateGroupStatus.Open && g.SuggestedName != null)
            .OrderByDescending(g => g.ConfidenceScore)
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var group in openGroups)
        {
            foreach (var reference in group.ParticipantRefs.Where(r => unknownParticipantIds.Contains(r.ParticipantId)))
                overlayMap.TryAdd(reference.ParticipantId, (group.SuggestedName, false));
        }

        return overlayMap;
    }
}
