//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Registry.Dtos;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Registry.Services;

/// <summary>
/// Сервіс реєстру осіб.
/// </summary>
public sealed class PersonRegistryService(
    IDbContextFactory<AppDbContext> dbFactory) : IPersonRegistryService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PersonRegistryItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var confirmed = await db.ResolvedParticipants
            .AsNoTracking()
            .Select(x => new PersonRegistryItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Role = x.Role,
                Division = x.Division,
                IsConfirmed = true,
                ConfirmedBy = x.ConfirmedBy,
                ConfirmedAt = x.ConfirmedAt
            })
            .ToListAsync(ct);

        var confirmedByName = confirmed
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<PersonRegistryItemDto>)x.ToList(), StringComparer.OrdinalIgnoreCase);

        var observedRows = await db.InterceptionMessages
            .AsNoTracking()
            .SelectMany(
                message => message.Participants,
                (message, participant) => new
                {
                    ParticipantId = participant.Id,
                    participant.Name,
                    participant.Role,
                    participant.IsUnknown,
                    message.Division
                })
            .Where(x =>
                !x.IsUnknown &&
                !string.IsNullOrWhiteSpace(x.Name))
            .ToListAsync(ct);

        var observed = observedRows
             .GroupBy(x => new
             {
                 NameKey = x.Name!.Trim().ToUpperInvariant(),
                 DivisionKey = (SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Division) ?? string.Empty).ToUpperInvariant()
             })
             .Select(group => new PersonRegistryItemDto
             {
                 Id = group.OrderBy(x => x.ParticipantId).Select(x => x.ParticipantId).First(),
                 Name = group.Select(x => x.Name!.Trim()).First(),
                 Role = group.Select(x => SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Role))
                     .FirstOrDefault(x => x is not null),
                 Division = string.IsNullOrWhiteSpace(group.Key.DivisionKey)
                     ? null
                     : group.Select(x => SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Division))
                         .FirstOrDefault(x => x is not null),
                 IsConfirmed = false,
                 ConfirmedBy = null,
                 ConfirmedAt = null
             })
             .Where(x => !ShouldHideObservedRow(x, confirmedByName))
             .ToList();

        return [.. confirmed
             .Concat(observed)
             .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
             .ThenBy(x => x.Division, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc />
    public async Task<PersonRegistryItemDto> UpdateAsync(Guid id, PersonRegistryUpdateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var normalizedName = dto.Name.Trim();
        var normalizedRole = SemanticValueExtensions.NormalizeMeaningfulOrNull(dto.Role);
        var normalizedDivision = SemanticValueExtensions.NormalizeMeaningfulOrNull(dto.Division);

        var resolved = await db.ResolvedParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (resolved is not null)
        {
            resolved.Update(normalizedName, normalizedRole, normalizedDivision);
            await db.SaveChangesAsync(ct);

            return new PersonRegistryItemDto
            {
                Id = resolved.Id,
                Name = resolved.Name,
                Role = resolved.Role,
                Division = resolved.Division,
                IsConfirmed = true,
                ConfirmedBy = resolved.ConfirmedBy,
                ConfirmedAt = resolved.ConfirmedAt
            };
        }

        var participant = await db.InterceptionMessageParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (participant is null || participant.IsUnknown || string.IsNullOrWhiteSpace(participant.Name))
            throw new InvalidOperationException($"Особу '{id}' не знайдено.");

        participant.ResolveAsKnown(normalizedName, normalizedRole);

        var created = ResolvedParticipant.Create(
            normalizedName,
            confirmedBy: "registry",
            role: normalizedRole,
            division: normalizedDivision);

        db.ResolvedParticipants.Add(created);
        await db.SaveChangesAsync(ct);

        return new PersonRegistryItemDto
        {
            Id = created.Id,
            Name = created.Name,
            Role = created.Role,
            Division = created.Division,
            IsConfirmed = true,
            ConfirmedBy = created.ConfirmedBy,
            ConfirmedAt = created.ConfirmedAt
        };
    }

    /// <summary>
    /// Визначає, чи треба приховати observed-рядок, якщо для нього вже існує
    /// однозначний canonical person.
    /// </summary>
    private static bool ShouldHideObservedRow(
        PersonRegistryItemDto observed,
        Dictionary<string, IReadOnlyList<PersonRegistryItemDto>> confirmedByName)
    {
        if (!confirmedByName.TryGetValue(observed.Name, out var confirmedRows) || confirmedRows.Count == 0)
            return false;

        var observedRole = SemanticValueExtensions.NormalizeMeaningfulOrNull(observed.Role);
        var observedDivision = SemanticValueExtensions.NormalizeMeaningfulOrNull(observed.Division);

        // 1) Найсильніший сигнал: exact-ish profile match.
        if (confirmedRows.Any(x =>
                StringComparer.OrdinalIgnoreCase.Equals(
                    SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Role),
                    observedRole) &&
                StringComparer.OrdinalIgnoreCase.Equals(
                    SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Division),
                    observedDivision)))
        {
            return true;
        }

        // 2) Якщо в observed немає підрозділу, але є рівно один confirmed з таким ім'ям і роллю,
        //    вважаємо це тим самим уже підтвердженим записом.
        if (observedDivision is null && observedRole is not null)
        {
            var sameRole = confirmedRows
                .Where(x => StringComparer.OrdinalIgnoreCase.Equals(
                    SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Role),
                    observedRole))
                .ToList();

            if (sameRole.Count == 1)
                return true;
        }

        // 3) Старий безпечний fallback: якщо confirmed рядок з таким ім'ям лише один,
        //    приховуємо observed-дубль навіть коли контекст бідний.
        if (confirmedRows.Count == 1)
            return true;

        return false;
    }
}
