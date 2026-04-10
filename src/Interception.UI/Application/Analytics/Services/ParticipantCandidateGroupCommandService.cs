//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Registry.Services;
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
    /// Якщо існує точний контекстний збіг — використовує його.
    /// Якщо існує один "порожній" запис (без ролі та підрозділу) — збагачує його.
    /// Якщо збіг лише по імені, але контекст відрізняється — створює додатковий запис.
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
        var normalizedDivision = StringTextNormExtensions.NormalizeOption(division);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var roleMap = await ParticipantRoleCatalogSupport.LoadRoleMapAsync(db, ct);
        var normalizedRole = ParticipantRoleCatalogSupport.NormalizeRole(role, roleMap);

        var group = await db.ParticipantCandidateGroups
            .SingleOrDefaultAsync(x => x.Id == groupId, ct)
            ?? throw new InvalidOperationException($"ParticipantCandidateGroup '{groupId}' не знайдено.");

        var normalizedLookupName = StringTextNormExtensions.NormalizeOption(normalizedName);

        var sameNameResolved = await db.ResolvedParticipants
            .AsTracking()
            .Where(x => x.Name != null && StringTextNormExtensions.NormalizeOption(x.Name) == normalizedLookupName)
            .ToListAsync(ct);

        var resolved = FindReusableResolvedParticipant(
            sameNameResolved,
            normalizedRole,
            normalizedDivision);

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
            ApplyResolvedParticipantState(
                db,
                resolved,
                normalizedName!,
                normalizedResolvedBy,
                normalizedRole,
                normalizedDivision);
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

    private static ResolvedParticipant? FindReusableResolvedParticipant(
        IReadOnlyList<ResolvedParticipant> candidates,
        string? normalizedRole,
        string? normalizedDivision)
    {
        if (candidates.Count == 0)
            return null;

        var exactContextMatch = candidates.SingleOrDefault(x =>
            AreSameOptionalValue(x.Role, normalizedRole) &&
            AreSameOptionalValue(x.Division, normalizedDivision));

        if (exactContextMatch is not null)
            return exactContextMatch;

        var contextFreeCandidates = candidates
            .Where(x => string.IsNullOrWhiteSpace(StringTextNormExtensions.NormalizeOption(x.Role)) &&
                        string.IsNullOrWhiteSpace(StringTextNormExtensions.NormalizeOption(x.Division)))
            .ToList();

        return contextFreeCandidates.Count == 1
            ? contextFreeCandidates[0]
            : null;
    }

    private static void ApplyResolvedParticipantState(
        AppDbContext db,
        ResolvedParticipant resolved,
        string normalizedName,
        string normalizedResolvedBy,
        string? normalizedRole,
        string? normalizedDivision)
    {
        var entry = db.Entry(resolved);

        entry.Property(nameof(ResolvedParticipant.Name)).CurrentValue = normalizedName;
        entry.Property(nameof(ResolvedParticipant.ConfirmedBy)).CurrentValue = normalizedResolvedBy;

        if (!string.IsNullOrWhiteSpace(normalizedRole) || string.IsNullOrWhiteSpace(resolved.Role))
            entry.Property(nameof(ResolvedParticipant.Role)).CurrentValue = normalizedRole;

        if (!string.IsNullOrWhiteSpace(normalizedDivision) || string.IsNullOrWhiteSpace(resolved.Division))
            entry.Property(nameof(ResolvedParticipant.Division)).CurrentValue = normalizedDivision;
    }

    private static bool AreSameOptionalValue(string? left, string? right)
        => string.Equals(
            StringTextNormExtensions.NormalizeOption(left),
            StringTextNormExtensions.NormalizeOption(right),
            StringComparison.OrdinalIgnoreCase);
}
