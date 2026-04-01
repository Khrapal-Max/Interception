//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Analytics.Abstractions;
using Interception.Common.Extensions;
using Interception.Domain.Entities;
using Interception.Domain.Enums;

namespace Interception.Application.Analytics.Services;

/// <summary>
/// Реалізація write-side сценаріїв для життєвого циклу груп кандидатів.
/// </summary>
public sealed class ParticipantCandidateGroupCommandService(IDbContextFactory<AppDbContext> dbFactory)
    : IParticipantCandidateGroupCommandService
{
    /// <inheritdoc />
    public async Task ConfirmAsync(
        Guid groupId,
        string resolvedName,
        string resolvedBy,
        string? role = null,
        string? division = null,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var group = await db.ParticipantCandidateGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.Status == CandidateGroupStatus.Open, ct)
            ?? throw new InvalidOperationException($"Open-групу '{groupId}' не знайдено.");

        var normalizedName = StringSemanticValueExtensions.NormalizeMeaningfulOrNull(resolvedName)
            ?? throw new ArgumentException("Ім'я для підтвердження обов'язкове.", nameof(resolvedName));

        var normalizedRole = StringSemanticValueExtensions.NormalizeMeaningfulOrNull(role);
        var normalizedDivision = StringSemanticValueExtensions.NormalizeMeaningfulOrNull(division);

        ResolvedParticipant resolvedParticipant;

        if (group.ResolvedParticipantId.HasValue)
        {
            resolvedParticipant = await db.ResolvedParticipants
                .FirstOrDefaultAsync(x => x.Id == group.ResolvedParticipantId.Value, ct)
                ?? throw new InvalidOperationException(
                    $"ResolvedParticipant '{group.ResolvedParticipantId.Value}' не знайдено.");

            resolvedParticipant.Update(normalizedName, normalizedRole, normalizedDivision);
        }
        else
        {
            resolvedParticipant = await db.ResolvedParticipants
                .FirstOrDefaultAsync(x => x.Name == normalizedName, ct);

            if (resolvedParticipant is null)
            {
                resolvedParticipant = ResolvedParticipant.Create(normalizedName, resolvedBy, normalizedRole, normalizedDivision);
                db.ResolvedParticipants.Add(resolvedParticipant);
            }
            else
            {
                resolvedParticipant.Update(normalizedName, normalizedRole, normalizedDivision);
            }
        }

        group.Confirm(normalizedName, resolvedBy, resolvedParticipant.Id);

        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DismissAsync(Guid groupId, string resolvedBy, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var group = await db.ParticipantCandidateGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.Status == CandidateGroupStatus.Open, ct)
            ?? throw new InvalidOperationException($"Open-групу '{groupId}' не знайдено.");

        group.Dismiss(resolvedBy);
        await db.SaveChangesAsync(ct);
    }
}
