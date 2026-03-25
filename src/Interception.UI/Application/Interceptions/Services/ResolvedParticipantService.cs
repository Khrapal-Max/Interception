//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services;

public sealed class ResolvedParticipantService(
    IDbContextFactory<AppDbContext> dbFactory) : IResolvedParticipantService
{
    // -------------------------------------------------------------------------
    // ConfirmGroupAsync
    // -------------------------------------------------------------------------

    public async Task<ResolvedParticipantDto> ConfirmGroupAsync(
        Guid groupId,
        ConfirmCandidateGroupDto form,
        string operatorName,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var group = await db.ParticipantCandidateGroups
            .FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new InvalidOperationException(
                $"Групу '{groupId}' не знайдено.");

        if (group.Status != CandidateGroupStatus.Open)
            throw new InvalidOperationException(
                $"Неможливо підтвердити групу зі статусом '{group.Status}'.");

        var duplicateObservationIds = group.ParticipantRefs
            .GroupBy(r => r.MessageId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateObservationIds.Count > 0)
            throw new InvalidOperationException(
                "Неможливо підтвердити групу: вона містить кілька НВ з одного спостереження. " +
                "Спершу перерахуйте групи після оновлення логіки або розділіть цей кейс вручну.");

        var normalizedName = form.Name.Trim();
        var normalizedRole = SemanticValue.NormalizeMeaningfulOrNull(form.Role);
        var normalizedDivision = SemanticValue.NormalizeMeaningfulOrNull(form.Division);

        // Перевіряємо чи ім'я вже зайняте
        var nameExists = await db.ResolvedParticipants
            .AnyAsync(r => r.Name == normalizedName, ct);

        if (nameExists)
            throw new InvalidOperationException(
                $"Встановлена особа з ім'ям '{normalizedName}' вже існує.");

        // Автоматично відхиляємо всі інші Open групи що містять тих самих НВ.
        // Після підтвердження вони не мають сенсу — НВ вже ідентифіковані.
        var participantIds = group.ParticipantRefs
            .Select(r => r.ParticipantId)
            .ToList();

        var duplicateGroups = await db.ParticipantCandidateGroups
            .Where(g => g.Id != groupId
                     && g.Status == CandidateGroupStatus.Open
                     && g.ParticipantRefs.Any(r => participantIds.Contains(r.ParticipantId)))
            .ToListAsync(ct);

        foreach (var dup in duplicateGroups)
            dup.Dismiss(operatorName);

        // Створюємо ResolvedParticipant
        var resolved = ResolvedParticipant.Create(
            name: normalizedName,
            confirmedBy: operatorName,
            role: normalizedRole,
            division: normalizedDivision);

        db.ResolvedParticipants.Add(resolved);

        // Спочатку оновлюємо suggestion-поля поки група ще Open,
        // потім переводимо її у Confirmed і прив'язуємо встановлену особу.
        group.UpdateSuggestedRole(normalizedRole);
        group.UpdateSuggestedDivision(normalizedDivision);
        group.Confirm(normalizedName, operatorName, resolved.Id);

        await db.SaveChangesAsync(ct);

        return MapToDto(resolved);
    }

    // -------------------------------------------------------------------------
    // DismissGroupAsync
    // -------------------------------------------------------------------------

    public async Task DismissGroupAsync(
        Guid groupId,
        string operatorName,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var group = await db.ParticipantCandidateGroups
            .FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new InvalidOperationException(
                $"Групу '{groupId}' не знайдено.");

        group.Dismiss(operatorName);
        await db.SaveChangesAsync(ct);
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<ResolvedParticipantDto>> GetAllAsync(
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        return await db.ResolvedParticipants
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => MapToDto(r))
            .ToListAsync(ct);
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    public async Task<ResolvedParticipantDto> UpdateAsync(
        Guid id,
        ConfirmCandidateGroupDto form,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var resolved = await db.ResolvedParticipants
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new InvalidOperationException(
                $"Встановлену особу '{id}' не знайдено.");

        var normalizedName = form.Name.Trim();
        var normalizedRole = SemanticValue.NormalizeMeaningfulOrNull(form.Role);
        var normalizedDivision = SemanticValue.NormalizeMeaningfulOrNull(form.Division);

        // Перевіряємо конфлікт імені з іншими
        var nameConflict = await db.ResolvedParticipants
            .AnyAsync(r => r.Name == normalizedName && r.Id != id, ct);

        if (nameConflict)
            throw new InvalidOperationException(
                $"Встановлена особа з ім'ям '{normalizedName}' вже існує.");

        resolved.Update(normalizedName, normalizedRole, normalizedDivision);
        await db.SaveChangesAsync(ct);

        return MapToDto(resolved);
    }

    // -------------------------------------------------------------------------
    // Mapping
    // -------------------------------------------------------------------------

    private static ResolvedParticipantDto MapToDto(ResolvedParticipant r) =>
        new()
        {
            Id = r.Id,
            Name = r.Name,
            Role = r.Role,
            Division = r.Division,
            ConfirmedBy = r.ConfirmedBy,
            ConfirmedAt = r.ConfirmedAt,
        };
}
