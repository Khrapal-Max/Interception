//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Registry;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services.Registry;

/// <summary>
/// Реєстр осіб системи.
/// </summary>
public sealed class PersonRegistryService(IDbContextFactory<AppDbContext> dbFactory)
    : IPersonRegistryService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PersonRegistryItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var observedRows = await db.InterceptionMessages
            .AsNoTracking()
            .SelectMany(
                x => x.Participants,
                (message, participant) => new
                {
                    participant.Id,
                    participant.Name,
                    participant.Role,
                    participant.IsUnknown,
                    message.Division,
                    message.ObservedDate
                })
            .Where(x => !x.IsUnknown && x.Name != null)
            .ToListAsync(ct);

        var observedPeople = observedRows
            .GroupBy(x => NormalizeKey(x.Name)!, StringComparer.Ordinal)
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(x => x.ObservedDate)
                    .ToList();

                var latest = ordered[0];
                var role = ordered
                    .Select(x => SemanticValue.NormalizeMeaningfulOrNull(x.Role))
                    .FirstOrDefault(x => x is not null);
                var division = ordered
                    .Select(x => SemanticValue.NormalizeMeaningfulOrNull(x.Division))
                    .FirstOrDefault(x => x is not null);

                return new PersonRegistryItemDto
                {
                    Id = latest.Id,
                    Name = latest.Name!,
                    Role = role,
                    Division = division,
                    IsConfirmed = false
                };
            })
            .ToDictionary(x => NormalizeKey(x.Name)!, x => x, StringComparer.Ordinal);

        var resolvedRows = await db.ResolvedParticipants
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        foreach (var resolved in resolvedRows)
        {
            observedPeople[NormalizeKey(resolved.Name)!] = MapToDto(resolved);
        }

        return observedPeople.Values
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<PersonRegistryItemDto> UpdateAsync(Guid id, PersonRegistryUpdateDto dto, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var normalizedName = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new ArgumentException("Ім'я особи обов'язкове.", nameof(dto));

        var normalizedRole = SemanticValue.NormalizeMeaningfulOrNull(dto.Role);
        var normalizedDivision = SemanticValue.NormalizeMeaningfulOrNull(dto.Division);
        var normalizedNameKey = NormalizeKey(normalizedName)!;

        var resolved = await db.ResolvedParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (resolved is not null)
        {
            var nameConflict = await db.ResolvedParticipants
                .AnyAsync(x => x.Id != id && x.Name.ToUpper() == normalizedNameKey, ct);

            if (nameConflict)
                throw new InvalidOperationException($"Встановлена особа з ім'ям '{normalizedName}' вже існує.");

            resolved.Update(normalizedName, normalizedRole, normalizedDivision);
            await db.SaveChangesAsync(ct);
            return MapToDto(resolved);
        }

        var participant = await db.InterceptionMessageParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (participant is null || participant.IsUnknown || string.IsNullOrWhiteSpace(participant.Name))
            throw new InvalidOperationException($"Особу '{id}' не знайдено.");

        var observedConflict = await db.ResolvedParticipants
            .AnyAsync(x => x.Name.ToUpper() == normalizedNameKey, ct);

        if (observedConflict)
            throw new InvalidOperationException($"Встановлена особа з ім'ям '{normalizedName}' вже існує.");

        var created = ResolvedParticipant.Create(
            normalizedName,
            confirmedBy: "registry",
            role: normalizedRole,
            division: normalizedDivision);

        db.ResolvedParticipants.Add(created);

        // Для вже відомої особи зі спостереження синхронізуємо принаймні ім'я і роль,
        // щоб реєстр та звіти бачили один канонічний варіант назви.
        participant.ResolveAsKnown(normalizedName, normalizedRole);

        await db.SaveChangesAsync(ct);
        return MapToDto(created);
    }

    /// <summary>
    /// Перетворює встановлену особу в DTO реєстру.
    /// </summary>
    private static PersonRegistryItemDto MapToDto(ResolvedParticipant value)
        => new()
        {
            Id = value.Id,
            Name = value.Name,
            Role = value.Role,
            Division = value.Division,
            IsConfirmed = true
        };

    /// <summary>
    /// Нормалізує ключ імені для злиття записів.
    /// </summary>
    private static string? NormalizeKey(string? value)
        => SemanticValue.NormalizeKeyOrNull(value);
}
