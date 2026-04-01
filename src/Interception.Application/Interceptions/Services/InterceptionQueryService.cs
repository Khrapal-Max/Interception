//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Analytics.Dtos;
using Interception.Application.Interceptions.Abstractions;
using Interception.Application.Interceptions.Dtos;
using Interception.Application.Registry.Builders;
using Interception.Domain.Entities;
using Interception.Domain.Enums;
using Interception.Application.Abstractions;

namespace Interception.Application.Interceptions.Services;

/// <summary>
/// Реалізація read-side реєстру перехоплень і деталізації одного повідомлення.
/// </summary>
public sealed class InterceptionQueryService(IAppDbContextFactory dbFactory) : IInterceptionQueryService
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

        if (filter.DateFrom.HasValue)
            q = q.Where(m => m.ObservedDate >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            q = q.Where(m => m.ObservedDate <= filter.DateTo.Value);
        if (!string.IsNullOrWhiteSpace(filter.Frequency))
            q = q.Where(m => m.Frequency == filter.Frequency);
        if (!string.IsNullOrWhiteSpace(filter.VectorSignal))
            q = q.Where(m => m.VectorSignal != null && m.VectorSignal.Contains(filter.VectorSignal));
        if (!string.IsNullOrWhiteSpace(filter.ParticipantName))
        {
            // Шукаємо по відомих учасниках (Name Contains)
            // АБО по НВ що підтверджені як ця особа через ResolvedParticipant.
            // Без цього фільтр ігнорує записи де НВ [= ШАПКА ✓].
            var resolvedParticipantIds = await db.ResolvedParticipants
                .Where(r => r.Name.Contains(filter.ParticipantName))
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
                    m.Participants.Any(p => p.Name != null && p.Name.Contains(filter.ParticipantName)) ||
                    m.Participants.Any(p => p.IsUnknown && resolvedUnknownParticipantIds.Contains(p.Id)))
                : q.Where(m =>
                    m.Participants.Any(p => p.Name != null && p.Name.Contains(filter.ParticipantName)));
        }
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
            Frequency = m.Frequency,
            VectorSignal = m.VectorSignal,
            Division = m.Division,
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
    public async Task<InterceptionMessage?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.InterceptionMessages
            .Include(m => m.InterceptionAction)
            .Include(m => m.Participants)
            .Include(m => m.Labels)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    /// <summary>
    /// Будує overlay-map для unknown participants у реєстрі.
    /// </summary>
    private static async Task<Dictionary<Guid, (string? Name, bool IsResolved)>> BuildUnknownOverlayMapAsync(
        IAppDbContext db,
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
