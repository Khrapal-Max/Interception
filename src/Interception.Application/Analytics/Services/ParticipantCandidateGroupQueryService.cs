//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Analytics.Abstractions;
using Interception.Application.Analytics.Builders;
using Interception.Application.Analytics.Dtos;
using Interception.Application.Interceptions.Dtos;
using Interception.Domain.Entities;
using Interception.Domain.Enums;
using Interception.Domain.Extensions;
using Interception.Application.Abstractions;

namespace Interception.Application.Analytics.Services;

/// <summary>
/// Read-side сервіс для реєстру й деталей груп кандидатів.
/// </summary>
public sealed class ParticipantCandidateGroupQueryService(IAppDbContextFactory dbFactory) : IParticipantCandidateGroupQueryService
{
    /// <inheritdoc />
    public async Task<PagedResultDto<CandidateGroupDto>> GetGroupsByStatusAsync(
        CandidateGroupStatus status,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var total = await db.ParticipantCandidateGroups.CountAsync(g => g.Status == status, ct);

        var groups = await db.ParticipantCandidateGroups
            .Where(g => g.Status == status)
            .OrderByDescending(g => g.ConfidenceScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        var dtos = await EnrichGroupsAsync(db, groups, ct);
        return new PagedResultDto<CandidateGroupDto>([.. dtos], total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<CandidateGroupDto?> GetGroupByIdAsync(Guid id, CancellationToken ct = default)
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

    /// <summary>
    /// Збагачує доменні групи observation-знімками для read-side.
    /// </summary>
    private static async Task<IReadOnlyList<CandidateGroupDto>> EnrichGroupsAsync(
        IAppDbContext db,
        List<ParticipantCandidateGroup> groups,
        CancellationToken ct)
    {
        if (groups.Count == 0)
            return [];

        var allMessageIds = groups
            .SelectMany(g => g.ParticipantRefs.Select(r => (Guid)r.MessageId))
            .ToHashSet();

        var messages = await db.InterceptionMessages
            .Where(m => allMessageIds.Contains(m.Id))
            .Select(m => new MessageSnapshot(m.Id, m.ObservedDate, m.Frequency, m.VectorSignal, m.Division))
            .AsNoTracking()
            .ToDictionaryAsync(m => m.Id, ct);

        return [.. groups.Select(g => MapToDto(g, messages))];
    }

    /// <summary>
    /// Мапить доменну групу в DTO для сторінки.
    /// </summary>
    private static CandidateGroupDto MapToDto(ParticipantCandidateGroup group, Dictionary<Guid, MessageSnapshot> messages)
    {
        var refs = group.ParticipantRefs.Select(r =>
        {
            messages.TryGetValue((Guid)r.MessageId, out var msg);
            return new CandidateGroupRefDto
            {
                MessageId = r.MessageId,
                ParticipantId = r.ParticipantId,
                Ordinal = r.Ordinal,
                ObservedDate = msg?.ObservedDate ?? default,
                Frequency = SemanticValueExtensions.NormalizeMeaningfulOrNull(msg?.Frequency),
                VectorSignal = SemanticValueExtensions.NormalizeMeaningfulOrNull(msg?.VectorSignal),
                Division = SemanticValueExtensions.NormalizeMeaningfulOrNull(msg?.Division),
            };
        }).ToList();

        return new CandidateGroupDto
        {
            Id = group.Id,
            Status = group.Status,
            ConfidenceScore = group.ConfidenceScore,
            SuggestedName = group.SuggestedName,
            SuggestedRole = group.SuggestedRole,
            SuggestedDivision = group.SuggestedDivision,
            ResolvedParticipantId = group.ResolvedParticipantId,
            ResolvedBy = group.ResolvedBy,
            ResolvedAt = group.ResolvedAt,
            CreatedAt = group.CreatedAt,
            Reasons = new CandidateGroupReasonsDto
            {
                SameFrequency = group.Reasons.SameFrequency,
                SameVector = group.Reasons.SameVector,
                SamePointSignal = group.Reasons.SamePointSignal,
                SameDivision = group.Reasons.SameDivision,
                CloseInTime = group.Reasons.CloseInTime,
                SharedPartners = group.Reasons.SharedPartners,
                SharedLabels = group.Reasons.SharedLabels,
            },
            Refs = refs,
        };
    }
}
