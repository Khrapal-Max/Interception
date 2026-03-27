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
            .GroupBy(x => BuildObservedKey(x.Name, x.Division), StringComparer.Ordinal)
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
            .ToList();

        var resolvedPeople = await db.ResolvedParticipants
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => MapToDto(x))
            .ToListAsync(ct);

        var items = new List<PersonRegistryItemDto>(resolvedPeople);

        foreach (var observed in observedPeople)
        {
            var hasExactConfirmed = resolvedPeople.Any(x => SamePersonKey(x.Name, x.Division, observed.Name, observed.Division));
            if (!hasExactConfirmed)
            {
                items.Add(observed);
            }
        }

        return [.. items
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.Division, StringComparer.Ordinal)
            .ThenBy(x => x.Role, StringComparer.Ordinal)];
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

        var resolved = await db.ResolvedParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (resolved is not null)
        {
            resolved.Update(normalizedName, normalizedRole, normalizedDivision);
            await db.SaveChangesAsync(ct);
            return MapToDto(resolved);
        }

        var participant = await db.InterceptionMessageParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (participant is null || participant.IsUnknown || string.IsNullOrWhiteSpace(participant.Name))
            throw new InvalidOperationException($"Особу '{id}' не знайдено.");

        var created = ResolvedParticipant.Create(
            normalizedName,
            confirmedBy: "registry",
            role: normalizedRole,
            division: normalizedDivision);

        db.ResolvedParticipants.Add(created);

        // Для відомої особи зі спостереження синхронізуємо канонічне ім'я і роль,
        // щоб реєстр і звіти бачили один варіант назви.
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
    /// Будує ключ observed-особи для злиття рядків реєстру.
    /// </summary>
    private static string BuildObservedKey(string? name, string? division)
        => $"{NormalizeKey(name) ?? string.Empty}|{NormalizeKey(division) ?? string.Empty}";

    /// <summary>
    /// Перевіряє, що два рядки описують одну й ту саму особу для реєстру.
    /// </summary>
    private static bool SamePersonKey(string? leftName, string? leftDivision, string? rightName, string? rightDivision)
        => string.Equals(NormalizeKey(leftName), NormalizeKey(rightName), StringComparison.Ordinal)
            && string.Equals(NormalizeKey(leftDivision), NormalizeKey(rightDivision), StringComparison.Ordinal);

    /// <summary>
    /// Нормалізує ключ значення для порівняння.
    /// </summary>
    private static string? NormalizeKey(string? value)
        => SemanticValue.NormalizeKeyOrNull(value);
}
