//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Командний сервіс для підтвердження або відхилення груп кандидатів.
/// </summary>
public sealed class ParticipantCandidateGroupCommandService(IDbContextFactory<AppDbContext> dbFactory) : IParticipantCandidateGroupCommandService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <summary>
    /// Підтверджує групу, зв'язуючи її з <see cref="ResolvedParticipant"/>.
    /// Якщо встановлена особа з таким ім'ям вже існує — використовує її,
    /// інакше створює новий запис.
    /// </summary>
    public async Task ConfirmAsync(
        Guid groupId,
        string resolvedName,
        string resolvedBy,
        string? role,
        string? division,
        CancellationToken ct = default)
    {
        var normalizedName = StringTextNormExtensions.NormalizeOption(resolvedName);
        var normalizedResolvedBy = StringTextNormExtensions.NormalizeRequired(resolvedBy);
        var normalizedRole = StringTextNormExtensions.NormalizeOption(role);
        var normalizedDivision = StringTextNormExtensions.NormalizeOption(division);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var group = await db.ParticipantCandidateGroups
            .SingleOrDefaultAsync(x => x.Id == groupId, ct)
            ?? throw new InvalidOperationException($"ParticipantCandidateGroup '{groupId}' не знайдено.");

        var normalizedLookupName = StringTextNormExtensions.NormalizeOption(normalizedName)!;

        var sameNameResolved = await db.ResolvedParticipants
            .AsTracking()
            .Where(x => x.Name != null && StringTextNormExtensions.NormalizeOption(x.Name) == normalizedLookupName)
            .ToListAsync(ct);

        ResolvedParticipant? resolved = null;

        // 1) Якщо вже є точний збіг по контексту — пере використовуємо його.
        var exactMatches = sameNameResolved
            .Where(x =>
                StringComparer.OrdinalIgnoreCase.Equals(StringTextNormExtensions.NormalizeOption(x.Role), normalizedRole) &&
                StringComparer.OrdinalIgnoreCase.Equals(StringTextNormExtensions.NormalizeOption(x.Division), normalizedDivision))
            .ToList();

        if (exactMatches.Count == 1)
        {
            resolved = exactMatches[0];
        }
        else
        {
            // 2) Якщо існує лише один частковий запис з цим ім'ям без власного контексту —
            //    можна безпечно дозаповнити його замість створення дубліката.
            var enrichable = sameNameResolved
                .Where(x => string.IsNullOrWhiteSpace(x.Role) && string.IsNullOrWhiteSpace(x.Division))
                .ToList();

            if (sameNameResolved.Count == 1 && enrichable.Count == 1)
                resolved = enrichable[0];
        }

        if (resolved is null)
        {
            resolved = ResolvedParticipant.Create(
                normalizedName!,
                normalizedResolvedBy,
                normalizedRole,
                normalizedDivision);

            db.ResolvedParticipants.Add(resolved);
        }
        else
        {
            var entry = db.Entry(resolved);
            entry.Property(nameof(ResolvedParticipant.Name)).CurrentValue = normalizedName;
            entry.Property(nameof(ResolvedParticipant.Role)).CurrentValue = normalizedRole;
            entry.Property(nameof(ResolvedParticipant.Division)).CurrentValue = normalizedDivision;
            entry.Property(nameof(ResolvedParticipant.ConfirmedBy)).CurrentValue = normalizedResolvedBy;
        }

        group.Confirm(normalizedName!, normalizedResolvedBy, resolved.Id);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Відхиляє групу кандидатів.
    /// </summary>
    public async Task DismissAsync(
        Guid groupId,
        string resolvedBy,
        CancellationToken ct = default)
    {
        var normalizedResolvedBy = StringTextNormExtensions.NormalizeRequired(resolvedBy);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var group = await db.ParticipantCandidateGroups
            .SingleOrDefaultAsync(x => x.Id == groupId, ct)
            ?? throw new InvalidOperationException($"ParticipantCandidateGroup '{groupId}' не знайдено.");

        group.Dismiss(normalizedResolvedBy);

        await db.SaveChangesAsync(ct);
    }
}
